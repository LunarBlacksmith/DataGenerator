using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using DataGenerator.Interfaces;
using DataGenerator.Models;

namespace DataGenerator.Services;

/// <summary>
///	Translates explicit fixed-width segments into generation patterns, .NET regexes and SQL Server predicates.
/// </summary>
public sealed class ExpressionBuilder : IExpressionBuilder
{
	#region FIELDS
	private const int MAXIMUM_FEED_LENGTH = 1024;
	private const int MAXIMUM_DIGITS      = 9;
	#endregion FIELDS

	#region CONSTRUCTOR
	/// <summary>
	///	Creates a stateless segment expression builder.
	/// </summary>
	public ExpressionBuilder()
	{
	}
	#endregion CONSTRUCTOR

	#region METHODS
	#region PUBLIC
	/// <summary>
	///	Builds equivalent whole-value matchers and a generation Pattern from the documented segment grammar.
	/// </summary>
	/// <exception cref="FormatException">
	///	Thrown when the feed or SQL identifier is malformed or outside the supported limits.
	/// </exception>
	public BuiltExpression Build(string feed, string columnName)
	{
		ArgumentNullException.ThrowIfNull(feed);
		ArgumentNullException.ThrowIfNull(columnName);

		if (string.IsNullOrWhiteSpace(feed) || feed.Length > MAXIMUM_FEED_LENGTH)
		{
			throw new FormatException($"Enter nonblank feed text with at most {MAXIMUM_FEED_LENGTH} characters.");
		}

		if (feed.Any(character => character is < ' ' or > '~'))
		{
			throw new FormatException("Only printable ASCII feed characters are supported.");
		}

		if (columnName.Length is 0 or > 128 || columnName.Any(char.IsControl))
		{
			throw new FormatException("The SQL column name must contain 1-128 characters and no control characters.");
		}

		List<Segment> segments = Parse(feed);
		List<string> patterns = [];
		List<string> regexes = [];
		List<string> descriptions = [];
		List<string> conditions = [];
		string column = $"CONVERT(nvarchar(max), [{columnName.Replace("]", "]]", StringComparison.Ordinal)}]) COLLATE Latin1_General_100_BIN2";
		int position = 1;

		foreach (Segment segment in segments)
		{
			string piece = $"SUBSTRING({column}, {position}, {segment.Width})";

			if (segment.Literal is string literal)
			{
				patterns.Add(Quote(literal));
				regexes.Add(Regex.Escape(literal));
				descriptions.Add($"Always the literal text {Quote(literal)}.");
				conditions.Add($"{piece} = N{Quote(literal)}");
			}
			else if (segment.Choices is string[] choices)
			{
				patterns.Add($"ONE_OF({string.Join(", ", choices.Select(Quote))})");
				regexes.Add($"(?:{string.Join("|", choices.Select(Regex.Escape))})");
				descriptions.Add($"{segment.Width} character(s), exactly one of {string.Join(", ", choices.Select(Quote))}.");
				conditions.Add($"{piece} IN ({string.Join(", ", choices.Select(choice => "N" + Quote(choice)))})");
			}
			else
			{
				int maximum = (int)Math.Pow(10, segment.Width) - 1;
				int minimum = segment.Threshold is int threshold ? threshold + 1 : 0;
				patterns.Add($"RAND_NUM({minimum.ToString(CultureInfo.InvariantCulture)}, {maximum.ToString(CultureInfo.InvariantCulture)}, {segment.Width})");
				regexes.Add(segment.Threshold is int bound ? GreaterThanRegex(bound, segment.Width) : Digits(segment.Width));
				descriptions.Add($"{segment.Width} numeric digit(s), zero-padded, from {minimum.ToString(CultureInfo.InvariantCulture).PadLeft(segment.Width, '0')} to {maximum.ToString(CultureInfo.InvariantCulture)}"
					+ (segment.Threshold is int limit ? $" (strictly greater than {limit.ToString(CultureInfo.InvariantCulture)})." : "."));
				conditions.Add($"{piece} NOT LIKE N'%[^0-9]%'");

				if (segment.Threshold is int lowerBound)
				{
					conditions.Add($"TRY_CONVERT(int, {piece}) > {lowerBound.ToString(CultureInfo.InvariantCulture)}");
				}
			}

			position += segment.Width;
		}

		conditions.Insert(0, $"DATALENGTH(CONVERT(nvarchar(max), [{columnName.Replace("]", "]]", StringComparison.Ordinal)}])) = {(position - 1) * 2}");

		return new BuiltExpression
		{
			Feed = feed,
			ColumnName = columnName,
			Description = string.Join(Environment.NewLine, descriptions.Select((description, index) => $"{index + 1}. {description}")),
			Pattern = string.Join(" FOLLOWED BY ", patterns),
			Regex = @"\A" + string.Concat(regexes) + @"\z",
			Sql = string.Join(Environment.NewLine + "AND ", conditions)
		};
	}
	#endregion PUBLIC

	#region PRIVATE
	private static List<Segment> Parse(string feed)
	{
		List<Segment> segments = [];
		StringBuilder literal = new();

		for (int index = 0; index < feed.Length; ++index)
		{
			char current = feed[index];

			if (current == '\\')
			{
				if (++index >= feed.Length || feed[index] is not ('[' or ']' or '\\'))
				{
					throw new FormatException("A backslash must escape [, ] or another backslash.");
				}

				literal.Append(feed[index]);
			}
			else if (current == '[')
			{
				if (literal.Length > 0)
				{
					segments.Add(new Segment(literal.Length, literal.ToString(), null, null));
					literal.Clear();
				}

				int end = feed.IndexOf(']', index + 1);

				if (end < 0)
				{
					throw new FormatException($"Missing closing bracket at position {index + 1}.");
				}

				segments.Add(ParseSegment(feed[(index + 1)..end].Trim()));
				index = end;
			}
			else if (current == ']')
			{
				throw new FormatException($"Unexpected closing bracket at position {index + 1}; escape literal brackets with a backslash.");
			}
			else
			{
				literal.Append(current);
			}
		}

		if (literal.Length > 0)
		{
			segments.Add(new Segment(literal.Length, literal.ToString(), null, null));
		}

		return segments;
	}

	private static Segment       ParseSegment(string text)
	{
		if (text.Contains('>'))
		{
			string[] parts = text.Split('>');

			if (parts.Length != 2 || !IsDigits(parts[0].Trim()) || !IsDigits(parts[1].Trim()))
			{
				throw new FormatException("Use [001 > 050] for a fixed-width numeric value strictly greater than a threshold.");
			}

			string template = parts[0].Trim();
			string bound = parts[1].Trim();
			ValidateWidth(template.Length);

			if (bound.Length != template.Length)
			{
				throw new FormatException("The numeric width template and threshold must have the same number of digits.");
			}

			int threshold = int.Parse(bound, CultureInfo.InvariantCulture);

			if (threshold == (int)Math.Pow(10, template.Length) - 1)
			{
				throw new FormatException("The threshold leaves no possible values at this width.");
			}

			return new Segment(template.Length, null, null, threshold);
		}

		if (text.Contains(','))
		{
			string[] choices = text.Split(',').Select(choice => choice.Trim()).ToArray();

			if (choices.Any(choice => !IsDigits(choice)) || choices.Any(choice => choice.Length != choices[0].Length))
			{
				throw new FormatException("Numeric choices must be nonempty and have equal widths, for example [01, 03, 05].");
			}

			ValidateWidth(choices[0].Length);

			if (choices.Distinct(StringComparer.Ordinal).Count() != choices.Length)
			{
				throw new FormatException("A choice list must not contain duplicate values.");
			}

			return new Segment(choices[0].Length, null, choices, null);
		}

		if (IsDigits(text))
		{
			ValidateWidth(text.Length);
			return new Segment(text.Length, null, null, null);
		}

		if (text.Length > 0 && text.All(char.IsAsciiLetter) && text.Distinct().Count() == text.Length)
		{
			return new Segment(1, null, text.Select(character => character.ToString()).ToArray(), null);
		}

		throw new FormatException($"Unsupported segment [{text}]. Use digits for width, equal-width numeric choices, alphabetic choices, or a > threshold.");
	}

	private static bool          IsDigits(string text) => text.Length > 0 && text.All(char.IsAsciiDigit);

	private static void          ValidateWidth(int width)
	{
		if (width is < 1 or > MAXIMUM_DIGITS)
		{
			throw new FormatException($"Numeric segments must contain 1-{MAXIMUM_DIGITS} digits.");
		}
	}

	private static string        Quote(string text) => "'" + text.Replace("'", "''", StringComparison.Ordinal) + "'";

	private static string        Digits(int width) => width == 0 ? string.Empty : $"[0-9]{{{width}}}";

	private static string        GreaterThanRegex(int threshold, int width)
	{
		string bound = threshold.ToString(CultureInfo.InvariantCulture).PadLeft(width, '0');
		List<string> alternatives = [];

		// Equal-width ASCII numbers compare lexicographically; the first larger digit decides the range.
		for (int index = 0; index < width; ++index)
		{
			int next = bound[index] - '0' + 1;

			if (next <= 9)
			{
				string digit = next == 9 ? "9" : $"[{next}-9]";
				alternatives.Add(bound[..index] + digit + Digits(width - index - 1));
			}
		}

		return $"(?:{string.Join("|", alternatives)})";
	}
	#endregion PRIVATE
	#endregion METHODS

	#region TYPES
	private sealed record Segment(int Width, string? Literal, string[]? Choices, int? Threshold);
	#endregion TYPES
}
