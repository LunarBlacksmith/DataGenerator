namespace DataGenerator.Models;

/// <summary>
///	Choices the user made in the application that are remembered for the next session.
/// </summary>
public sealed class UserPreferences
{
	/// <summary>
	///	The chosen theme, or <see langword="null"/> to follow the Windows app theme.
	/// </summary>
	public AppTheme? Theme { get; set; }

	/// <summary>
	///	The keys of the optional columns of the column rules grid that the user hid (see RuleGridColumnsViewModel).
	/// </summary>
	public List<string> HiddenRuleGridColumns { get; set; } = [];
}