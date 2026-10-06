namespace DataGenerator.Models;

/// <summary>
/// User-facing description of a failure: what went wrong, where it happened and the full technical details.
/// </summary>
public sealed class ErrorReport
{
	public required string Summary  { get; init; }
	public required string Location { get; init; }
	public required string Details  { get; init; }
}