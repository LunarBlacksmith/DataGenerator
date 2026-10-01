using DataGenerator.Models;

namespace DataGenerator.Services.Generation;

/// <summary>
/// Validates a <see cref="GenerationRequest"/> and prepares everything the script writer and direct inserter need.
/// </summary>
internal sealed class GenerationBlueprintBuilder
{
	private readonly ISqlValueConverter _converter;

	public GenerationBlueprintBuilder(ISqlValueConverter converter)
	{
		_converter = converter ?? throw new ArgumentNullException(nameof(converter));
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
			ResetIdentitySeeds = request.ResetIdentitySeeds
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

				string? problem = FindRuleProblem(rule, plansByKey);

				if (problem is not null)
				{
					throw new DataGenerationException(problem, DescribeLocation(plan.Table, rowSet, null, rule.Column));
				}
			}
		}
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

		return new RowSetBlueprint
		{
			Plan                   = rowSet,
			Sources                = sources,
			KeySourceIndexes       = keySourceIndexes,
			GeneratedKeyGroupCount = generatedGroups.Count,
			ExistingKeyGroupCount  = existingGroupCount
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