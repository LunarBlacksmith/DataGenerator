namespace DataGenerator.Models;

/// <summary>
/// A named batch of rows for one table, generated with its own set of column rules.
/// </summary>
public sealed class RowSetPlan
{
	public required string                    Name     { get; init; }
	public required int                       RowCount { get; init; }
	public required IReadOnlyList<ColumnRule> Rules    { get; init; }
}