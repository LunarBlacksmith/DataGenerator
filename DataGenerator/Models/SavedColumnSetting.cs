namespace DataGenerator.Models;

/// <summary>
///	The generation mode and settings of a column rule, saved under a name so they can be reused in other row sets,
///	tables and sessions, or shared with colleagues through an exported file.
/// </summary>
public sealed class SavedColumnSetting
{
	#region FIELDS
	public const string DEFAULT_SEQUENCE_VALUE = "1";
	#endregion FIELDS

	#region PROPERTIES
	public string              Name              { get; set; }
	public ValueGenerationMode GenerationMode    { get; set; }
	public string              FixedValue        { get; set; }
	public string              SequenceStart     { get; set; }
	public string              SequenceStep      { get; set; }
	public string              RegexPattern      { get; set; }
	public string              PatternExpression { get; set; }
	public string              SourceColumnName  { get; set; }
	public string              LookupExpression  { get; set; }

	/// <summary>
	///	The schema.table of the column the setting was saved from, or <see langword="null"/> when it applies to a column
	///	name in any table.
	/// </summary>
	public string? TableName { get; set; }

	/// <summary>
	///	The column the setting was saved from.
	/// </summary>
	public string? ColumnName { get; set; }

	/// <summary>
	///	Whether new row sets use this setting for the matching column (see <see cref="TableName"/> and <see cref="ColumnName"/>).
	/// </summary>
	public bool ApplyAutomatically { get; set; }

	public bool AppliesToAnyTable => string.IsNullOrWhiteSpace(TableName);
	#endregion PROPERTIES

	#region CONSTRUCTOR
	/// <summary>
	///	Creates a new <see cref="SavedColumnSetting"/> and sets the default values of its fields and properties.
	/// </summary>
	public SavedColumnSetting()
	{
		Name               = string.Empty;
		GenerationMode     = ValueGenerationMode.Random;
		FixedValue         = string.Empty;
		SequenceStart      = DEFAULT_SEQUENCE_VALUE;
		SequenceStep       = DEFAULT_SEQUENCE_VALUE;
		RegexPattern       = string.Empty;
		PatternExpression  = string.Empty;
		SourceColumnName   = string.Empty;
		LookupExpression   = string.Empty;
		TableName          = null;
		ColumnName         = null;
		ApplyAutomatically = false;
	}
	#endregion CONSTRUCTOR

	#region METHODS
	/// <summary>
	///	Checks whether this setting can apply automatically to the given table and column.
	/// </summary>
	/// <param name="tableName">
	///	The schema.table name of the row set being configured.
	/// </param>
	/// <param name="columnName">
	///	The column name to compare with <see cref="ColumnName"/>.
	/// </param>
	/// <returns>
	///	<see langword="true"/> when the column matches and the setting either applies to any table or to
	///	<paramref name="tableName"/>; otherwise <see langword="false"/>.
	/// </returns>
	public bool Matches(string tableName, string columnName)
		=> !string.IsNullOrWhiteSpace(ColumnName)
			&& string.Equals(ColumnName, columnName, StringComparison.OrdinalIgnoreCase)
			&& (AppliesToAnyTable || string.Equals(TableName, tableName, StringComparison.OrdinalIgnoreCase));

	/// <summary>
	///	Checks whether another setting targets the same column and table scope.
	/// </summary>
	/// <param name="other">
	///	The setting to compare with this one.
	/// </param>
	/// <returns>
	///	<see langword="true"/> when both settings have the same column name, the same any-table scope and, when
	///	table-specific, the same table name; otherwise <see langword="false"/>.
	/// </returns>
	/// <exception cref="ArgumentNullException">
	///	Thrown when <paramref name="other"/> is <see langword="null"/>.
	/// </exception>
	public bool HasSameTarget(SavedColumnSetting other)
	{
		ArgumentNullException.ThrowIfNull(other);

		return string.Equals(ColumnName, other.ColumnName, StringComparison.OrdinalIgnoreCase)
			&& AppliesToAnyTable == other.AppliesToAnyTable
			&& (AppliesToAnyTable || string.Equals(TableName, other.TableName, StringComparison.OrdinalIgnoreCase));
	}

	/// <summary>
	///	Whether both settings generate values in the same way. Only the settings of the generation mode are compared.
	/// </summary>
	/// <param name="other">
	///	The setting whose value-generation fields are compared with this one.
	/// </param>
	/// <returns>
	///	<see langword="true"/> when both settings use the same generation mode and matching values for that mode;
	///	otherwise <see langword="false"/>.
	/// </returns>
	/// <exception cref="ArgumentNullException">
	///	Thrown when <paramref name="other"/> is <see langword="null"/>.
	/// </exception>
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

	/// <summary>
	///	Creates a copy of this saved setting.
	/// </summary>
	/// <returns>
	///	A new setting instance with the same values as this one.
	/// </returns>
	public SavedColumnSetting Clone() => (SavedColumnSetting)MemberwiseClone();
	#endregion METHODS
}