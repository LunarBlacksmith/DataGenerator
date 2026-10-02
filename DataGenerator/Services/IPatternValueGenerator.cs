namespace DataGenerator.Services;

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

	bool TryValidate(string expression, out string errorMessage);
}