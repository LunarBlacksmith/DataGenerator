namespace DataGenerator.Models;

/// <summary>
///	User-facing description of a failure: what went wrong, where it happened and the full technical details.
/// </summary>
public sealed class ErrorReport
{
	#region PROPERTIES
	#region PUBLIC
	public required string Summary  { get; init; }
	public required string Location { get; init; }
	public required string Details  { get; init; }
	#endregion PUBLIC
	#endregion PROPERTIES

	#region CONSTRUCTORS
	#region PUBLIC
	/// <summary>
	///	Creates a new <see cref="ErrorReport"/>.
	/// </summary>
	public ErrorReport()
	{
	}
	#endregion PUBLIC
	#endregion CONSTRUCTORS
}