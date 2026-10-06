using DataGenerator.Models;

namespace DataGenerator.Interfaces;

/// <summary>
/// Switches the colours of the whole application between the light and dark themes.
/// </summary>
public interface IThemeService
{
	AppTheme CurrentTheme { get; }

	/// <summary>
	/// Returns the theme Windows uses for applications, or <see cref="AppTheme.Light"/> when it cannot be read.
	/// </summary>
	AppTheme GetWindowsTheme();

	/// <summary>
	/// Replaces the application's colours, and the title bars of its windows, with those of the given theme.
	/// </summary>
	void ApplyTheme(AppTheme theme);
}