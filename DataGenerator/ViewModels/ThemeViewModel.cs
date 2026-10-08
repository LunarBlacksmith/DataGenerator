using System.IO;
using DataGenerator.Infrastructure;
using DataGenerator.Interfaces;
using DataGenerator.Models;

namespace DataGenerator.ViewModels;

/// <summary>
///	Lets the user switch between the light and dark themes, and remembers the choice for the next start.
/// </summary>
public sealed class ThemeViewModel : ObservableObject
{
	#region FIELDS
	private const string THEME_ERROR_TITLE = "Theme";

	private readonly IThemeService         _themeService;
	private readonly IUserPreferencesStore _preferencesStore;
	private readonly IDialogService        _dialogService;
	#endregion FIELDS

	#region PROPERTIES
	public bool IsDarkTheme
	{
		get => _themeService.CurrentTheme == AppTheme.Dark;
		set
		{
			if (value == IsDarkTheme)
			{
				return;
			}

			AppTheme theme = value ? AppTheme.Dark : AppTheme.Light;

			_themeService.ApplyTheme(theme);
			OnPropertyChanged();
			SaveTheme(theme);
		}
	}
	#endregion PROPERTIES

	#region CONSTRUCTOR
	/// <summary>
	///	Creates the theme switcher and connects it to the theme service and preferences.
	/// </summary>
	/// <param name="themeService">
	///	The service that applies themes and reads the Windows theme.
	/// </param>
	/// <param name="preferencesStore">
	///	The preferences store that remembers the user's theme choice.
	/// </param>
	/// <param name="dialogService">
	///	The dialog service used when the choice cannot be saved.
	/// </param>
	/// <exception cref="ArgumentNullException">
	///	Thrown when any service argument is <see langword="null"/>.
	/// </exception>
	public ThemeViewModel(
		IThemeService         themeService,
		IUserPreferencesStore preferencesStore,
		IDialogService        dialogService
	)
	{
		_themeService     = themeService ?? throw new ArgumentNullException(nameof(themeService));
		_preferencesStore = preferencesStore ?? throw new ArgumentNullException(nameof(preferencesStore));
		_dialogService    = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
	}
	#endregion CONSTRUCTOR

	#region METHODS
	#region PUBLIC
	/// <summary>
	///	Applies the theme the user chose last time, or the Windows application theme when they have never chosen one.
	/// </summary>
	public void ApplySavedTheme()
	{
		AppTheme theme = _preferencesStore.Load().Theme ?? _themeService.GetWindowsTheme();

		_themeService.ApplyTheme(theme);
		OnPropertyChanged(nameof(IsDarkTheme));
	}
	#endregion PUBLIC

	#region PRIVATE
	/// <summary>
	///	Stores the chosen theme in the user's preferences.
	/// </summary>
	/// <param name="theme">
	///	The theme to remember.
	/// </param>
	private void SaveTheme(AppTheme theme)
	{
		try
		{
			UserPreferences preferences = _preferencesStore.Load();

			preferences.Theme = theme;
			_preferencesStore.Save(preferences);
		}
		catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
		{
			_dialogService.ShowError(
				THEME_ERROR_TITLE,
				$"The theme was changed, but it could not be remembered for next time.{Environment.NewLine}{Environment.NewLine}{exception.Message}"
			);
		}
	}
	#endregion PRIVATE
	#endregion METHODS
}