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
	private const string UNEXPECTED_ERROR_TITLE = "Unexpected error";

	private readonly IExceptionFormatter _exceptionFormatter = new ExceptionFormatter();
	private readonly IDialogService      _dialogService      = new DialogService();

	private MainViewModel? _mainViewModel;

	protected override void OnStartup(StartupEventArgs e)
	{
		base.OnStartup(e);

		DispatcherUnhandledException += OnDispatcherUnhandledException;

		ISqlValueConverter     converter            = new SqlValueConverter();
		IRegexValueGenerator   regexGenerator       = new RegexValueGenerator();
		IPatternValueGenerator patternGenerator     = new PatternValueGenerator();
		IColumnValueGenerator  columnValueGenerator = new ColumnValueGenerator(converter, regexGenerator, patternGenerator);
		ColumnRuleServices     ruleServices         = new ColumnRuleServices(converter, columnValueGenerator, patternGenerator, RegexProfile.DEFAULT_PROFILES);

		DatabaseExplorerViewModel explorer = new DatabaseExplorerViewModel(new ColumnRuleFactory(ruleServices), _dialogService);

		_mainViewModel = new MainViewModel(
			new SqlMetadataService(),
			new DataGenerationService(converter, columnValueGenerator),
			new FileDialogService(),
			_dialogService,
			new ShellService(),
			new ClipboardService(),
			new HelpService(patternGenerator),
			_exceptionFormatter,
			new ForeignTableKeyResolver(),
			explorer
		);

		MainWindow window = new MainWindow
		{
			DataContext = _mainViewModel
		};

		MainWindow = window;
		window.Show();
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