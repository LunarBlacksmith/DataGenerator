using DataGenerator.Models;

namespace DataGenerator.ViewModels;

/// <summary>
/// Text that describes saved column settings in lists, menus and dialogs.
/// </summary>
public static class SavedSettingDescriber
{
	private const int    MAXIMUM_VALUE_LENGTH = 60;
	private const string ELLIPSIS             = "…";

	/// <summary>
	/// The generation mode and its settings, e.g. "Sequence from 1, step 1".
	/// </summary>
	public static string DescribeValues(SavedColumnSetting setting)
	{
		ArgumentNullException.ThrowIfNull(setting);

		return setting.GenerationMode switch
		{
			ValueGenerationMode.Fixed    => $"Fixed value: {Shorten(setting.FixedValue.Length == 0 ? "(empty)" : setting.FixedValue)}",
			ValueGenerationMode.Sequence => $"Sequence from {setting.SequenceStart.Trim()}, step {setting.SequenceStep.Trim()}",
			ValueGenerationMode.Regex    => $"Regex: {Shorten(setting.RegexPattern)}",
			ValueGenerationMode.Pattern  => $"Pattern: {Shorten(setting.PatternExpression)}",
			_                            => GenerationModeOption.Get(setting.GenerationMode).DisplayName
		};
	}

	/// <summary>
	/// The columns that use the setting automatically, e.g. "OrderNumber in dbo.Orders".
	/// </summary>
	public static string DescribeTarget(SavedColumnSetting setting)
	{
		ArgumentNullException.ThrowIfNull(setting);

		if (setting.ColumnName is null)
		{
			return "(not saved from a column)";
		}

		return setting.AppliesToAnyTable
			? $"Every column named {setting.ColumnName}"
			: $"{setting.ColumnName} in {setting.TableName}";
	}

	private static string Shorten(string text)
	{
		string singleLine = text.ReplaceLineEndings(" ");

		return singleLine.Length <= MAXIMUM_VALUE_LENGTH
			? singleLine
			: string.Concat(singleLine.AsSpan(0, MAXIMUM_VALUE_LENGTH - ELLIPSIS.Length), ELLIPSIS);
	}
}