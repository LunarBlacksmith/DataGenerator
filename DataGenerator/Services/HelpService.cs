using System.Windows;
using DataGenerator.Interfaces;
using DataGenerator.ViewModels;
using DataGenerator.Views;

namespace DataGenerator.Services;

public sealed class HelpService : IHelpService
{
	#region FIELDS
	#region PRIVATE
	private readonly IPatternValueGenerator _patternGenerator;
	private PatternHelpWindow?              _patternHelpWindow;
	#endregion PRIVATE
	#endregion FIELDS

	#region CONSTRUCTORS
	#region PUBLIC
	/// <summary>
	///	Creates the service that shows the pattern language help window.
	/// </summary>
	/// <param name="patternGenerator">
	///	The pattern generator used by the help view model to evaluate examples.
	/// </param>
	/// <exception cref="ArgumentNullException">
	///	Thrown when <paramref name="patternGenerator"/> is <see langword="null"/>.
	/// </exception>
	public HelpService(IPatternValueGenerator patternGenerator)
	{
		_patternHelpWindow = null;

		_patternGenerator = patternGenerator ?? throw new ArgumentNullException(nameof(patternGenerator));
	}
	#endregion PUBLIC
	#endregion CONSTRUCTORS

	#region METHODS
	#region PUBLIC
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
	#endregion PUBLIC
	#endregion METHODS
}