using System.Windows;
using DataGenerator.Interfaces;
using DataGenerator.ViewModels;
using DataGenerator.Views;

namespace DataGenerator.Services;

public sealed class HelpService : IHelpService
{
	#region FIELDS
	private readonly IPatternValueGenerator _patternGenerator;
	private readonly IMarkdownRenderer      _markdownRenderer;
	private readonly IShellService          _shellService;
	private readonly IExpressionBuilder     _expressionBuilder;
	private readonly IExpressionLibraryStore _expressionStore;
	private readonly IClipboardService      _clipboardService;
	private readonly IDialogService         _dialogService;
	private PatternHelpWindow?              _patternHelpWindow;
	private DocumentationWindow?            _documentationWindow;
	private ExpressionBuilderWindow?        _expressionBuilderWindow;
	#endregion FIELDS

	#region CONSTRUCTOR
	/// <summary>
	///	Creates the service that shows the pattern reference and bundled documentation windows.
	/// </summary>
	/// <param name="patternGenerator">
	///	The pattern generator used by the help view model to evaluate examples.
	/// </param>
	/// <param name="markdownRenderer">
	///	Converts the bundled Markdown guides into formatted pages.
	/// </param>
	/// <param name="shellService">
	///	Opens web links from the guides in the user's browser.
	/// </param>
	/// <param name="expressionBuilder">Builds expressions from explicit segment syntax.</param>
	/// <param name="expressionStore">Stores the reusable expression library.</param>
	/// <param name="clipboardService">Copies generated expressions.</param>
	/// <param name="dialogService">Reports persistence errors and confirms replacement.</param>
	/// <exception cref="ArgumentNullException">
	///	Thrown when any argument is <see langword="null"/>.
	/// </exception>
	public HelpService(
		IPatternValueGenerator patternGenerator,
		IMarkdownRenderer markdownRenderer,
		IShellService shellService,
		IExpressionBuilder expressionBuilder,
		IExpressionLibraryStore expressionStore,
		IClipboardService clipboardService,
		IDialogService dialogService)
	{
		_patternHelpWindow   = null;
		_documentationWindow = null;

		_patternGenerator = patternGenerator ?? throw new ArgumentNullException(nameof(patternGenerator));
		_markdownRenderer = markdownRenderer ?? throw new ArgumentNullException(nameof(markdownRenderer));
		_shellService     = shellService ?? throw new ArgumentNullException(nameof(shellService));
		_expressionBuilder = expressionBuilder ?? throw new ArgumentNullException(nameof(expressionBuilder));
		_expressionStore = expressionStore ?? throw new ArgumentNullException(nameof(expressionStore));
		_clipboardService = clipboardService ?? throw new ArgumentNullException(nameof(clipboardService));
		_dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
	}
	#endregion CONSTRUCTOR

	#region METHODS
	/// <summary>
	///	Shows or activates the pattern language help window and optionally loads an expression into it.
	/// </summary>
	/// <param name="expression">
	///	The optional expression to show in the help window.
	/// </param>
	public void ShowPatternLanguageHelp(string? expression = null)
	{
		if (_patternHelpWindow is null)
		{
			_patternHelpWindow = new PatternHelpWindow
			{
				DataContext = new PatternHelpViewModel(_patternGenerator),
				Owner       = Application.Current?.MainWindow
			};

			_patternHelpWindow.Closed += (_, _) => _patternHelpWindow = null;
			_patternHelpWindow.Show();
		}

		if (!string.IsNullOrWhiteSpace(expression) && _patternHelpWindow.DataContext is PatternHelpViewModel viewModel)
		{
			viewModel.Expression = expression;
		}

		if (_patternHelpWindow.WindowState == WindowState.Minimized)
		{
			_patternHelpWindow.WindowState = WindowState.Normal;
		}

		_ = _patternHelpWindow.Activate();
	}

	/// <summary>
	///	Shows or activates a reader for the guides bundled with this version of the application.
	/// </summary>
	/// <exception cref="System.IO.IOException">
	///	Thrown when a bundled guide is missing or cannot be read.
	/// </exception>
	public void ShowDocumentation()
	{
		if (_documentationWindow is null)
		{
			Window?                owner     = Application.Current?.MainWindow;
			DocumentationViewModel viewModel = new(_markdownRenderer, _shellService);
			_documentationWindow = new DocumentationWindow
			{
				DataContext = viewModel,
				Owner       = owner
			};
			_documentationWindow.Closed += (_, _) => _documentationWindow = null;
			_documentationWindow.Show();
		}

		if (_documentationWindow.WindowState == WindowState.Minimized)
		{
			_documentationWindow.WindowState = WindowState.Normal;
		}

		_ = _documentationWindow.Activate();
	}

	/// <summary>
	///	Shows one reusable, modeless expression builder window without requiring a database connection.
	/// </summary>
	public void ShowExpressionBuilder()
	{
		if (_expressionBuilderWindow is null)
		{
			_expressionBuilderWindow = new ExpressionBuilderWindow
			{
				DataContext = new ExpressionBuilderViewModel(_expressionBuilder, _expressionStore, _clipboardService, _dialogService),
				Owner = Application.Current?.MainWindow
			};
			_expressionBuilderWindow.Closed += (_, _) => _expressionBuilderWindow = null;
			_expressionBuilderWindow.Show();
		}

		if (_expressionBuilderWindow.WindowState == WindowState.Minimized)
		{
			_expressionBuilderWindow.WindowState = WindowState.Normal;
		}

		_ = _expressionBuilderWindow.Activate();
	}
	#endregion METHODS
}