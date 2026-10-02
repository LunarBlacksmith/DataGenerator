using System.Windows;
using System.Windows.Threading;
using DataGenerator.Models;
using DataGenerator.Services;
using DataGenerator.Services.Patterns;
using DataGenerator.ViewModels;

namespace DataGenerator;

/// <summary>
/// Composition root: creates the services and view models, shows the main window and reports unhandled errors.
/// </summary>
public partial class App : Application
{
	private const string UNEXPECTED_ERROR_TITLE     = "Unexpected error";
	private const string SAVED_SETTINGS_ERROR_TITLE = "Saved settings";

	private readonly IExceptionFormatter _exceptionFormatter = new ExceptionFormatter();
	private readonly IDialogService      _dialogService      = new DialogService();

	private MainViewModel? _mainViewModel;

	protected override void OnStartup(StartupEventArgs e)
	{
		base.OnStartup(e);

		DispatcherUnhandledException += OnDispatcherUnhandledException;

		ISqlValueConverter          converter            = new SqlValueConverter();
		IRegexValueGenerator        regexGenerator       = new RegexValueGenerator();
		IPatternValueGenerator      patternGenerator     = new PatternValueGenerator();
		IColumnValueGenerator       columnValueGenerator = new ColumnValueGenerator(converter, regexGenerator, patternGenerator);
		IFileDialogService          fileDialogService    = new FileDialogService();
		SavedSettingsLibrary        savedSettings        = new SavedSettingsLibrary(new JsonSavedSettingsStore(SecurePathService.GetSavedSettingsFilePath()));
		ISavedSettingsWindowService savedSettingsWindows = new SavedSettingsWindowService(savedSettings, _dialogService, fileDialogService);
		string?                     savedSettingsWarning = savedSettings.Load();

		ThemeViewModel theme = new ThemeViewModel(
			new ThemeService(this),
			new JsonUserPreferencesStore(SecurePathService.GetPreferencesFilePath()),
			_dialogService
		);

		// Applied before any window is created, so nothing is ever drawn in the wrong colours.
		theme.ApplySavedTheme();

		ColumnRuleServices ruleServices = new ColumnRuleServices(
			converter,
			columnValueGenerator,
			patternGenerator,
			RegexProfile.DEFAULT_PROFILES,
			savedSettings,
			savedSettingsWindows
		);

		DatabaseExplorerViewModel explorer = new DatabaseExplorerViewModel(
			new ColumnRuleFactory(ruleServices),
			savedSettings,
			savedSettingsWindows,
			_dialogService
		);

		_mainViewModel = new MainViewModel(
			new SqlMetadataService(),
			new DataGenerationService(converter, columnValueGenerator),
			fileDialogService,
			_dialogService,
			new ShellService(),
			new ClipboardService(),
			new HelpService(patternGenerator),
			_exceptionFormatter,
			new ForeignTableKeyResolver(),
			explorer,
			theme
		);

		MainWindow window = new MainWindow
		{
			DataContext = _mainViewModel
		};

		MainWindow = window;
		window.Show();

		if (savedSettingsWarning is not null)
		{
			_dialogService.ShowError(SAVED_SETTINGS_ERROR_TITLE, savedSettingsWarning);
		}
	}

	/// <summary>
	/// Shows errors that nothing else handled in the error panel, or in a message box before the main window exists, and keeps the application running.
	/// </summary>
	private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
	{
		if (_mainViewModel is not null)
		{
			_mainViewModel.ReportError(e.Exception);
		}
		else
		{
			ErrorReport report = _exceptionFormatter.Format(e.Exception);

			_dialogService.ShowError(
				UNEXPECTED_ERROR_TITLE,
				$"{report.Summary}{Environment.NewLine}{Environment.NewLine}Where: {report.Location}"
			);
		}

		e.Handled = true;
	}
}