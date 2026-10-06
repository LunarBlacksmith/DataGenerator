using DataGenerator.Models;

namespace DataGenerator.ViewModels;

/// <summary>
/// A saved setting offered in the saved-settings menu of a column rule.
/// </summary>
public sealed class SavedSettingOption
{
	public SavedSettingOption(SavedColumnSetting setting, bool isSavedForColumn)
	{
		Setting          = setting ?? throw new ArgumentNullException(nameof(setting));
		IsSavedForColumn = isSavedForColumn;
		Summary          = SavedSettingDescriber.DescribeValues(setting);
	}

	public SavedColumnSetting Setting          { get; }
	public bool               IsSavedForColumn { get; }
	public string             Summary          { get; }

	public string Name => Setting.Name;

	public string ToolTipText
		=> $"{Summary}{Environment.NewLine}Saved for: {SavedSettingDescriber.DescribeTarget(Setting)}"
			+ (Setting.ApplyAutomatically ? $"{Environment.NewLine}Applied automatically to new row sets" : string.Empty);
}