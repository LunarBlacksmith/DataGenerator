using DataGenerator.Models;

namespace DataGenerator.Services.Generation;

internal enum ValueSourceKind
{
	Rule         = 0,
	GeneratedKey = 1,
	ExistingKey  = 2,
	Lookup       = 3
}

/// <summary>
/// Where the value of one inserted column comes from.
/// </summary>
internal sealed class ValueSource
{
	public required ColumnRule         Rule        { get; init; }
	public required ValueSourceKind    Kind        { get; init; }
	public GeneratedKeyTable?          KeyTable    { get; init; }
	public ExistingKeyPool?            Pool        { get; init; }
	public LookupPool?                 Lookup      { get; init; }

	/// <summary>
	/// Column index within <see cref="KeyTable"/> or <see cref="Pool"/>.
	/// </summary>
	public int                         ColumnIndex { get; init; }

	/// <summary>
	/// Columns of the same (composite) foreign key share a group, so they use the same referenced row.
	/// </summary>
	public int                         GroupIndex  { get; init; }
}

internal sealed class RowSetBlueprint
{
	public required RowSetPlan                 Plan                   { get; init; }

	/// <summary>
	/// One entry per inserted (or, in update sets, changed) column, in column order. Empty when the row uses DEFAULT VALUES.
	/// </summary>
	public required IReadOnlyList<ValueSource> Sources                { get; init; }

	/// <summary>
	/// For every key column captured for dependent tables: the index into <see cref="Sources"/>,
	/// or -1 when SQL Server produces the value.
	/// </summary>
	public required IReadOnlyList<int>         KeySourceIndexes       { get; init; }

	public required int                        GeneratedKeyGroupCount { get; init; }
	public required int                        ExistingKeyGroupCount  { get; init; }

	/// <summary>
	/// Indexes into <see cref="Sources"/> in the order the values are generated, so that columns which use the value of
	/// another column (Copy of column, COL(...)) come after that column.
	/// </summary>
	public required IReadOnlyList<int>         EvaluationOrder        { get; init; }

	/// <summary>
	/// The index into <see cref="Sources"/> of every inserted column, by column name.
	/// </summary>
	public required IReadOnlyDictionary<string, int> SourceIndexesByName { get; init; }

	/// <summary>
	/// "Existing key" samples used by this row set; each is loaded once, before the first row set that uses it.
	/// </summary>
	public required IReadOnlyList<ExistingKeyPool> ExistingKeyPools    { get; init; }

	/// <summary>
	/// "Value from table" values, loaded right before the rows of this row set are generated.
	/// </summary>
	public required IReadOnlyList<LookupPool>      LookupPools         { get; init; }

	/// <summary>
	/// The staging table and UPDATE statement of an update set; null for insert sets.
	/// </summary>
	public RowSetUpdate?                           Update              { get; init; }

	public bool IsUpdate => Update is not null;
}

/// <summary>
/// One row set of one table, in the order the row sets run.
/// </summary>
internal sealed class GenerationOperation
{
	public required TableBlueprint  Table  { get; init; }
	public required RowSetBlueprint RowSet { get; init; }
}

internal sealed class TableBlueprint
{
	public required TableModel                     Table            { get; init; }
	public required IReadOnlyList<RowSetBlueprint> RowSets          { get; init; }

	/// <summary>
	/// Captured key values for dependent tables; null when no other table uses "Generated key" rules against this one.
	/// </summary>
	public GeneratedKeyTable?                      Keys             { get; init; }

	/// <summary>
	/// The number of inserted rows; rows changed by update sets are counted by <see cref="UpdatedRowCount"/>.
	/// </summary>
	public long TotalRowCount   => RowSets.Where(rowSet => !rowSet.IsUpdate).Sum(rowSet => (long)rowSet.Plan.RowCount);

	public long UpdatedRowCount => RowSets.Where(rowSet => rowSet.IsUpdate).Sum(rowSet => (long)rowSet.Plan.RowCount);
}

internal sealed class GenerationBlueprint
{
	public required IReadOnlyList<TableBlueprint>      Tables             { get; init; }

	/// <summary>
	/// Every row set in the order it runs: step by step, and within a step the insert sets (referenced tables first)
	/// before the update sets.
	/// </summary>
	public required IReadOnlyList<GenerationOperation> Operations         { get; init; }
	public required IReadOnlyList<ExistingKeyPool>     ExistingKeyPools   { get; init; }

	/// <summary>
	/// Copies of tables taken before anything is inserted, for the "Generated rows" and "Existing rows" scopes.
	/// </summary>
	public required IReadOnlyList<RowSnapshot>         Snapshots          { get; init; }

	/// <summary>
	/// Tables to empty before inserting, ordered so that dependent tables are cleared first.
	/// </summary>
	public required IReadOnlyList<TableModel>          TablesToClear      { get; init; }

	public required bool                               ResetIdentitySeeds { get; init; }

	/// <summary>
	/// Stored procedures or SQL run after the inserts and before the commit; null when there are none.
	/// </summary>
	public PostGenerationScript?                       PostGeneration     { get; init; }

	public long TotalRowCount   => Tables.Sum(table => table.TotalRowCount);

	public long UpdatedRowCount => Tables.Sum(table => table.UpdatedRowCount);

	public int RowSetCount      => Operations.Count;

	public int StepCount        => Operations.Select(operation => operation.RowSet.Plan.Step).Distinct().Count();

	public IEnumerable<LookupPool>   LookupPools => Operations.SelectMany(operation => operation.RowSet.LookupPools);

	public IEnumerable<RowSetUpdate> Updates     => Operations.Select(operation => operation.RowSet.Update).OfType<RowSetUpdate>();
}