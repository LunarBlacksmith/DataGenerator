namespace DataGenerator.Interfaces;

/// <summary>
/// Generates text from the plain-English pattern language, for example
/// <c>P FOLLOWED BY SEQ(1-1000) FOLLOWED BY (X OR Y) FOLLOWED BY RAND_NUM(0, 99, 2)</c>.
/// See Docs/PatternLanguage.md for the full reference.
/// </summary>
public interface IPatternValueGenerator
{
	/// <summary>
	/// Generates the value for the zero-based <paramref name="rowIndex"/> within a row set.
	/// Throws <see cref="Patterns.PatternSyntaxException"/> when the expression is invalid.
	/// </summary>
	string Generate(string expression, long rowIndex);

	/// <summary>
	/// Generates a value whose COL(name) calls are answered by <paramref name="columnValues"/>, which returns the
	/// value of another column of the same row as text.
	/// </summary>
	/// <param name="rowCount">The number of rows in the row set, used by LAST(...); <see langword="null"/> when unknown.</param>
	string Generate(string expression, long rowIndex, long? rowCount, Func<string, string>? columnValues);

	/// <summary>
	/// The names of the columns used with COL(...) in the expression, or none when the expression is invalid.
	/// </summary>
	IReadOnlyList<string> GetColumnReferences(string expression);

	bool TryValidate(string expression, out string errorMessage);
}