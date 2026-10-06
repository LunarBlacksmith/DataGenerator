namespace DataGenerator.Models;

/// <summary>
/// The generation mode and settings of a column rule, saved under a name so they can be reused in other row sets,
/// tables and sessions, or shared with colleagues through an exported file.
/// </summary>
public sealed class SavedColumnSetting
{
	public const string DEFAULT_SEQUENCE_VALUE = "1";

	public string              Name              { get; set; } = string.Empty;
	public ValueGenerationMode GenerationMode    { get; set; }
	public string              FixedValue        { get; set; } = string.Empty;
	public string              SequenceStart     { get; set; } = DEFAULT_SEQUENCE_VALUE;
	public string              SequenceStep      { get; set; } = DEFAULT_SEQUENCE_VALUE;
	public string              RegexPattern      { get; set; } = string.Empty;
	public string              PatternExpression { get; set; } = string.Empty;
	public string              SourceColumnName  { get; set; } = string.Empty;
	public string              LookupExpression  { get; set; } = string.Empty;

	/// <summary>
	/// The schema.table of the column the setting was saved from, or <see langword="null"/> when it applies to a column
	/// name in any table.
	/// </summary>
	public string? TableName { get; set; }

	/// <summary>
	/// The column the setting was saved from.
	/// </summary>
	public string? ColumnName { get; set; }

	/// <summary>
	/// Whether new row sets use this setting for the matching column (see <see cref="TableName"/> and <see cref="ColumnName"/>).
	/// </summary>
	public bool ApplyAutomatically { get; set; }

	public bool AppliesToAnyTable => string.IsNullOrWhiteSpace(TableName);

	public bool Matches(string tableName, string columnName)
		=> !string.IsNullOrWhiteSpace(ColumnName)
			&& string.Equals(ColumnName, columnName, StringComparison.OrdinalIgnoreCase)
			&& (AppliesToAnyTable || string.Equals(TableName, tableName, StringComparison.OrdinalIgnoreCase));

	public bool HasSameTarget(SavedColumnSetting other)
	{
		ArgumentNullException.ThrowIfNull(other);

		return string.Equals(ColumnName, other.ColumnName, StringComparison.OrdinalIgnoreCase)
			&& AppliesToAnyTable == other.AppliesToAnyTable
			&& (AppliesToAnyTable || string.Equals(TableName, other.TableName, StringComparison.OrdinalIgnoreCase));
	}

	/// <summary>
	/// Whether both settings generate values in the same way. Only the settings of the generation mode are compared.
	/// </summary>
	public bool HasSameValues(SavedColumnSetting other)
	{
		ArgumentNullException.ThrowIfNull(other);

		return GenerationMode == other.GenerationMode
			&& GenerationMode switch
			{
				ValueGenerationMode.Fixed       => string.Equals(FixedValue, other.FixedValue, StringComparison.Ordinal),
				ValueGenerationMode.Sequence    => string.Equals(SequenceStart.Trim(), other.SequenceStart.Trim(), StringComparison.Ordinal)
					&& string.Equals(SequenceStep.Trim(), other.SequenceStep.Trim(), StringComparison.Ordinal),
				ValueGenerationMode.Regex       => string.Equals(RegexPattern, other.RegexPattern, StringComparison.Ordinal),
				ValueGenerationMode.Pattern     => string.Equals(PatternExpression, other.PatternExpression, StringComparison.Ordinal),
				ValueGenerationMode.CopyColumn  => string.Equals(SourceColumnName.Trim(), other.SourceColumnName.Trim(), StringComparison.OrdinalIgnoreCase),
				ValueGenerationMode.TableLookup => string.Equals(LookupExpression.Trim(), other.LookupExpression.Trim(), StringComparison.OrdinalIgnoreCase),
				_                               => true
			};
	}

	public SavedColumnSetting Clone() => (SavedColumnSetting)MemberwiseClone();
}