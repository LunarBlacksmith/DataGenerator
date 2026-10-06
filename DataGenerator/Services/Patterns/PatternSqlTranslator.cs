using System.Text;
using DataGenerator.Interfaces;

namespace DataGenerator.Services.Patterns;

/// <summary>
/// Turns a pattern into a T-SQL condition that is true for text the pattern could have produced, ignoring letter case.
/// For example 'S' THEN NUM(digits=5) GREATER THAN 50 matches S00051 to S99999.
/// </summary>
public sealed class PatternSqlTranslator : IPatternSqlTranslator
{
	private const string BINARY_COLLATION = "Latin1_General_100_BIN2";
	private const string NUMBER_TYPE      = "decimal(19, 0)";

	public string ToSqlCondition(string expression, string textExpression)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(textExpression);

		PatternTemplateSet templates  = PatternParser.Parse(expression).ExpandTemplates();
		string             text       = $"(UPPER({textExpression}) COLLATE {BINARY_COLLATION})";
		List<string>       conditions = [.. templates.Templates.Select(template => ToCondition(template, text))];

		return conditions.Count == 1 ? conditions[0] : $"({string.Join(" OR ", conditions)})";
	}

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
