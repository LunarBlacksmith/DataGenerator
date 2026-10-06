namespace DataGenerator.Models;

/// <summary>
/// Where a "Value from table" rule takes its values from: a column of a table (possibly the same table), optionally
/// limited to generated or existing rows and to values that match a filter.
/// </summary>
public sealed class ColumnLookup
{
	public required TableModel       SourceTable  { get; init; }
	public required ColumnModel      SourceColumn { get; init; }

	/// <summary>
	/// Every row gets a different value, which is also not yet used in the target column.
	/// </summary>
	public bool                      IsUnique     { get; init; }
	public RowScope                  Scope        { get; init; }
	public LookupFilterKind          FilterKind   { get; init; }

	/// <summary>
	/// The pattern, regular expression or SQL condition after WHERE; empty when <see cref="FilterKind"/> is None.
	/// </summary>
	public string                    FilterText   { get; init; } = string.Empty;

	/// <summary>
	/// schema.table.column, e.g. dbo.Shirt.ShirtID.
	/// </summary>
	public string SourceDisplayName => $"{SourceTable.DisplayName}.{SourceColumn.Name}";
}
