using DataGenerator.Models;
using DataGenerator.Services;

namespace DataGenerator.ViewModels;

/// <summary>
/// Checks the included tables before generating: invalid rules and keys taken from tables that are not included.
/// </summary>
public static class GenerationPreflight
{
	public static IReadOnlyList<RuleProblem> FindRuleProblems(IEnumerable<TableNodeViewModel> tables)
	{
		ArgumentNullException.ThrowIfNull(tables);

		List<RuleProblem> problems = [];

		foreach (TableNodeViewModel table in tables)
		{
			table.EnsureRowSets();

			foreach (RowSetViewModel rowSet in table.RowSets)
			{
				if (rowSet.HasErrors)
				{
					problems.Add(
						new RuleProblem(
							table,
							rowSet,
							GenerationLocation.Describe(table.Model, rowSet.Name, null, null),
							"The row set needs a name."
						)
					);
				}

				foreach (ColumnRuleViewModel rule in rowSet.ColumnRules.Where(rule => rule.ValidationError is not null))
				{
					problems.Add(
						new RuleProblem(
							table,
							rowSet,
							GenerationLocation.Describe(table.Model, rowSet.Name, null, rule.Name),
							rule.ValidationError!
						)
					);
				}
			}
		}

		return problems;
	}

	/// <summary>
	/// Finds "Generated key" rules whose referenced table is not included, following the references of those tables too,
	/// because including a table brings its own default "Generated key" rules with it.
	/// </summary>
	public static IReadOnlyList<MissingReference> FindMissingReferences(
		DatabaseExplorerViewModel         explorer,
		IReadOnlyList<TableNodeViewModel> includedTables
	)
	{
		ArgumentNullException.ThrowIfNull(explorer);
		ArgumentNullException.ThrowIfNull(includedTables);

		HashSet<TableNodeViewModel> plannedTables = [.. includedTables];
		Queue<TableNodeViewModel>   pendingTables = new Queue<TableNodeViewModel>(includedTables);
		List<MissingReference>      missing       = [];

		while (pendingTables.Count > 0)
		{
			TableNodeViewModel table = pendingTables.Dequeue();

			table.EnsureRowSets();

			foreach (RowSetViewModel rowSet in table.RowSets)
			{
				foreach (ColumnRuleViewModel rule in rowSet.ColumnRules)
				{
					if (rule.GenerationMode != ValueGenerationMode.GeneratedForeignKey || rule.Reference is null)
					{
						continue;
					}

					TableNodeViewModel? referencedTable = explorer.FindTable(rule.Reference.ReferencedTableKey);

					if (referencedTable is null || referencedTable.IsIncluded)
					{
						continue;
					}

					missing.Add(new MissingReference(table, rule, referencedTable));

					if (plannedTables.Add(referencedTable))
					{
						pendingTables.Enqueue(referencedTable);
					}
				}
			}
		}

		return missing;
	}
}

/// <summary>
/// A rule that must be fixed before generating, with where it is.
/// </summary>
public sealed record RuleProblem(TableNodeViewModel Table, RowSetViewModel RowSet, string Location, string Message);

/// <summary>
/// A "Generated key" rule of <paramref name="ReferencingTable"/> that needs rows generated for <paramref name="ReferencedTable"/>.
/// </summary>
public sealed record MissingReference(TableNodeViewModel ReferencingTable, ColumnRuleViewModel Rule, TableNodeViewModel ReferencedTable);