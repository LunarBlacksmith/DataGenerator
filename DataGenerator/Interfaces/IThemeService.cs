using DataGenerator.Models;

namespace DataGenerator.Interfaces;

/// <summary>
///	Switches the colours of the whole application between the light and dark themes.
/// </summary>
public interface IThemeService
{
	AppTheme CurrentTheme { get; }

	/// <summary>
	///	Returns the theme Windows uses for applications, or <see cref="AppTheme.Light"/> when it cannot be read.
	/// </summary>
	/// <returns>
	///	<see cref="AppTheme.Dark"/> when Windows is configured for dark application themes; otherwise
	///	<see cref="AppTheme.Light"/>.
	/// </returns>
	AppTheme GetWindowsTheme();

	/// <summary>
	///	Replaces the application's colours, and the title bars of its windows, with those of the given theme.
	/// </summary>
	/// <param name="theme">
	///	The theme to apply.
	/// </param>
	/// <exception cref="ArgumentOutOfRangeException">
	///	Thrown when <paramref name="theme"/> is not a defined <see cref="AppTheme"/>.
	/// </exception>
	void ApplyTheme(AppTheme theme);
}