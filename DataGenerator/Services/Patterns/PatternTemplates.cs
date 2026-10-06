using System.Globalization;

namespace DataGenerator.Services.Patterns;

internal enum PatternSegmentKind
{
	Literal        = 0,
	CharacterClass = 1,
	Number         = 2
}

/// <summary>
/// One part of a <see cref="PatternTemplate"/>: fixed text, a run of characters from a class (e.g. 3 letters) or a
/// whole number of a fixed width within a range.
/// </summary>
/// <param name="Text">The text of a literal, or the LIKE class of a character class, e.g. [0-9].</param>
/// <param name="Length">The number of characters the segment covers.</param>
internal sealed record PatternSegment(PatternSegmentKind Kind, string Text, int Length, decimal Minimum = 0, decimal Maximum = 0);

/// <summary>
/// One fixed-length shape of the values a pattern produces, e.g. 'S', then 5 digits from 51 to 99999, then 1 or 2.
/// </summary>
internal sealed class PatternTemplate
{
	public PatternTemplate(IReadOnlyList<PatternSegment> segments)
	{
		Segments = segments;
	}

	public IReadOnlyList<PatternSegment> Segments { get; }

	public PatternTemplate Then(PatternTemplate other)
	{
		List<PatternSegment> segments = [.. Segments];

		foreach (PatternSegment segment in other.Segments)
		{
			if (segments.Count > 0
				&& segment.Kind == PatternSegmentKind.Literal
				&& segments[^1].Kind == PatternSegmentKind.Literal)
			{
				string text = segments[^1].Text + segment.Text;

				segments[^1] = new PatternSegment(PatternSegmentKind.Literal, text, text.Length);
			}
			else
			{
				segments.Add(segment);
			}
		}

		return new PatternTemplate(segments);
	}
}

/// <summary>
/// Every shape of value a pattern can produce. The number of shapes is capped, because each one becomes a condition
/// in the SQL that finds matching values.
/// </summary>
internal sealed class PatternTemplateSet
{
	public const int MAXIMUM_TEMPLATES = 256;

	public static readonly PatternTemplateSet EMPTY_TEXT = new([new PatternTemplate([])]);

	private const string DIGIT_CLASS = "[0-9]";

	private PatternTemplateSet(IReadOnlyList<PatternTemplate> templates)
	{
		if (templates.Count > MAXIMUM_TEMPLATES)
		{
			throw CreateTooManyShapesException();
		}

		Templates = templates;
	}

	public IReadOnlyList<PatternTemplate> Templates { get; }

	public static PatternTemplateSet FromLiteral(string text)
		=> text.Length == 0
			? EMPTY_TEXT
			: new PatternTemplateSet([new PatternTemplate([new PatternSegment(PatternSegmentKind.Literal, text, text.Length)])]);

	public static PatternTemplateSet Union(IReadOnlyList<PatternTemplateSet> sets)
		=> new PatternTemplateSet([.. sets.SelectMany(set => set.Templates)]);

	/// <summary>
	/// Characters from a LIKE class such as [A-Z], repeated from <paramref name="minimumLength"/> to
	/// <paramref name="maximumLength"/> times.
	/// </summary>
	public static PatternTemplateSet ForCharacters(string likeClass, int minimumLength, int maximumLength)
	{
		List<PatternTemplate> templates = [];

		for (int length = minimumLength; length <= maximumLength && templates.Count <= MAXIMUM_TEMPLATES; ++length)
		{
			templates.Add(
				new PatternTemplate(length == 0 ? [] : [new PatternSegment(PatternSegmentKind.CharacterClass, likeClass, length)])
			);
		}

		return new PatternTemplateSet(templates);
	}

	/// <summary>
	/// Whole numbers from <paramref name="minimum"/> to <paramref name="maximum"/>, zero-padded to
	/// <paramref name="digits"/> or, when it is 0, written without leading zeros.
	/// </summary>
	public static PatternTemplateSet ForNumbers(long minimum, long maximum, int digits, string functionName)
	{
		if (minimum < 0)
		{
			throw new PatternSyntaxException(
				$"{functionName}(...) with negative numbers cannot be used to find existing values. Use a range from 0 upwards.",
				1
			);
		}

		if (digits > 0)
		{
			return new PatternTemplateSet([new PatternTemplate([new PatternSegment(PatternSegmentKind.Number, DIGIT_CLASS, digits, minimum, maximum)])]);
		}

		List<PatternTemplate> templates = [];
		int                   fewest    = PatternNumberFormatter.CountDigits(minimum);
		int                   most      = PatternNumberFormatter.CountDigits(maximum);

		for (int width = fewest; width <= most; ++width)
		{
			decimal smallest = width == 1 ? 0 : Pow10(width - 1);
			decimal largest  = Pow10(width) - 1;

			templates.Add(
				new PatternTemplate([
					new PatternSegment(PatternSegmentKind.Number, DIGIT_CLASS, width, Math.Max(minimum, smallest), Math.Min(maximum, largest))
				])
			);
		}

		return new PatternTemplateSet(templates);
	}

	public PatternTemplateSet Then(PatternTemplateSet other)
	{
		if ((long)Templates.Count * other.Templates.Count > MAXIMUM_TEMPLATES)
		{
			throw CreateTooManyShapesException();
		}

		return new PatternTemplateSet([.. Templates.SelectMany(first => other.Templates.Select(first.Then))]);
	}

	public static decimal Pow10(int exponent)
	{
		decimal value = 1;

		for (int index = 0; index < exponent; ++index)
		{
			value *= 10;
		}

		return value;
	}

	public static string FormatNumber(decimal value) => value.ToString("0", CultureInfo.InvariantCulture);

	private static PatternSyntaxException CreateTooManyShapesException()
		=> new PatternSyntaxException(
			$"The pattern produces values of more than {MAXIMUM_TEMPLATES} different shapes, which is too many to find values with. "
			+ "Use fewer OR choices, or narrower REPEATED counts and lengths.",
			1
		);
}
