using System.Text;
using DataGenerator.Interfaces;

namespace DataGenerator.Services.Patterns;

/// <summary>
///	Turns a pattern into a T-SQL condition that is true for text the pattern could have produced, ignoring letter case.
///	For example 'S' THEN NUM(digits=5) GREATER THAN 50 matches S00051 to S99999.
/// </summary>
public sealed class PatternSqlTranslator : IPatternSqlTranslator
{
	private const string BINARY_COLLATION = "Latin1_General_100_BIN2";
	private const string NUMBER_TYPE      = "decimal(19, 0)";

	/// <summary>
	///	Converts a pattern expression into a T-SQL condition that matches every recognisable value the pattern can produce.
	/// </summary>
	/// <param name="expression">
	///	The pattern expression to translate.
	/// </param>
	/// <param name="textExpression">
	///	The SQL expression that yields the text value to test.
	/// </param>
	/// <returns>
	///	A SQL condition that compares <paramref name="textExpression"/> with the pattern's possible shapes.
	/// </returns>
	/// <exception cref="ArgumentException">
	///	Thrown when <paramref name="textExpression"/> is <see langword="null"/>, empty or whitespace.
	/// </exception>
	/// <exception cref="PatternSyntaxException">
	///	Thrown when the pattern is invalid or contains values that cannot be translated to SQL matching.
	/// </exception>
	public string ToSqlCondition(string expression, string textExpression)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(textExpression);

		PatternTemplateSet templates  = PatternParser.Parse(expression).ExpandTemplates();
		string             text       = $"(UPPER({textExpression}) COLLATE {BINARY_COLLATION})";
		List<string>       conditions = [.. templates.Templates.Select(template => ToCondition(template, text))];

		return conditions.Count == 1 ? conditions[0] : $"({string.Join(" OR ", conditions)})";
	}

	/// <summary>
	///	Checks whether a pattern can be translated into SQL matching templates.
	/// </summary>
	/// <param name="expression">
	///	The pattern expression to validate.
	/// </param>
	/// <param name="errorMessage">
	///	The syntax or translation error when validation fails; otherwise an empty string.
	/// </param>
	/// <returns>
	///	<see langword="true"/> when the pattern can be translated; otherwise <see langword="false"/>.
	/// </returns>
	public bool TryValidate(string expression, out string errorMessage)
	{
		try
		{
			_            = PatternParser.Parse(expression).ExpandTemplates();
			errorMessage = string.Empty;
			return true;
		}
		catch (PatternSyntaxException exception)
		{
			errorMessage = exception.Message;
			return false;
		}
	}

	/// <summary>
	///	Builds the SQL condition for one fixed-length pattern template.
	/// </summary>
	/// <param name="template">
	///	The template whose segments should be converted to LIKE and numeric range checks.
	/// </param>
	/// <param name="text">
	///	The collated SQL text expression being tested.
	/// </param>
	/// <returns>
	///	A SQL condition for the single template.
	/// </returns>
	private static string ToCondition(PatternTemplate template, string text)
	{
		StringBuilder likePattern = new();
		List<string>  conditions  = [];
		int           offset      = 1;

		foreach (PatternSegment segment in template.Segments)
		{
			switch (segment.Kind)
			{
				case PatternSegmentKind.Literal:
				{
					AppendEscaped(likePattern, segment.Text.ToUpperInvariant());
					break;
				}

				case PatternSegmentKind.Number:
				{
					for (int index = 0; index < segment.Length; ++index)
					{
						_ = likePattern.Append(segment.Text);
					}

					if (segment.Minimum > 0 || segment.Maximum < PatternTemplateSet.Pow10(segment.Length) - 1)
					{
						conditions.Add(
							$"TRY_CONVERT({NUMBER_TYPE}, SUBSTRING({text}, {offset}, {segment.Length})) BETWEEN "
							+ $"{PatternTemplateSet.FormatNumber(segment.Minimum)} AND {PatternTemplateSet.FormatNumber(segment.Maximum)}"
						);
					}

					break;
				}

				default:
				{
					for (int index = 0; index < segment.Length; ++index)
					{
						_ = likePattern.Append(segment.Text);
					}

					break;
				}
			}

			offset += segment.Length;
		}

		conditions.Insert(0, $"{text} LIKE N'{likePattern.Replace("'", "''")}'");
		return conditions.Count == 1 ? conditions[0] : $"({string.Join(" AND ", conditions)})";
	}

	/// <summary>
	///	Appends literal text to a LIKE pattern, escaping characters that have LIKE wildcard or bracket meaning.
	/// </summary>
	/// <param name="builder">
	///	The LIKE pattern builder.
	/// </param>
	/// <param name="text">
	///	The literal text to append.
	/// </param>
	private static void AppendEscaped(StringBuilder builder, string text)
	{
		foreach (char character in text)
		{
			_ = character switch
			{
				'[' => builder.Append("[[]"),
				'%' => builder.Append("[%]"),
				'_' => builder.Append("[_]"),
				_   => builder.Append(character)
			};
		}
	}
}
