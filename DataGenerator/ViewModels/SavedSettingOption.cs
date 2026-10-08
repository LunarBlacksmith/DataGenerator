using DataGenerator.Models;

namespace DataGenerator.ViewModels;

/// <summary>
///	A saved setting offered in the saved-settings menu of a column rule.
/// </summary>
public sealed class SavedSettingOption
{
	#region PROPERTIES
	public SavedColumnSetting Setting          { get; }
	public bool               IsSavedForColumn { get; }
	public string             Summary          { get; }

	public string Name => Setting.Name;

	public string ToolTipText
		=> $"{Summary}{Environment.NewLine}Saved for: {SavedSettingDescriber.DescribeTarget(Setting)}"
			+ (Setting.ApplyAutomatically ? $"{Environment.NewLine}Applied automatically to new row sets" : string.Empty);
	#endregion PROPERTIES

	/// <summary>
	///	Creates a saved setting choice for a column-rule menu.
	/// </summary>
	/// <param name="setting">
	///	The saved setting offered to the user.
	/// </param>
	/// <param name="isSavedForColumn">
	///	Whether the setting was saved for the active column rather than only by name.
	/// </param>
	/// <exception cref="ArgumentNullException">
	///	Thrown when <paramref name="setting"/> is <see langword="null"/>.
	/// </exception>
	public SavedSettingOption(SavedColumnSetting setting, bool isSavedForColumn)
	{
		Setting          = setting ?? throw new ArgumentNullException(nameof(setting));
		IsSavedForColumn = isSavedForColumn;
		Summary          = SavedSettingDescriber.DescribeValues(setting);
	}
}