using System.Windows;
using DataGenerator.Interfaces;
using DataGenerator.ViewModels;
using DataGenerator.Views;

namespace DataGenerator.Services;

public sealed class HelpService : IHelpService
{
	private readonly IPatternValueGenerator _patternGenerator;
	private PatternHelpWindow?              _patternHelpWindow;

	public HelpService(IPatternValueGenerator patternGenerator)
	{
		_patternGenerator = patternGenerator ?? throw new ArgumentNullException(nameof(patternGenerator));
	}

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
}