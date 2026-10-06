using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Security;
using System.Windows;
using DataGenerator.Infrastructure;
using DataGenerator.Interfaces;
using DataGenerator.Models;
using Microsoft.Win32;

namespace DataGenerator.Services;

/// <summary>
/// Switches themes by replacing the application's brush dictionary; every brush is looked up dynamically, so open windows recolour at once.
/// Create one per application: it also gives each window that opens later the title bar of the current theme.
/// </summary>
public sealed class ThemeService : IThemeService
{
	private const string BRUSHES_SOURCE_MARKER   = "Themes/Brushes.";
	private const string BRUSHES_URI_FORMAT      = "pack://application:,,,/DataGenerator;component/Themes/Brushes.{0}.xaml";
	private const string PERSONALIZE_KEY_PATH    = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
	private const string APPS_USE_LIGHT_THEME    = "AppsUseLightTheme";
	private const int    WINDOWS_DARK_THEME_FLAG = 0;

	private readonly Application _application;

	public ThemeService(Application application)
	{
		_application = application ?? throw new ArgumentNullException(nameof(application));

		EventManager.RegisterClassHandler(typeof(Window), FrameworkElement.LoadedEvent, new RoutedEventHandler(OnWindowLoaded));
	}

	public AppTheme CurrentTheme { get; private set; } = AppTheme.Light;

	public AppTheme GetWindowsTheme()
	{
		try
		{
			using RegistryKey? key = Registry.CurrentUser.OpenSubKey(PERSONALIZE_KEY_PATH);

			return key?.GetValue(APPS_USE_LIGHT_THEME) is int flag && flag == WINDOWS_DARK_THEME_FLAG
				? AppTheme.Dark
				: AppTheme.Light;
		}
		catch (Exception exception) when (exception is SecurityException or UnauthorizedAccessException or IOException)
		{
			return AppTheme.Light;
		}
	}

	public void ApplyTheme(AppTheme theme)
	{
		if (!Enum.IsDefined(theme))
		{
			throw new ArgumentOutOfRangeException(nameof(theme), theme, "Unknown theme.");
		}

		ResourceDictionary brushes = new()
		{
			Source = new Uri(string.Format(CultureInfo.InvariantCulture, BRUSHES_URI_FORMAT, theme), UriKind.Absolute)
		};

		Collection<ResourceDictionary> dictionaries = _application.Resources.MergedDictionaries;
		ResourceDictionary?            current      = dictionaries.FirstOrDefault(IsBrushDictionary);

		// The brushes go where the current ones were, so the dictionaries merged after them can still override them.
		if (current is null)
		{
			dictionaries.Insert(0, brushes);
		}
		else
		{
			dictionaries[dictionaries.IndexOf(current)] = brushes;
		}

		CurrentTheme = theme;

		foreach (Window window in _application.Windows)
		{
			WindowTitleBar.Apply(window, theme == AppTheme.Dark);
		}
	}

	private static bool IsBrushDictionary(ResourceDictionary dictionary)
	{
		return dictionary.Source is not null
			&& dictionary.Source.OriginalString.Contains(BRUSHES_SOURCE_MARKER, StringComparison.OrdinalIgnoreCase);
	}

	private void OnWindowLoaded(object sender, RoutedEventArgs e)
	{
		if (sender is Window window)
		{
			WindowTitleBar.Apply(window, CurrentTheme == AppTheme.Dark);
		}
	}
}