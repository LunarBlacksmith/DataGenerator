namespace DataGenerator.Interfaces;

/// <summary>
///	Generates text from the plain-English pattern language, for example
///	<c>P FOLLOWED BY SEQ(1-1000) FOLLOWED BY (X OR Y) FOLLOWED BY RAND_NUM(0, 99, 2)</c>.
///	See Docs/PatternLanguage.md for the full reference.
/// </summary>
public interface IPatternValueGenerator
{
	/// <summary>
	///	Generates the value for the zero-based <paramref name="rowIndex"/> within a row set.
	///	Throws <see cref="Services.Patterns.PatternSyntaxException"/> when the expression is invalid.
	/// </summary>
	/// <param name="expression">
	///	The pattern expression to run.
	/// </param>
	/// <param name="rowIndex">
	///	The zero-based row index used by sequence functions.
	/// </param>
	/// <returns>
	///	The generated text. Empty text is returned when the pattern contains no output segments.
	/// </returns>
	/// <exception cref="ArgumentNullException">
	///	Thrown when <paramref name="expression"/> is <see langword="null"/>.
	/// </exception>
	/// <exception cref="Services.Patterns.PatternSyntaxException">
	///	Thrown when <paramref name="expression"/> is not valid.
	/// </exception>
	string Generate(string expression, long rowIndex);

	/// <summary>
	///	Generates a value whose COL(name) calls are answered by <paramref name="columnValues"/>, which returns the
	///	value of another column of the same row as text.
	/// </summary>
	/// <param name="expression">
	///	The pattern expression to run.
	/// </param>
	/// <param name="rowIndex">
	///	The zero-based row index used by sequence functions.
	/// </param>
	/// <param name="rowCount">
	///	The number of rows in the row set, used by LAST(...); <see langword="null"/> when unknown.
	/// </param>
	/// <param name="columnValues">
	///	Function that returns another column's text value for COL(...), or <see langword="null"/> when COL(...) is not
	///	available.
	/// </param>
	/// <returns>
	///	The generated text. Empty text is returned when the pattern contains no output segments.
	/// </returns>
	/// <exception cref="ArgumentNullException">
	///	Thrown when <paramref name="expression"/> is <see langword="null"/>.
	/// </exception>
	/// <exception cref="Services.Patterns.PatternSyntaxException">
	///	Thrown when <paramref name="expression"/> is not valid or requires a column value that cannot be supplied.
	/// </exception>
	/// <param name="typedColumnValues">
	///	Optional raw same-row values for typed predicates and extraction. When omitted, COL operands remain text.
	/// </param>
	string Generate(string expression, long rowIndex, long? rowCount, Func<string, string>? columnValues,
		Func<string, object?>? typedColumnValues = null);

	/// <summary>
	///	The names of the columns used with COL(...) in the expression, or none when the expression is invalid.
	/// </summary>
	/// <param name="expression">
	///	The pattern expression to inspect.
	/// </param>
	/// <returns>
	///	The referenced column names, or an empty list when the expression is invalid or has no COL(...) calls.
	/// </returns>
	IReadOnlyList<string> GetColumnReferences(string expression);

	/// <summary>
	///	Checks whether an expression is valid for text generation.
	/// </summary>
	/// <param name="expression">
	///	The pattern expression to validate.
	/// </param>
	/// <param name="errorMessage">
	///	Empty text when validation succeeds; otherwise the parser error.
	/// </param>
	/// <returns>
	///	<see langword="true"/> when the expression can generate text; otherwise <see langword="false"/>.
	/// </returns>
	bool TryValidate(string expression, out string errorMessage);
}