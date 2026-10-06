namespace DataGenerator.Interfaces;

public interface IHelpService
{
	/// <summary>
	/// Shows the pattern language reference window, optionally pre-filled with an expression to try out.
	/// </summary>
	void ShowPatternLanguageHelp(string? expression = null);
}