using DataGenerator.Models;

namespace DataGenerator.Services.Generation;

internal enum ValueSourceKind
{
	Rule         = 0,
	GeneratedKey = 1,
	ExistingKey  = 2
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
	/// One entry per inserted column, in INSERT column order. Empty when the row uses DEFAULT VALUES.
	/// </summary>
	public required IReadOnlyList<ValueSource> Sources                { get; init; }

	/// <summary>
	/// For every key column captured for dependent tables: the index into <see cref="Sources"/>,
	/// or -1 when SQL Server produces the value.
	/// </summary>
	public required IReadOnlyList<int>         KeySourceIndexes       { get; init; }

	public required int                        GeneratedKeyGroupCount { get; init; }
	public required int                        ExistingKeyGroupCount  { get; init; }
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
	/// "Existing key" samples that must be loaded before the first row of this table is inserted.
	/// </summary>
	public required IReadOnlyList<ExistingKeyPool> ExistingKeyPools { get; init; }

	public long TotalRowCount => RowSets.Sum(rowSet => (long)rowSet.Plan.RowCount);
}

internal sealed class GenerationBlueprint
{
	public required IReadOnlyList<TableBlueprint>  Tables             { get; init; }
	public required IReadOnlyList<ExistingKeyPool> ExistingKeyPools   { get; init; }

	/// <summary>
	/// Tables to empty before inserting, ordered so that dependent tables are cleared first.
	/// </summary>
	public required IReadOnlyList<TableModel>      TablesToClear      { get; init; }

	public required bool                           ResetIdentitySeeds { get; init; }

	public long TotalRowCount => Tables.Sum(table => table.TotalRowCount);

	public int RowSetCount => Tables.Sum(table => table.RowSets.Count);
}