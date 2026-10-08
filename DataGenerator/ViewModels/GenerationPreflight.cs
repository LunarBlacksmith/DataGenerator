using DataGenerator.Models;
using DataGenerator.Services;

namespace DataGenerator.ViewModels;

/// <summary>
///	Checks the included tables before generating: invalid rules and keys taken from tables that are not included.
/// </summary>
public static class GenerationPreflight
{
	#region CONSTRUCTOR
	/// <summary>
	///	Initialises the static state of <see cref="GenerationPreflight"/>.
	/// </summary>
	static GenerationPreflight()
	{
	}
	#endregion CONSTRUCTOR

	#region METHODS
	/// <summary>
	///	Collects validation problems from the included tables' row sets and column rules.
	/// </summary>
	/// <param name="tables">
	///	The tables that will be generated or updated.
	/// </param>
	/// <returns>
	///	The rule problems that must be fixed before generation; empty when all rules are valid.
	/// </returns>
	/// <exception cref="ArgumentNullException">
	///	Thrown when <paramref name="tables"/> is <see langword="null"/>.
	/// </exception>
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

				foreach (ColumnRuleViewModel rule in
					rowSet
						.ColumnRules
						.Where(rule => rule.ValidationError is not null)
				)
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
	///	Finds "Generated key" rules whose referenced table is not included, following the references of those tables too,
	///	because including a table brings its own default "Generated key" rules with it.
	/// </summary>
	/// <param name="explorer">
	///	The explorer used to find tables by referenced key.
	/// </param>
	/// <param name="includedTables">
	///	The tables already planned for generation.
	/// </param>
	/// <returns>
	///	The missing referenced tables, including ones discovered through the newly required tables.
	/// </returns>
	/// <exception cref="ArgumentNullException">
	///	Thrown when <paramref name="explorer"/> or <paramref name="includedTables"/> is <see langword="null"/>.
	/// </exception>
	public static IReadOnlyList<MissingReference> FindMissingReferences(
		DatabaseExplorerViewModel         explorer,
		IReadOnlyList<TableNodeViewModel> includedTables
	)
	{
		ArgumentNullException.ThrowIfNull(explorer);
		ArgumentNullException.ThrowIfNull(includedTables);

		HashSet<TableNodeViewModel> plannedTables = [.. includedTables];
		Queue<TableNodeViewModel>   pendingTables = new(includedTables);
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
	#endregion METHODS
}

/// <summary>
///	A rule that must be fixed before generating, with where it is.
/// </summary>
public sealed record RuleProblem
{
	#region PROPERTIES
	public TableNodeViewModel Table    { get; init; }
	public RowSetViewModel    RowSet   { get; init; }
	public string             Location { get; init; }
	public string             Message  { get; init; }
	#endregion PROPERTIES

	/// <summary>
	///	Creates a new <see cref="RuleProblem"/> from the supplied values.
	/// </summary>
	/// <param name="table">
	///	The value of <see cref="Table"/>.
	/// </param>
	/// <param name="rowSet">
	///	The value of <see cref="RowSet"/>.
	/// </param>
	/// <param name="location">
	///	The value of <see cref="Location"/>.
	/// </param>
	/// <param name="message">
	///	The value of <see cref="Message"/>.
	/// </param>
	public RuleProblem(TableNodeViewModel table, RowSetViewModel rowSet, string location, string message)
	{
		Table    = table;
		RowSet   = rowSet;
		Location = location;
		Message  = message;
	}
}

/// <summary>
///	A "Generated key" rule of <see cref="ReferencingTable"/> that needs rows generated for <see cref="ReferencedTable"/>.
/// </summary>
public sealed record MissingReference
{
	#region PROPERTIES
	public TableNodeViewModel  ReferencingTable { get; init; }
	public ColumnRuleViewModel Rule             { get; init; }
	public TableNodeViewModel  ReferencedTable  { get; init; }
	#endregion PROPERTIES

	/// <summary>
	///	Creates a new <see cref="MissingReference"/> from the supplied values.
	/// </summary>
	/// <param name="referencingTable">
	///	The value of <see cref="ReferencingTable"/>.
	/// </param>
	/// <param name="rule">
	///	The value of <see cref="Rule"/>.
	/// </param>
	/// <param name="referencedTable">
	///	The value of <see cref="ReferencedTable"/>.
	/// </param>
	public MissingReference(
		TableNodeViewModel  referencingTable,
		ColumnRuleViewModel rule,
		TableNodeViewModel  referencedTable
	)
	{
		ReferencingTable = referencingTable;
		Rule             = rule;
		ReferencedTable  = referencedTable;
	}
}