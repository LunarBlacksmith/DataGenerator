namespace DataGenerator.Interfaces;

public interface IHelpService
{
	/// <summary>
	///	Shows the pattern language reference window, optionally pre-filled with an expression to try out.
	/// </summary>
	/// <param name="expression">
	///	Optional pattern expression to place in the help window; <see langword="null"/>, empty or whitespace leaves the
	///	current expression unchanged.
	/// </param>
	void ShowPatternLanguageHelp(string? expression = null);
}