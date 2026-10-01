namespace DataGenerator.Models;

public sealed class TableGenerationPlan
{
	public required TableModel                Table   { get; init; }
	public required IReadOnlyList<RowSetPlan> RowSets { get; init; }

	public int TotalRowCount => RowSets.Sum(rowSet => rowSet.RowCount);
}