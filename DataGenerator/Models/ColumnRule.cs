namespace DataGenerator.Models;

/// <summary>
///	Immutable snapshot of how one column is generated for one row set.
/// </summary>
public sealed class ColumnRule
{
	#region PROPERTIES
	public required ColumnModel         Column            { get; init; }
	public required ValueGenerationMode GenerationMode    { get; init; }
	public string                       FixedValue        { get; init; }
	public decimal                      SequenceStart     { get; init; }
	public decimal                      SequenceStep      { get; init; }
	public string                       RegexPattern      { get; init; }
	public string                       PatternExpression { get; init; }

	/// <summary>
	///	The column of the same row whose value is copied in <see cref="ValueGenerationMode.CopyColumn"/> mode.
	/// </summary>
	public string                       SourceColumnName  { get; init; }

	/// <summary>
	///	The "Value from table" expression as typed, e.g. dbo.Shirt.ShirtID UNIQUE.
	/// </summary>
	public string                       LookupExpression  { get; init; }

	/// <summary>
	///	The resolved <see cref="LookupExpression"/> in <see cref="ValueGenerationMode.TableLookup"/> mode; null when it
	///	could not be resolved.
	/// </summary>
	public ColumnLookup?                Lookup            { get; init; }
	public ForeignKeyModel?             Reference         { get; init; }
	#endregion PROPERTIES

	/// <summary>
	///	Creates a new <see cref="ColumnRule"/> and sets the default values of its fields and properties.
	/// </summary>
	public ColumnRule()
	{
		FixedValue        = string.Empty;
		SequenceStart     = 1;
		SequenceStep      = 1;
		RegexPattern      = string.Empty;
		PatternExpression = string.Empty;
		SourceColumnName  = string.Empty;
		LookupExpression  = string.Empty;
		Lookup            = null;
		Reference         = null;
	}
}