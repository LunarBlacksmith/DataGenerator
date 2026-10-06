using DataGenerator.Interfaces;
using DataGenerator.Models;

namespace DataGenerator.Services.Generation;

/// <summary>
/// Validates a <see cref="GenerationRequest"/> and prepares everything the script writer and direct inserter need.
/// </summary>
internal sealed class GenerationBlueprintBuilder
{
	private readonly ISqlValueConverter    _converter;
	private readonly IColumnValueGenerator _valueGenerator;
	private readonly IPatternSqlTranslator _patternTranslator;

	public GenerationBlueprintBuilder(ISqlValueConverter converter, IColumnValueGenerator valueGenerator, IPatternSqlTranslator patternTranslator)
	{
		_converter         = converter         ?? throw new ArgumentNullException(nameof(converter));
		_valueGenerator    = valueGenerator    ?? throw new ArgumentNullException(nameof(valueGenerator));
		_patternTranslator = patternTranslator ?? throw new ArgumentNullException(nameof(patternTranslator));
	}

	public GenerationBlueprint Build(GenerationRequest request)
	{
		ArgumentNullException.ThrowIfNull(request);

		bool                                    isScript         = request.Mode == GenerationMode.SqlFile;
		Dictionary<string, TableGenerationPlan> plansByKey       = IndexPlans(request.Plans);
		Dictionary<string, int>                 firstInsertSteps = FindFirstInsertSteps(request.Plans);

		foreach (TableGenerationPlan plan in request.Plans)
		{
			ValidatePlan(plan, plansByKey, firstInsertSteps);
		}

		RowSnapshotSet                     snapshots    = new();
		IReadOnlyList<TableGenerationPlan> orderedPlans = OrderPlans(request.Plans, plansByKey);
		BuildContext                       context      = new()
		{
			KeyTables     = CreateKeyTables(request.Plans, isScript),
			Snapshots     = snapshots,
			LookupFactory = new LookupPoolFactory(_converter, _patternTranslator, snapshots, isScript),
			IsScript      = isScript
		};
		List<TableBlueprint>               tables       = [];

		foreach (TableGenerationPlan plan in orderedPlans)
		{
			List<RowSetBlueprint> rowSets = [];

			_ = context.KeyTables.TryGetValue(plan.Table.Key, out GeneratedKeyTable? keys);

			foreach (RowSetPlan rowSet in plan.RowSets)
			{
				rowSets.Add(BuildRowSet(plan.Table, rowSet, keys, context));
			}

			tables.Add(new TableBlueprint
			{
				Table   = plan.Table,
				RowSets = rowSets,
				Keys    = keys
			});
		}

		return new GenerationBlueprint
		{
			Tables             = tables,
			Operations         = OrderOperations(tables),
			ExistingKeyPools   = [.. context.Pools.Values],
			Snapshots          = snapshots.Snapshots,
			TablesToClear      = TableDependencySorter.SortForDeletion(request.TablesToClear),
			ResetIdentitySeeds = request.ResetIdentitySeeds,
			PostGeneration     = request.PostGeneration is { Statements.Count: > 0 } ? request.PostGeneration : null
		};
	}

	public static string DescribeLocation(TableModel table, RowSetPlan? rowSet = null, long? rowIndex = null, ColumnModel? column = null)
		=> GenerationLocation.Describe(table, rowSet?.Name, rowIndex, column?.Name);

	/// <summary>
	/// Tables that insert rows come first, referenced tables before the tables that reference them (only insert sets
	/// count, because update sets do not create keys); tables with only update sets follow in request order.
	/// </summary>
	private static IReadOnlyList<TableGenerationPlan> OrderPlans(
		IReadOnlyList<TableGenerationPlan>      plans,
		Dictionary<string, TableGenerationPlan> plansByKey
	)
	{
		List<TableGenerationPlan> insertPlans =
		[
			.. plans
				.Where(plan => plan.RowSets.Any(rowSet => !rowSet.IsUpdate))
				.Select(plan => new TableGenerationPlan { Table = plan.Table, RowSets = [.. plan.RowSets.Where(rowSet => !rowSet.IsUpdate)] })
		];

		List<TableGenerationPlan> ordered     = [.. TableDependencySorter.SortForInsertion(insertPlans).Select(plan => plansByKey[plan.Table.Key])];
		HashSet<string>           orderedKeys = new(ordered.Select(plan => plan.Table.Key), StringComparer.OrdinalIgnoreCase);

		ordered.AddRange(plans.Where(plan => !orderedKeys.Contains(plan.Table.Key)));

		return ordered;
	}

	/// <summary>
	/// Runs the row sets step by step; within a step the insert sets run first (in table order), then the update sets.
	/// </summary>
	private static List<GenerationOperation> OrderOperations(List<TableBlueprint> tables)
	{
		List<int>                 steps      = [.. tables.SelectMany(table => table.RowSets).Select(rowSet => rowSet.Plan.Step).Distinct().Order()];
		List<GenerationOperation> operations = [];

		foreach (int step in steps)
		{
			foreach (bool isUpdate in (bool[])[false, true])
			{
				foreach (TableBlueprint table in tables)
				{
					operations.AddRange(
						table.RowSets
							.Where(rowSet => rowSet.Plan.Step == step && rowSet.Plan.IsUpdate == isUpdate)
							.Select(rowSet => new GenerationOperation { Table = table, RowSet = rowSet })
					);
				}
			}
		}

		return operations;
	}

	/// <summary>
	/// The first step in which each table inserts rows, by table key.
	/// </summary>
	private static Dictionary<string, int> FindFirstInsertSteps(IReadOnlyList<TableGenerationPlan> plans)
	{
		Dictionary<string, int> steps = new(StringComparer.OrdinalIgnoreCase);

		foreach (TableGenerationPlan plan in plans)
		{
			List<int> insertSteps = [.. plan.RowSets.Where(rowSet => !rowSet.IsUpdate).Select(rowSet => rowSet.Step)];

			if (insertSteps.Count > 0)
			{
				steps[plan.Table.Key] = insertSteps.Min();
			}
		}

		return steps;
	}

	private static Dictionary<string, TableGenerationPlan> IndexPlans(IReadOnlyList<TableGenerationPlan> plans)
	{
		Dictionary<string, TableGenerationPlan> plansByKey = new(StringComparer.OrdinalIgnoreCase);

		foreach (TableGenerationPlan plan in plans)
		{
			if (!plansByKey.TryAdd(plan.Table.Key, plan))
			{
				throw new DataGenerationException(
					"The same table appears more than once in the request. Use row sets to generate several groups of rows for one table.",
					DescribeLocation(plan.Table)
				);
			}
		}

		return plansByKey;
	}

	private void ValidatePlan(
		TableGenerationPlan                     plan,
		Dictionary<string, TableGenerationPlan> plansByKey,
		Dictionary<string, int>                 firstInsertSteps
	)
	{
		if (plan.RowSets.Count == 0)
		{
			throw new DataGenerationException("The table has no row sets.", DescribeLocation(plan.Table));
		}

		foreach (RowSetPlan rowSet in plan.RowSets)
		{
			if (rowSet.RowCount < 1)
			{
				throw new DataGenerationException("Each row set must generate at least one row.", DescribeLocation(plan.Table, rowSet));
			}

			if (FindRowSetProblem(plan.Table, rowSet) is string rowSetProblem)
			{
				throw new DataGenerationException(rowSetProblem, DescribeLocation(plan.Table, rowSet));
			}

			HashSet<string> columnNames = new(StringComparer.OrdinalIgnoreCase);

			foreach (ColumnRule rule in rowSet.Rules)
			{
				if (!columnNames.Add(rule.Column.Name))
				{
					throw new DataGenerationException(
						"The column has more than one rule in the same row set.",
						DescribeLocation(plan.Table, rowSet, null, rule.Column)
					);
				}

				string? problem = FindRuleProblem(rule, rowSet, plansByKey, firstInsertSteps) ?? FindReferenceProblem(rule, rowSet.Rules);

				if (problem is not null)
				{
					throw new DataGenerationException(problem, DescribeLocation(plan.Table, rowSet, null, rule.Column));
				}
			}

			_ = SortByReferences(rowSet.Rules, out List<string> cycle);

			if (cycle.Count > 0)
			{
				throw new DataGenerationException(
					$"The columns use each other's values in a loop ({string.Join(" → ", cycle)}), so none of them can be generated first. "
						+ "Change one of them to another mode.",
					DescribeLocation(plan.Table, rowSet)
				);
			}
		}
	}

	private static string? FindRowSetProblem(TableModel table, RowSetPlan rowSet)
	{
		if (rowSet.Step < RowSetPlan.FIRST_STEP)
		{
			return $"The step must be {RowSetPlan.FIRST_STEP} or higher.";
		}

		if (!rowSet.IsUpdate)
		{
			return null;
		}

		if (rowSet.Rules.All(rule => rule.GenerationMode == ValueGenerationMode.KeepCurrent))
		{
			return "The update set does not change any column. Choose another mode than 'Keep current value' for at least one column.";
		}

		return rowSet.UpdateScope != RowScope.Any && !table.Columns.Any(column => column.IsPrimaryKey)
			? "Only tables with a primary key can limit an update set to generated or existing rows. Use 'Any rows' and a condition instead."
			: null;
	}

	private string? FindReferenceProblem(ColumnRule rule, IReadOnlyList<ColumnRule> rules)
	{
		if (rule.GenerationMode == ValueGenerationMode.CopyColumn && string.IsNullOrWhiteSpace(rule.SourceColumnName))
		{
			return "Choose the column to copy the value from.";
		}

		foreach (string columnName in _valueGenerator.GetReferencedColumns(rule))
		{
			ColumnRule? referencedRule = rules.FirstOrDefault(item => string.Equals(item.Column.Name, columnName, StringComparison.OrdinalIgnoreCase));

			if (referencedRule is null)
			{
				return $"The column uses the value of [{columnName}], but the table has no column with that name.";
			}

			if (ReferenceEquals(referencedRule, rule))
			{
				return "The column cannot use its own value. Choose another column.";
			}

			if (referencedRule.GenerationMode == ValueGenerationMode.KeepCurrent)
			{
				return $"The column uses the value of [{columnName}], but that column keeps its current value, which is not known "
					+ $"in advance. Choose another column, or another mode for [{columnName}].";
			}

			if (referencedRule.GenerationMode == ValueGenerationMode.DatabaseGenerated)
			{
				return $"The column uses the value of [{columnName}], but SQL Server generates that value while inserting the row, "
					+ "so it is not known in advance. Choose another column, or another mode for "
					+ $"[{columnName}].";
			}
		}

		return null;
	}

	/// <summary>
	/// Orders the rules so that every rule comes after the columns whose values it uses (Kahn's algorithm). When the rules
	/// use each other in a loop, <paramref name="cycle"/> receives the column names of the loop.
	/// </summary>
	private int[] SortByReferences(IReadOnlyList<ColumnRule> rules, out List<string> cycle)
	{
		Dictionary<string, int> indexesByName = new(StringComparer.OrdinalIgnoreCase);

		for (int index = 0; index < rules.Count; ++index)
		{
			_ = indexesByName.TryAdd(rules[index].Column.Name, index);
		}

		int[]       remainingReferences = new int[rules.Count];
		List<int>[] dependents          = new List<int>[rules.Count];
		List<int>[] references          = new List<int>[rules.Count];

		for (int index = 0; index < rules.Count; ++index)
		{
			dependents[index] = [];
			references[index] = [];
		}

		for (int index = 0; index < rules.Count; ++index)
		{
			foreach (string columnName in _valueGenerator.GetReferencedColumns(rules[index]))
			{
				if (indexesByName.TryGetValue(columnName, out int referencedIndex) && !references[index].Contains(referencedIndex))
				{
					references[index].Add(referencedIndex);
					dependents[referencedIndex].Add(index);
					++remainingReferences[index];
				}
			}
		}

		Queue<int> ready = new(Enumerable.Range(0, rules.Count).Where(index => remainingReferences[index] == 0));
		List<int>  order = new(rules.Count);

		while (ready.Count > 0)
		{
			int index = ready.Dequeue();

			order.Add(index);

			foreach (int dependent in dependents[index])
			{
				if (--remainingReferences[dependent] == 0)
				{
					ready.Enqueue(dependent);
				}
			}
		}

		cycle = order.Count == rules.Count ? [] : FindCycle(rules, references, remainingReferences);

		return [.. order];
	}

	private static List<string> FindCycle(IReadOnlyList<ColumnRule> rules, List<int>[] references, int[] remainingReferences)
	{
		// Every unsorted rule still uses another unsorted rule, so following those references must return to a visited rule.
		List<int> path    = [];
		int       current = Array.FindIndex(remainingReferences, count => count > 0);

		while (!path.Contains(current))
		{
			path.Add(current);
			current = references[current].First(index => remainingReferences[index] > 0);
		}

		List<string> cycle = [.. path.Skip(path.IndexOf(current)).Select(index => rules[index].Column.Name)];

		cycle.Add(rules[current].Column.Name);
		return cycle;
	}

	private string? FindRuleProblem(
		ColumnRule                              rule,
		RowSetPlan                              rowSet,
		Dictionary<string, TableGenerationPlan> plansByKey,
		Dictionary<string, int>                 firstInsertSteps
	)
	{
		ColumnModel column         = rule.Column;
		bool        isDatabaseOnly = column.IsIdentity || column.IsComputed || _converter.GetCategory(column) == SqlTypeCategory.RowVersion;
		bool        isDatabaseRule = rule.GenerationMode == ValueGenerationMode.DatabaseGenerated;

		if (rule.GenerationMode == ValueGenerationMode.KeepCurrent)
		{
			return rowSet.IsUpdate ? null : "'Keep current value' can only be used in update sets.";
		}

		if (rowSet.IsUpdate && (isDatabaseOnly || column.IsPrimaryKey))
		{
			return "Update sets cannot change primary key columns or columns that SQL Server generates. Use 'Keep current value'.";
		}

		if (rowSet.IsUpdate && isDatabaseRule)
		{
			return "'Database generated' can only be used in insert sets. Use 'Keep current value' to leave the column unchanged.";
		}

		if (isDatabaseOnly && !isDatabaseRule)
		{
			return "SQL Server always generates this column (identity, computed or rowversion). Use the 'Database generated' mode.";
		}

		if (isDatabaseRule && !isDatabaseOnly && !column.HasDefault && !column.IsNullable)
		{
			return "The column has no default value and does not allow NULL, so SQL Server cannot generate it. Choose another mode.";
		}

		if (rule.GenerationMode == ValueGenerationMode.Null && !column.IsNullable)
		{
			return "The column does not allow NULL values.";
		}

		if (rule.GenerationMode == ValueGenerationMode.TableLookup)
		{
			return FindLookupProblem(rule, rowSet, firstInsertSteps);
		}

		if (rule.GenerationMode is not (ValueGenerationMode.GeneratedForeignKey or ValueGenerationMode.ExistingForeignKey))
		{
			return null;
		}

		ForeignKeyModel? reference = rule.Reference;

		if (reference is null)
		{
			return "The column does not reference another table, so it cannot use a key mode.";
		}

		if (rule.GenerationMode == ValueGenerationMode.ExistingForeignKey)
		{
			return null;
		}

		if (!plansByKey.TryGetValue(reference.ReferencedTableKey, out TableGenerationPlan? referencedPlan))
		{
			return $"'Generated key' takes values from rows generated for {reference.ReferencedSchema}.{reference.ReferencedTable}, "
				+ "but that table is not included. Include it, or use 'Existing key' to pick keys that already exist.";
		}

		if (!InsertsBy(firstInsertSteps, reference.ReferencedTableKey, rowSet.Step))
		{
			return $"'Generated key' takes values from rows generated for {referencedPlan.Table.DisplayName}, but no insert set of "
				+ $"that table runs in step {rowSet.Step} or earlier. Move an insert set of that table to an earlier step, "
				+ "or use 'Existing key'.";
		}

		bool referencedColumnExists = referencedPlan.Table.Columns.Any(
			item => string.Equals(item.Name, reference.ReferencedColumn, StringComparison.OrdinalIgnoreCase)
		);

		return referencedColumnExists
			? null
			: $"The referenced column [{reference.ReferencedColumn}] does not exist in {referencedPlan.Table.DisplayName}.";
	}

	private static string? FindLookupProblem(ColumnRule rule, RowSetPlan rowSet, Dictionary<string, int> firstInsertSteps)
	{
		if (rule.Lookup is not ColumnLookup lookup)
		{
			return "Enter which table column the values come from, e.g. dbo.Shirt.ShirtID.";
		}

		if (lookup.Scope == RowScope.Generated && !InsertsBy(firstInsertSteps, lookup.SourceTable.Key, rowSet.Step))
		{
			return $"'Value from table' uses rows generated for {lookup.SourceTable.DisplayName}, but no insert set of that table "
				+ $"runs in step {rowSet.Step} or earlier. Move an insert set of that table to an earlier step, or use FROM ANY.";
		}

		return null;
	}

	private static bool InsertsBy(Dictionary<string, int> firstInsertSteps, string tableKey, int step)
		=> firstInsertSteps.TryGetValue(tableKey, out int insertStep) && insertStep <= step;

	private static Dictionary<string, GeneratedKeyTable> CreateKeyTables(IReadOnlyList<TableGenerationPlan> plans, bool isScript)
	{
		Dictionary<string, List<string>> columnNamesByTable = new(StringComparer.OrdinalIgnoreCase);

		foreach (ColumnRule rule in plans.SelectMany(plan => plan.RowSets).SelectMany(rowSet => rowSet.Rules))
		{
			ForeignKeyModel? reference = rule.Reference;

			if (rule.GenerationMode != ValueGenerationMode.GeneratedForeignKey || reference is null)
			{
				continue;
			}

			if (!columnNamesByTable.TryGetValue(reference.ReferencedTableKey, out List<string>? columnNames))
			{
				columnNames                                     = [];
				columnNamesByTable[reference.ReferencedTableKey] = columnNames;
			}

			if (!columnNames.Contains(reference.ReferencedColumn, StringComparer.OrdinalIgnoreCase))
			{
				columnNames.Add(reference.ReferencedColumn);
			}
		}

		Dictionary<string, GeneratedKeyTable> keyTables = new(StringComparer.OrdinalIgnoreCase);
		int                                   number    = 0;

		foreach (TableGenerationPlan plan in plans)
		{
			if (!columnNamesByTable.TryGetValue(plan.Table.Key, out List<string>? columnNames))
			{
				continue;
			}

			List<ColumnModel> columns = [.. plan.Table.Columns.Where(column => columnNames.Contains(column.Name, StringComparer.OrdinalIgnoreCase))];

			++number;

			bool    needsOutput        = isScript
				&& plan.RowSets.Any(rowSet => !rowSet.IsUpdate && columns.Any(column => IsProducedByDatabase(rowSet, column)));
			string? outputVariableName = needsOutput ? $"@dg_keys_{number}" : null;

			keyTables[plan.Table.Key] = new GeneratedKeyTable(plan.Table, columns, outputVariableName);
		}

		return keyTables;
	}

	/// <summary>
	/// Builds the value sources of a row set. Insert sets fill every column SQL Server does not generate; update sets
	/// change every column that does not keep its current value.
	/// </summary>
	private RowSetBlueprint BuildRowSet(TableModel table, RowSetPlan rowSet, GeneratedKeyTable? keys, BuildContext context)
	{
		List<ColumnRule>      valueRules  =
		[
			.. rowSet.Rules.Where(rule => rule.GenerationMode is not (ValueGenerationMode.DatabaseGenerated or ValueGenerationMode.KeepCurrent))
		];
		List<ExistingKeyPool> rowSetPools = [];
		List<LookupPool>      lookupPools = [];

		Dictionary<ColumnRule, ValueSource> existingSources = CreateExistingKeySources(valueRules, context.Pools, rowSetPools, context.IsScript, out int existingGroupCount);
		Dictionary<string, int>             generatedGroups = new(StringComparer.OrdinalIgnoreCase);
		List<ValueSource>                   sources         = [];

		foreach (ColumnRule rule in valueRules)
		{
			if (existingSources.TryGetValue(rule, out ValueSource? existingSource))
			{
				sources.Add(existingSource);
				continue;
			}

			if (rule.GenerationMode == ValueGenerationMode.TableLookup)
			{
				LookupPool lookupPool = context.LookupFactory.Create(table, rowSet, rule);

				lookupPools.Add(lookupPool);
				sources.Add(new ValueSource { Rule = rule, Kind = ValueSourceKind.Lookup, Lookup = lookupPool });
				continue;
			}

			ForeignKeyModel? reference = rule.Reference;

			if (rule.GenerationMode != ValueGenerationMode.GeneratedForeignKey || reference is null)
			{
				sources.Add(new ValueSource { Rule = rule, Kind = ValueSourceKind.Rule });
				continue;
			}

			GeneratedKeyTable parentKeys = context.KeyTables[reference.ReferencedTableKey];

			if (!generatedGroups.TryGetValue(reference.Name, out int groupIndex))
			{
				groupIndex = generatedGroups.Count;
				generatedGroups.Add(reference.Name, groupIndex);
			}

			sources.Add(new ValueSource
			{
				Rule        = rule,
				Kind        = ValueSourceKind.GeneratedKey,
				KeyTable    = parentKeys,
				ColumnIndex = parentKeys.IndexOf(reference.ReferencedColumn),
				GroupIndex  = groupIndex
			});
		}

		List<int> keySourceIndexes = keys is null || rowSet.IsUpdate
			? []
			: [.. keys.Columns.Select(column => sources.FindIndex(source => IsSameColumn(source.Rule.Column, column)))];

		Dictionary<string, int> sourceIndexesByName = new(StringComparer.OrdinalIgnoreCase);

		for (int index = 0; index < sources.Count; ++index)
		{
			_ = sourceIndexesByName.TryAdd(sources[index].Rule.Column.Name, index);
		}

		return new RowSetBlueprint
		{
			Plan                   = rowSet,
			Sources                = sources,
			KeySourceIndexes       = keySourceIndexes,
			GeneratedKeyGroupCount = generatedGroups.Count,
			ExistingKeyGroupCount  = existingGroupCount,
			EvaluationOrder        = SortByReferences(valueRules, out _),
			SourceIndexesByName    = sourceIndexesByName,
			ExistingKeyPools       = rowSetPools,
			LookupPools            = lookupPools,
			Update                 = rowSet.IsUpdate ? CreateUpdate(table, rowSet, sources, context) : null
		};
	}

	private RowSetUpdate CreateUpdate(TableModel table, RowSetPlan rowSet, List<ValueSource> sources, BuildContext context)
	{
		string? scopeCondition = null;

		if (rowSet.UpdateScope != RowScope.Any)
		{
			List<string> keyColumns = [.. table.Columns.Where(column => column.IsPrimaryKey).Select(column => column.Name)];

			scopeCondition = context.Snapshots.Get(table, keyColumns).BuildScopeCondition(rowSet.UpdateScope, RowSetUpdate.TARGET_ALIAS);
		}

		++context.UpdateCount;

		return new RowSetUpdate(context.UpdateCount, table, rowSet, [.. sources.Select(source => source.Rule.Column)], scopeCondition, _converter);
	}

	private Dictionary<ColumnRule, ValueSource> CreateExistingKeySources(
		List<ColumnRule>                    valueRules,
		Dictionary<string, ExistingKeyPool> pools,
		List<ExistingKeyPool>               rowSetPools,
		bool                                isScript,
		out int                             groupCount
	)
	{
		Dictionary<string, List<ColumnRule>> groups = new(StringComparer.OrdinalIgnoreCase);

		foreach (ColumnRule rule in valueRules)
		{
			ForeignKeyModel? reference = rule.Reference;

			if (rule.GenerationMode != ValueGenerationMode.ExistingForeignKey || reference is null)
			{
				continue;
			}

			if (!groups.TryGetValue(reference.Name, out List<ColumnRule>? groupRules))
			{
				groupRules             = [];
				groups[reference.Name] = groupRules;
			}

			groupRules.Add(rule);
		}

		Dictionary<ColumnRule, ValueSource> sources    = [];
		int                                 groupIndex = 0;

		foreach (List<ColumnRule> groupRules in groups.Values)
		{
			ForeignKeyModel    reference         = groupRules[0].Reference!;
			List<string>       referencedColumns = [.. groupRules.Select(rule => rule.Reference!.ReferencedColumn)];
			List<ColumnModel>  targetColumns     = [.. groupRules.Select(rule => rule.Column)];
			string             signature         = ExistingKeyPool.CreateSignature(reference, referencedColumns, targetColumns, _converter);

			if (!pools.TryGetValue(signature, out ExistingKeyPool? pool))
			{
				pool = new ExistingKeyPool(pools.Count + 1, reference, referencedColumns, targetColumns, isScript);
				pools.Add(signature, pool);
			}

			if (!rowSetPools.Contains(pool))
			{
				rowSetPools.Add(pool);
			}

			for (int index = 0; index < groupRules.Count; ++index)
			{
				sources[groupRules[index]] = new ValueSource
				{
					Rule        = groupRules[index],
					Kind        = ValueSourceKind.ExistingKey,
					Pool        = pool,
					ColumnIndex = index,
					GroupIndex  = groupIndex
				};
			}

			++groupIndex;
		}

		groupCount = groupIndex;

		return sources;
	}

	private static bool IsProducedByDatabase(RowSetPlan rowSet, ColumnModel column)
	{
		ColumnRule? rule = rowSet.Rules.FirstOrDefault(item => IsSameColumn(item.Column, column));

		return rule is null || rule.GenerationMode == ValueGenerationMode.DatabaseGenerated;
	}

	private static bool IsSameColumn(ColumnModel first, ColumnModel second)
		=> ReferenceEquals(first, second) || string.Equals(first.Name, second.Name, StringComparison.OrdinalIgnoreCase);

	/// <summary>
	/// What the row sets of one request share while they are built.
	/// </summary>
	private sealed class BuildContext
	{
		public required Dictionary<string, GeneratedKeyTable> KeyTables     { get; init; }
		public Dictionary<string, ExistingKeyPool>            Pools         { get; } = new(StringComparer.OrdinalIgnoreCase);
		public required RowSnapshotSet                        Snapshots     { get; init; }
		public required LookupPoolFactory                     LookupFactory { get; init; }
		public required bool                                  IsScript      { get; init; }
		public int                                            UpdateCount   { get; set; }
	}
}