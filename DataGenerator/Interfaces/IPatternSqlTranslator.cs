namespace DataGenerator.Interfaces;

/// <summary>
/// Finds values that a pattern could have produced, so a pattern can select existing rows, e.g. the ShirtIDs that look
/// like 'S' THEN NUM(digits=5) GREATER THAN 50. See Docs/PatternLanguage.md for the functions that can be used.
/// </summary>
public interface IPatternSqlTranslator
{
	/// <summary>
	/// A T-SQL condition that is true when <paramref name="textExpression"/> (an nvarchar expression) matches the
	/// pattern, ignoring letter case. Throws <see cref="Patterns.PatternSyntaxException"/> when the pattern is invalid
	/// or uses functions whose values cannot be recognised, such as RAND_DATE.
	/// </summary>
	string ToSqlCondition(string expression, string textExpression);

	bool TryValidate(string expression, out string errorMessage);
}
