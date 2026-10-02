namespace DataGenerator.Models;

/// <summary>
/// Immutable snapshot of how one column is generated for one row set.
/// </summary>
public sealed class ColumnRule
{
	public required ColumnModel         Column            { get; init; }
	public required ValueGenerationMode GenerationMode    { get; init; }
	public string                       FixedValue        { get; init; } = string.Empty;
	public decimal                      SequenceStart     { get; init; } = 1;
	public decimal                      SequenceStep      { get; init; } = 1;
	public string                       RegexPattern      { get; init; } = string.Empty;
	public string                       PatternExpression { get; init; } = string.Empty;

	/// <summary>
	/// The column of the same row whose value is copied in <see cref="ValueGenerationMode.CopyColumn"/> mode.
	/// </summary>
	public string                       SourceColumnName  { get; init; } = string.Empty;
	public ForeignKeyModel?             Reference         { get; init; }
}