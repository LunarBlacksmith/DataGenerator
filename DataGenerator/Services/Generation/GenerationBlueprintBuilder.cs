using DataGenerator.Models;

namespace DataGenerator.Services.Generation;

/// <summary>
/// Validates a <see cref="GenerationRequest"/> and prepares everything the script writer and direct inserter need.
/// </summary>
internal sealed class GenerationBlueprintBuilder
{
	private readonly ISqlValueConverter    _converter;
	private readonly IColumnValueGenerator _valueGenerator;

	public GenerationBlueprintBuilder(ISqlValueConverter converter, IColumnValueGenerator valueGenerator)
	{
		_converter      = converter ?? throw new ArgumentNullException(nameof(converter));
		_valueGenerator = valueGenerator ?? throw new ArgumentNullException(nameof(valueGenerator));
	}

	public GenerationBlueprint Build(GenerationRequest request)
	{
		ArgumentNullException.ThrowIfNull(request);

		bool                                    isScript   = request.Mode == GenerationMode.SqlFile;
		Dictionary<string, TableGenerationPlan> plansByKey = IndexPlans(request.Plans);

		foreach (TableGenerationPlan plan in request.Plans)
		{
			ValidatePlan(plan, plansByKey);
		}

		IReadOnlyList<TableGenerationPlan>    orderedPlans = TableDependencySorter.SortForInsertion(request.Plans);
		Dictionary<string, GeneratedKeyTable> keyTables    = CreateKeyTables(request.Plans, isScript);
		Dictionary<string, ExistingKeyPool>   pools        = new Dictionary<string, ExistingKeyPool>(StringComparer.OrdinalIgnoreCase);
		List<TableBlueprint>                  tables       = [];

		foreach (TableGenerationPlan plan in orderedPlans)
		{
			List<RowSetBlueprint> rowSets    = [];
			List<ExistingKeyPool> tablePools = [];

			_ = keyTables.TryGetValue(plan.Table.Key, out GeneratedKeyTable? keys);

			foreach (RowSetPlan rowSet in plan.RowSets)
			{
				rowSets.Add(BuildRowSet(rowSet, keys, keyTables, pools, tablePools, isScript));
			}

			tables.Add(new TableBlueprint
			{
				Table            = plan.Table,
				RowSets          = rowSets,
				Keys             = keys,
				ExistingKeyPools = tablePools
			});
		}

		return new GenerationBlueprint
		{
			Tables             = tables,
			ExistingKeyPools   = [.. pools.Values],
			TablesToClear      = TableDependencySorter.SortForDeletion(request.TablesToClear),
			ResetIdentitySeeds = request.ResetIdentitySeeds,
			PostGeneration     = request.PostGeneration is { Statements.Count: > 0 } ? request.PostGeneration : null
		};
	}

	public static string DescribeLocation(TableModel table, RowSetPlan? rowSet = null, long? rowIndex = null, ColumnModel? column = null)
		=> GenerationLocation.Describe(table, rowSet?.Name, rowIndex, column?.Name);

	private static Dictionary<string, TableGenerationPlan> IndexPlans(IReadOnlyList<TableGenerationPlan> plans)
	{
		Dictionary<string, TableGenerationPlan> plansByKey = new Dictionary<string, TableGenerationPlan>(StringComparer.OrdinalIgnoreCase);

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

	private void ValidatePlan(TableGenerationPlan plan, Dictionary<string, TableGenerationPlan> plansByKey)
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

			HashSet<string> columnNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

			foreach (ColumnRule rule in rowSet.Rules)
			{
				if (!columnNames.Add(rule.Column.Name))
				{
					throw new DataGenerationException(
						"The column has more than one rule in the same row set.",
						DescribeLocation(plan.Table, rowSet, null, rule.Column)
					);
				}

				string? problem = FindRuleProblem(rule, plansByKey) ?? FindReferenceProblem(rule, rowSet.Rules);

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
		Dictionary<string, int> indexesByName = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

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

		Queue<int> ready = new Queue<int>(Enumerable.Range(0, rules.Count).Where(index => remainingReferences[index] == 0));
		List<int>  order = new List<int>(rules.Count);

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

	private string? FindRuleProblem(ColumnRule rule, Dictionary<string, TableGenerationPlan> plansByKey)
	{
		ColumnModel column         = rule.Column;
		bool        isDatabaseOnly = column.IsIdentity || column.IsComputed || _converter.GetCategory(column) == SqlTypeCategory.RowVersion;
		bool        isDatabaseRule = rule.GenerationMode == ValueGenerationMode.DatabaseGenerated;

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

		bool referencedColumnExists = referencedPlan.Table.Columns.Any(
			item => string.Equals(item.Name, reference.ReferencedColumn, StringComparison.OrdinalIgnoreCase)
		);

		return referencedColumnExists
			? null
			: $"The referenced column [{reference.ReferencedColumn}] does not exist in {referencedPlan.Table.DisplayName}.";
	}

	private static Dictionary<string, GeneratedKeyTable> CreateKeyTables(IReadOnlyList<TableGenerationPlan> plans, bool isScript)
	{
		Dictionary<string, List<string>> columnNamesByTable = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

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

		Dictionary<string, GeneratedKeyTable> keyTables = new Dictionary<string, GeneratedKeyTable>(StringComparer.OrdinalIgnoreCase);
		int                                   number    = 0;

		foreach (TableGenerationPlan plan in plans)
		{
			if (!columnNamesByTable.TryGetValue(plan.Table.Key, out List<string>? columnNames))
			{
				continue;
			}

			List<ColumnModel> columns = [.. plan.Table.Columns.Where(column => columnNames.Contains(column.Name, StringComparer.OrdinalIgnoreCase))];

			++number;

			bool    needsOutput        = isScript && plan.RowSets.Any(rowSet => columns.Any(column => IsProducedByDatabase(rowSet, column)));
			string? outputVariableName = needsOutput ? $"@dg_keys_{number}" : null;

			keyTables[plan.Table.Key] = new GeneratedKeyTable(plan.Table, columns, outputVariableName);
		}

		return keyTables;
	}

	private RowSetBlueprint BuildRowSet(
		RowSetPlan                            rowSet,
		GeneratedKeyTable?                    keys,
		Dictionary<string, GeneratedKeyTable> keyTables,
		Dictionary<string, ExistingKeyPool>   pools,
		List<ExistingKeyPool>                 tablePools,
		bool                                  isScript
	)
	{
		List<ColumnRule> insertRules = [.. rowSet.Rules.Where(rule => rule.GenerationMode != ValueGenerationMode.DatabaseGenerated)];

		Dictionary<ColumnRule, ValueSource> existingSources = CreateExistingKeySources(insertRules, pools, tablePools, isScript, out int existingGroupCount);
		Dictionary<string, int>             generatedGroups = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
		List<ValueSource>                   sources         = [];

		foreach (ColumnRule rule in insertRules)
		{
			if (existingSources.TryGetValue(rule, out ValueSource? existingSource))
			{
				sources.Add(existingSource);
				continue;
			}

			ForeignKeyModel? reference = rule.Reference;

			if (rule.GenerationMode != ValueGenerationMode.GeneratedForeignKey || reference is null)
			{
				sources.Add(new ValueSource { Rule = rule, Kind = ValueSourceKind.Rule });
				continue;
			}

			GeneratedKeyTable parentKeys = keyTables[reference.ReferencedTableKey];

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

		List<int> keySourceIndexes = keys is null
			? []
			: [.. keys.Columns.Select(column => sources.FindIndex(source => IsSameColumn(source.Rule.Column, column)))];

		Dictionary<string, int> sourceIndexesByName = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

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
			EvaluationOrder        = SortByReferences(insertRules, out _),
			SourceIndexesByName    = sourceIndexesByName
		};
	}

	private Dictionary<ColumnRule, ValueSource> CreateExistingKeySources(
		List<ColumnRule>                    insertRules,
		Dictionary<string, ExistingKeyPool> pools,
		List<ExistingKeyPool>               tablePools,
		bool                                isScript,
		out int                             groupCount
	)
	{
		Dictionary<string, List<ColumnRule>> groups = new Dictionary<string, List<ColumnRule>>(StringComparer.OrdinalIgnoreCase);

		foreach (ColumnRule rule in insertRules)
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

		Dictionary<ColumnRule, ValueSource> sources    = new Dictionary<ColumnRule, ValueSource>();
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

			if (!tablePools.Contains(pool))
			{
				tablePools.Add(pool);
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
}