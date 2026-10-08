namespace DataGenerator.Models;

public sealed class TableGenerationPlan
{
	#region PROPERTIES
	public required TableModel                Table   { get; init; }
	public required IReadOnlyList<RowSetPlan> RowSets { get; init; }

	/// <summary>
	///	The number of rows inserted; update sets change existing rows and are not counted.
	/// </summary>
	public int TotalRowCount
		=>
			RowSets
				.Where(rowSet => !rowSet.IsUpdate)
				.Sum(rowSet => rowSet.RowCount);

	public int UpdatedRowCount
		=>
			RowSets
				.Where(rowSet => rowSet.IsUpdate)
				.Sum(rowSet => rowSet.RowCount);
	#endregion PROPERTIES

	/// <summary>
	///	Creates a new <see cref="TableGenerationPlan"/>.
	/// </summary>
	public TableGenerationPlan()
	{
	}
}