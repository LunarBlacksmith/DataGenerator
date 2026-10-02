namespace DataGenerator.Models;

/// <summary>
/// Choices the user made in the application that are remembered for the next session.
/// </summary>
public sealed class UserPreferences
{
	/// <summary>
	/// The chosen theme, or <see langword="null"/> to follow the Windows app theme.
	/// </summary>
	public AppTheme? Theme { get; set; }
}