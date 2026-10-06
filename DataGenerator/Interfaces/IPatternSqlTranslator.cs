namespace DataGenerator.Interfaces;

/// <summary>
///	Finds values that a pattern could have produced, so a pattern can select existing rows, e.g. the ShirtIDs that look
///	like 'S' THEN NUM(digits=5) GREATER THAN 50. See Docs/PatternLanguage.md for the functions that can be used.
/// </summary>
public interface IPatternSqlTranslator
{
	/// <summary>
	///	A T-SQL condition that is true when <paramref name="textExpression"/> (an nvarchar expression) matches the
	///	pattern, ignoring letter case. Throws <see cref="Services.Patterns.PatternSyntaxException"/> when the pattern is invalid
	///	or uses functions whose values cannot be recognised, such as RAND_DATE.
	/// </summary>
	/// <param name="expression">
	///	The pattern expression to translate.
	/// </param>
	/// <param name="textExpression">
	///	The T-SQL expression that yields the text to test.
	/// </param>
	/// <returns>
	///	A T-SQL condition that can be placed in a WHERE clause.
	/// </returns>
	/// <exception cref="ArgumentException">
	///	Thrown when <paramref name="textExpression"/> is <see langword="null"/>, empty or whitespace.
	/// </exception>
	/// <exception cref="Services.Patterns.PatternSyntaxException">
	///	Thrown when <paramref name="expression"/> is not a valid SQL-translatable pattern.
	/// </exception>
	string ToSqlCondition(string expression, string textExpression);

	/// <summary>
	///	Checks whether a pattern can be translated into SQL.
	/// </summary>
	/// <param name="expression">
	///	The pattern expression to validate.
	/// </param>
	/// <param name="errorMessage">
	///	Empty text when validation succeeds; otherwise the parser or translation error.
	/// </param>
	/// <returns>
	///	<see langword="true"/> when the expression can be translated; otherwise <see langword="false"/>.
	/// </returns>
	bool TryValidate(string expression, out string errorMessage);
}
