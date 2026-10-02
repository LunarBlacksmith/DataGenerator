using System.IO;
using DataGenerator.Infrastructure;
using DataGenerator.Models;
using DataGenerator.Services;

namespace DataGenerator.ViewModels;

/// <summary>
/// Lets the user switch between the light and dark themes, and remembers the choice for the next start.
/// </summary>
public sealed class ThemeViewModel : ObservableObject
{
	private const string THEME_ERROR_TITLE = "Theme";

	private readonly IThemeService         _themeService;
	private readonly IUserPreferencesStore _preferencesStore;
	private readonly IDialogService        _dialogService;

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

	/// <summary>
	/// Applies the theme the user chose last time, or the Windows application theme when they have never chosen one.
	/// </summary>
	public void ApplySavedTheme()
	{
		AppTheme theme = _preferencesStore.Load().Theme ?? _themeService.GetWindowsTheme();

		_themeService.ApplyTheme(theme);
		OnPropertyChanged(nameof(IsDarkTheme));
	}

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
}