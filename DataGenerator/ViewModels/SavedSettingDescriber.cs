using DataGenerator.Models;

namespace DataGenerator.ViewModels;

/// <summary>
///	Text that describes saved column settings in lists, menus and dialogs.
/// </summary>
public static class SavedSettingDescriber
{
	#region FIELDS
	#region PRIVATE
	private const int    MAXIMUM_VALUE_LENGTH = 60;
	private const string ELLIPSIS             = "…";
	#endregion PRIVATE
	#endregion FIELDS

	#region CONSTRUCTORS
	#region STATIC
	/// <summary>
	///	Initialises the static state of <see cref="SavedSettingDescriber"/>.
	/// </summary>
	static SavedSettingDescriber()
	{
	}
	#endregion STATIC
	#endregion CONSTRUCTORS

	#region METHODS
	#region PUBLIC
	/// <summary>
	///	The generation mode and its settings, e.g. "Sequence from 1, step 1".
	/// </summary>
	/// <param name="setting">
	///	The saved setting to describe.
	/// </param>
	/// <returns>
	///	The short text that describes the saved values.
	/// </returns>
	/// <exception cref="ArgumentNullException">
	///	Thrown when <paramref name="setting"/> is <see langword="null"/>.
	/// </exception>
	public static string DescribeValues(SavedColumnSetting setting)
	{
		ArgumentNullException.ThrowIfNull(setting);

		return setting.GenerationMode switch
		{
			ValueGenerationMode.Fixed       => $"Fixed value: {Shorten(setting.FixedValue.Length == 0 ? "(empty)" : setting.FixedValue)}",
			ValueGenerationMode.Sequence    => $"Sequence from {setting.SequenceStart.Trim()}, step {setting.SequenceStep.Trim()}",
			ValueGenerationMode.Regex       => $"Regex: {Shorten(setting.RegexPattern)}",
			ValueGenerationMode.Pattern     => $"Pattern: {Shorten(setting.PatternExpression)}",
			ValueGenerationMode.CopyColumn  => $"Copy of column [{Shorten(setting.SourceColumnName.Trim())}]",
			ValueGenerationMode.TableLookup => $"Value from table: {Shorten(setting.LookupExpression.Trim())}",
			_                               => GenerationModeOption.Get(setting.GenerationMode).DisplayName
		};
	}

	/// <summary>
	///	The columns that use the setting automatically, e.g. "OrderNumber in dbo.Orders".
	/// </summary>
	/// <param name="setting">
	///	The saved setting whose automatic target is described.
	/// </param>
	/// <returns>
	///	The target text, or a note that the setting was not saved from a column.
	/// </returns>
	/// <exception cref="ArgumentNullException">
	///	Thrown when <paramref name="setting"/> is <see langword="null"/>.
	/// </exception>
	public static string DescribeTarget(SavedColumnSetting setting)
	{
		ArgumentNullException.ThrowIfNull(setting);

		return
			setting.ColumnName is null
				? "(not saved from a column)"
				: setting.AppliesToAnyTable
					? $"Every column named {setting.ColumnName}"
					: $"{setting.ColumnName} in {setting.TableName}";
	}
	#endregion PUBLIC

	#region PRIVATE
	/// <summary>
	///	Makes a setting value safe for single-line summaries and shortens long values.
	/// </summary>
	/// <param name="text">
	///	The value text to shorten.
	/// </param>
	/// <returns>
	///	The single-line value, with an ellipsis when it was longer than the maximum length.
	/// </returns>
	private static string Shorten(string text)
	{
		string singleLine = text.ReplaceLineEndings(" ");

		return
			singleLine.Length <= MAXIMUM_VALUE_LENGTH
				? singleLine
				: string.Concat(singleLine.AsSpan(0, MAXIMUM_VALUE_LENGTH - ELLIPSIS.Length), ELLIPSIS);
	}
	#endregion PRIVATE
	#endregion METHODS
}