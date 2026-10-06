using System.Windows;
using System.Windows.Threading;
using DataGenerator.Infrastructure;
using DataGenerator.Interfaces;
using DataGenerator.Models;
using DataGenerator.Services;
using DataGenerator.Services.Patterns;
using DataGenerator.ViewModels;

namespace DataGenerator;

/// <summary>
///	Composition root: creates the services and view models, shows the main window and reports unhandled errors.
/// </summary>
public partial class App : Application
{
	#region FIELDS
	#region PRIVATE
	private const string UNEXPECTED_ERROR_TITLE         = "Unexpected error";
	private const string SAVED_SETTINGS_ERROR_TITLE     = "Saved column settings";
	private const string SET_CONFIGURATIONS_ERROR_TITLE = "Set configurations";

	private readonly IExceptionFormatter _exceptionFormatter;
	private readonly IDialogService      _dialogService;

	private MainViewModel? _mainViewModel;
	#endregion PRIVATE
	#endregion FIELDS

	#region CONSTRUCTORS
	#region PUBLIC
	/// <summary>
	///	Creates a new <see cref="App"/> and sets the default values of its fields and properties.
	/// </summary>
	public App()
	{
		_exceptionFormatter = new ExceptionFormatter();
		_dialogService      = new DialogService();
		_mainViewModel      = null;
	}
	#endregion PUBLIC
	#endregion CONSTRUCTORS

	#region METHODS
	#region PROTECTED
	/// <summary>
	///	Composes the application: creates the services and view models, loads the saved settings, set configurations and
	///	preferences, applies the saved theme and shows the main window.
	///	<para>
	///		Problems reading the saved settings or set configurations files do not stop the application; they are shown
	///		once the main window is open.
	///	</para>
	/// </summary>
	/// <param name="e">
	///	The start-up arguments, passed on to the base class.
	/// </param>
	protected override void OnStartup(StartupEventArgs e)
	{
		base.OnStartup(e);

		DispatcherUnhandledException += OnDispatcherUnhandledException;

		HintPresentation.Register();

		ISqlValueConverter          converter            = new SqlValueConverter();
		IRegexValueGenerator        regexGenerator       = new RegexValueGenerator();
		IPatternValueGenerator      patternGenerator     = new PatternValueGenerator();
		IColumnValueCaster          columnValueCaster    = new ColumnValueCaster(converter);
		IColumnValueGenerator       columnValueGenerator = new ColumnValueGenerator(converter, regexGenerator, patternGenerator, columnValueCaster);
		IPatternSqlTranslator       patternTranslator    = new PatternSqlTranslator();
		ITableCatalog               tableCatalog         = new TableCatalog();
		IFileDialogService          fileDialogService    = new FileDialogService();
		SavedSettingsLibrary        savedSettings        = new(new JsonSavedSettingsStore(SecurePathService.GetSavedSettingsFilePath()));
		ISavedSettingsWindowService savedSettingsWindows = new SavedSettingsWindowService(savedSettings, _dialogService, fileDialogService);
		string?                     savedSettingsWarning = savedSettings.Load();
		RowSetConfigurationLibrary  setConfigurations    = new(new JsonRowSetConfigurationStore(SecurePathService.GetSetConfigurationsFilePath()));
		string?                     setConfigWarning     = setConfigurations.Load();
		IUserPreferencesStore       preferencesStore     = new JsonUserPreferencesStore(SecurePathService.GetPreferencesFilePath());

		ThemeViewModel theme = new(
			new ThemeService(this),
			preferencesStore,
			_dialogService
		);

		// Applied before any window is created, so nothing is ever drawn in the wrong colours.
		theme.ApplySavedTheme();

		ColumnRuleServices ruleServices = new(
			converter,
			columnValueGenerator,
			patternGenerator,
			RegexProfile.DEFAULT_PROFILES,
			savedSettings,
			savedSettingsWindows,
			tableCatalog,
			new LookupExpressionParser(tableCatalog, patternTranslator)
		);

		DatabaseExplorerViewModel explorer = new(
			new ColumnRuleFactory(ruleServices),
			savedSettings,
			savedSettingsWindows,
			new RuleGridColumnsViewModel(preferencesStore, _dialogService),
			new RowSetConfigurationsViewModel(setConfigurations, _dialogService, fileDialogService),
			_dialogService
		);

		_mainViewModel = new MainViewModel(
			new SqlMetadataService(),
			new DataGenerationService(converter, columnValueGenerator, patternTranslator),
			fileDialogService,
			_dialogService,
			new ShellService(),
			new ClipboardService(),
			new HelpService(patternGenerator),
			_exceptionFormatter,
			new ForeignTableKeyResolver(),
			tableCatalog,
			explorer,
			theme,
			new PostGenerationSqlViewModel(new PostGenerationSqlParser())
		);

		MainWindow window = new()
		{
			DataContext = _mainViewModel
		};

		MainWindow = window;
		window.Show();

		if (savedSettingsWarning is not null)
		{
			_dialogService.ShowError(SAVED_SETTINGS_ERROR_TITLE, savedSettingsWarning);
		}

		if (setConfigWarning is not null)
		{
			_dialogService.ShowError(SET_CONFIGURATIONS_ERROR_TITLE, setConfigWarning);
		}
	}
	#endregion PROTECTED

	#region PRIVATE
	/// <summary>
	///	Shows errors that nothing else handled in the error panel, or in a message box before the main window exists, and
	///	keeps the application running.
	/// </summary>
	/// <param name="sender">
	///	The dispatcher that raised the event.
	/// </param>
	/// <param name="e">
	///	The unhandled exception; it is marked as handled so the application does not close.
	/// </param>
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
	#endregion PRIVATE
	#endregion METHODS
}