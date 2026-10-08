namespace DataGenerator.Models;

/// <summary>
///	A named batch of rows for one table, generated with its own set of column rules. Insert sets add new rows; update
///	sets change <see cref="RowCount"/> rows that are already in the table.
/// </summary>
public sealed class RowSetPlan
{
	#region FIELDS
	public const int FIRST_STEP = 1;
	public const int MAXIMUM_ROW_COUNT = 1_000_000;
	#endregion FIELDS

	#region PROPERTIES
	public required string                    Name            { get; init; }
	public required int                       RowCount        { get; init; }
	public required IReadOnlyList<ColumnRule> Rules           { get; init; }
	public RowSetAction                       Action          { get; init; }

	/// <summary>
	///	Row sets run step by step (1, 2, 3, …), all inside the same transaction.
	/// </summary>
	public int                                Step            { get; init; }

	/// <summary>
	///	Update sets only: which rows of the table may be changed.
	/// </summary>
	public RowScope                           UpdateScope     { get; init; }

	/// <summary>
	///	Update sets only: an optional SQL condition the changed rows must meet, e.g. t.[Size] = 'XL'.
	/// </summary>
	public string                             UpdateCondition { get; init; }

	/// <summary>
	///	Update sets only: whether the run fails when fewer than <see cref="RowCount"/> rows can be changed.
	/// </summary>
	public bool                               RequireAllRows  { get; init; }

	public bool IsUpdate => Action == RowSetAction.Update;
	#endregion PROPERTIES

	/// <summary>
	///	Creates a new <see cref="RowSetPlan"/> and sets the default values of its fields and properties.
	/// </summary>
	public RowSetPlan()
	{
		Action          = RowSetAction.Insert;
		Step            = FIRST_STEP;
		UpdateScope     = RowScope.Any;
		UpdateCondition = string.Empty;
		RequireAllRows  = true;
	}
}