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

	/// <summary>
	///	Shows the bundled documentation reader without requiring an internet connection.
	/// </summary>
	void ShowDocumentation();

	/// <summary>
	///	Shows or activates the saved Pattern / Regex / SQL expression builder.
	/// </summary>
	void ShowExpressionBuilder();
}