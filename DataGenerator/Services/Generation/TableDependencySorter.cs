using DataGenerator.Models;

namespace DataGenerator.Services.Generation;

/// <summary>
///	Orders tables so referenced tables are handled before the tables that depend on them.
/// </summary>
internal static class TableDependencySorter
{
	/// <summary>
	///	"Generated key" references are hard dependencies (a cycle is an error); "Existing key" references between
	///	generated tables are preferences that are dropped when they would form a cycle.
	/// </summary>
	/// <param name="plans">
	///	The table plans to order for insertion.
	/// </param>
	/// <returns>
	///	The plans ordered with referenced tables before dependent tables.
	/// </returns>
	/// <exception cref="InvalidOperationException">
	///	Thrown when required generated-key dependencies form a cycle.
	/// </exception>
	public static IReadOnlyList<TableGenerationPlan> SortForInsertion(IReadOnlyList<TableGenerationPlan> plans)
	{
		Dictionary<string, TableGenerationPlan> plansByKey   = new(StringComparer.OrdinalIgnoreCase);
		Dictionary<string, List<Dependency>>    dependencies = new(StringComparer.OrdinalIgnoreCase);

		foreach (TableGenerationPlan plan in plans)
		{
			plansByKey[plan.Table.Key] = plan;
		}

		foreach (TableGenerationPlan plan in plans)
		{
			List<Dependency> tableDependencies = [];

			foreach (ColumnRule rule in plan.RowSets.SelectMany(rowSet => rowSet.Rules))
			{
				if (rule.Reference is null || !plansByKey.ContainsKey(rule.Reference.ReferencedTableKey))
				{
					continue;
				}

				if (rule.GenerationMode is ValueGenerationMode.GeneratedForeignKey or ValueGenerationMode.ExistingForeignKey)
				{
					tableDependencies.Add(new Dependency(
						rule.Reference.ReferencedTableKey,
						rule.GenerationMode == ValueGenerationMode.GeneratedForeignKey,
						rule.Column.Name
					));
				}
			}

			dependencies[plan.Table.Key] = tableDependencies;
		}

		List<string> orderedKeys = Sort(plans.Select(plan => plan.Table.Key), dependencies, plansByKey);

		return [.. orderedKeys.Select(key => plansByKey[key])];
	}

	/// <summary>
	///	Orders tables for DELETE statements: tables that reference others (through declared foreign keys) come first.
	/// </summary>
	/// <param name="tables">
	///	The tables to order for deletion.
	/// </param>
	/// <returns>
	///	The tables ordered so dependent tables are deleted before referenced tables.
	/// </returns>
	public static IReadOnlyList<TableModel> SortForDeletion(IReadOnlyList<TableModel> tables)
	{
		Dictionary<string, TableModel>       tablesByKey  = new(StringComparer.OrdinalIgnoreCase);
		Dictionary<string, List<Dependency>> dependencies = new(StringComparer.OrdinalIgnoreCase);

		foreach (TableModel table in tables)
		{
			tablesByKey[table.Key] = table;
		}

		foreach (TableModel table in tablesByKey.Values)
		{
			dependencies[table.Key] =
			[
				.. table.ForeignKeys
					.Where(foreignKey => !foreignKey.IsInferred && tablesByKey.ContainsKey(foreignKey.ReferencedTableKey))
					.Select(foreignKey => new Dependency(foreignKey.ReferencedTableKey, false, foreignKey.ParentColumn))
			];
		}

		List<string> parentsFirst = Sort(tablesByKey.Keys, dependencies, null);

		parentsFirst.Reverse();

		return [.. parentsFirst.Select(key => tablesByKey[key])];
	}

	/// <summary>
	///	Topologically sorts keys using the supplied dependency graph.
	/// </summary>
	/// <param name="keys">
	///	The keys to sort.
	/// </param>
	/// <param name="dependencies">
	///	Dependencies keyed by the item that depends on them.
	/// </param>
	/// <param name="plansByKey">
	///	Optional table plans used to produce friendly cycle messages.
	/// </param>
	/// <returns>
	///	The sorted keys with dependencies before dependants.
	/// </returns>
	/// <exception cref="InvalidOperationException">
	///	Thrown when required dependencies form a cycle.
	/// </exception>
	private static List<string> Sort(
		IEnumerable<string>                        keys,
		Dictionary<string, List<Dependency>>       dependencies,
		Dictionary<string, TableGenerationPlan>?   plansByKey
	)
	{
		HashSet<string> visited = new(StringComparer.OrdinalIgnoreCase);
		List<string>    path    = [];
		List<string>    result  = [];

		foreach (string key in keys)
		{
			Visit(key, dependencies, plansByKey, visited, path, result);
		}

		return result;
	}

	/// <summary>
	///	Visits one key and appends it after its dependencies have been visited.
	/// </summary>
	/// <param name="key">
	///	The key to visit.
	/// </param>
	/// <param name="dependencies">
	///	Dependencies keyed by item.
	/// </param>
	/// <param name="plansByKey">
	///	Optional table plans used to produce friendly cycle messages.
	/// </param>
	/// <param name="visited">
	///	The keys already added to the result.
	/// </param>
	/// <param name="path">
	///	The current recursion path, used to detect cycles.
	/// </param>
	/// <param name="result">
	///	The sorted result being built.
	/// </param>
	/// <exception cref="InvalidOperationException">
	///	Thrown when required dependencies form a cycle.
	/// </exception>
	private static void Visit(
		string                                   key,
		Dictionary<string, List<Dependency>>     dependencies,
		Dictionary<string, TableGenerationPlan>? plansByKey,
		HashSet<string>                          visited,
		List<string>                             path,
		List<string>                             result
	)
	{
		if (visited.Contains(key))
		{
			return;
		}

		path.Add(key);

		foreach (Dependency dependency in dependencies[key])
		{
			int pathIndex = path.FindIndex(item => string.Equals(item, dependency.TargetKey, StringComparison.OrdinalIgnoreCase));

			if (pathIndex >= 0)
			{
				if (dependency.IsRequired)
				{
					throw new InvalidOperationException(DescribeCycle(path, pathIndex, dependency, plansByKey));
				}

				continue;
			}

			Visit(dependency.TargetKey, dependencies, plansByKey, visited, path, result);
		}

		path.RemoveAt(path.Count - 1);
		_ = visited.Add(key);
		result.Add(key);
	}

	/// <summary>
	///	Builds the validation message for a required generated-key dependency cycle.
	/// </summary>
	/// <param name="path">
	///	The current dependency path.
	/// </param>
	/// <param name="pathIndex">
	///	The index in the path where the cycle begins.
	/// </param>
	/// <param name="dependency">
	///	The dependency that closes the cycle.
	/// </param>
	/// <param name="plansByKey">
	///	Optional table plans used to display table names.
	/// </param>
	/// <returns>
	///	A user-facing description of the cycle.
	/// </returns>
	private static string DescribeCycle(
		List<string>                             path,
		int                                      pathIndex,
		Dependency                               dependency,
		Dictionary<string, TableGenerationPlan>? plansByKey
	)
	{
		string GetName(string key)
			=> plansByKey is not null && plansByKey.TryGetValue(key, out TableGenerationPlan? plan) ? plan.Table.DisplayName : key;

		string current = GetName(path[^1]);

		if (pathIndex == path.Count - 1)
		{
			return $"Table {current} references itself through column [{dependency.ColumnName}], which uses 'Generated key'. "
				+ "A row cannot reference rows generated in the same run of its own table; use 'Existing key', 'NULL' or another mode for that column.";
		}

		IEnumerable<string> cycle = path.Skip(pathIndex).Append(dependency.TargetKey).Select(GetName);

		return $"The 'Generated key' rules form a cycle: {string.Join(" → ", cycle)} (column [{dependency.ColumnName}] of {current}). "
			+ "Change at least one of these columns to 'Existing key', 'NULL' or another mode.";
	}

	private sealed record Dependency(string TargetKey, bool IsRequired, string ColumnName);
}