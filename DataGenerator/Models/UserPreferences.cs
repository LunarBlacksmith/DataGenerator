namespace DataGenerator.Models;

/// <summary>
///	Choices the user made in the application that are remembered for the next session.
/// </summary>
public sealed class UserPreferences
{
	#region PROPERTIES
	/// <summary>
	///	The chosen theme, or <see langword="null"/> to follow the Windows app theme.
	/// </summary>
	public AppTheme? Theme { get; set; }

	/// <summary>
	///	The keys of the optional columns of the column rules grid that the user hid (see RuleGridColumnsViewModel).
	/// </summary>
	public List<string> HiddenRuleGridColumns { get; set; }
	#endregion PROPERTIES

	/// <summary>
	///	Creates a new <see cref="UserPreferences"/> and sets the default values of its fields and properties.
	/// </summary>
	public UserPreferences()
	{
		Theme                 = null;
		HiddenRuleGridColumns = [];
	}
}