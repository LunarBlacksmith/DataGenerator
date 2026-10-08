using System.Globalization;

namespace DataGenerator.Services.Patterns;

internal enum PatternSegmentKind
{
	Literal        = 0,
	CharacterClass = 1,
	Number         = 2
}

/// <summary>
///	One part of a <see cref="PatternTemplate"/>: fixed text, a run of characters from a class (e.g. 3 letters) or a
///	whole number of a fixed width within a range.
/// </summary>
/// <param name="Kind">
///	Whether the segment is fixed text, a character class or a number.
/// </param>
/// <param name="Text">
///	The text of a literal, or the LIKE class of a character class, e.g. [0-9].
/// </param>
/// <param name="Length">
///	The number of characters the segment covers.
/// </param>
/// <param name="Minimum">
///	The smallest value of a number segment; unused by the other kinds.
/// </param>
/// <param name="Maximum">
///	The largest value of a number segment; unused by the other kinds.
/// </param>
internal sealed record PatternSegment
{
	#region PROPERTIES
	public PatternSegmentKind Kind    { get; init; }
	public string             Text    { get; init; }
	public int                Length  { get; init; }
	public decimal            Minimum { get; init; }
	public decimal            Maximum { get; init; }
	public bool?              IsOdd   { get; init; }
	#endregion PROPERTIES

	/// <summary>
	///	Creates a new <see cref="PatternSegment"/> from the supplied values.
	/// </summary>
	/// <param name="kind">
	///	The value of <see cref="Kind"/>.
	/// </param>
	/// <param name="text">
	///	The value of <see cref="Text"/>.
	/// </param>
	/// <param name="length">
	///	The value of <see cref="Length"/>.
	/// </param>
	/// <param name="minimum">
	///	The value of <see cref="Minimum"/>.
	/// </param>
	/// <param name="maximum">
	///	The value of <see cref="Maximum"/>.
	/// </param>
	/// <param name="isOdd">
	///	The required numeric parity, or <see langword="null"/> to allow both parities.
	/// </param>
	public PatternSegment(
		PatternSegmentKind kind,
		string             text,
		int                length,
		decimal            minimum = 0,
		decimal            maximum = 0,
		bool?              isOdd   = null
	)
	{
		Kind    = kind;
		Text    = text;
		Length  = length;
		Minimum = minimum;
		Maximum = maximum;
		IsOdd   = isOdd;
	}
}

/// <summary>
///	One fixed-length shape of the values a pattern produces, e.g. 'S', then 5 digits from 51 to 99999, then 1 or 2.
/// </summary>
internal sealed class PatternTemplate
{
	#region PROPERTIES
	public IReadOnlyList<PatternSegment> Segments { get; }
	#endregion PROPERTIES

	#region CONSTRUCTOR
	/// <summary>
	///	Creates one fixed-length output shape from ordered literal, character-class and number segments.
	/// </summary>
	/// <param name="segments">
	///	The ordered segments that make up the template.
	/// </param>
	public PatternTemplate(IReadOnlyList<PatternSegment> segments)
	{
		Segments = segments;
	}
	#endregion CONSTRUCTOR

	#region METHODS
	/// <summary>
	///	Concatenates another template after this one, merging adjacent literal segments.
	/// </summary>
	/// <param name="other">
	///	The template to append.
	/// </param>
	/// <returns>
	///	A combined template containing this template followed by <paramref name="other"/>.
	/// </returns>
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
	#endregion METHODS
}

/// <summary>
///	Every shape of value a pattern can produce. The number of shapes is capped, because each one becomes a condition
///	in the SQL that finds matching values.
/// </summary>
internal sealed class PatternTemplateSet
{
	#region FIELDS
	#region PUBLIC
	public const int MAXIMUM_TEMPLATES = 256;

	public static readonly PatternTemplateSet EMPTY_TEXT;
	#endregion PUBLIC

	#region PRIVATE
	private const string DIGIT_CLASS = "[0-9]";
	#endregion PRIVATE
	#endregion FIELDS

	#region PROPERTIES
	public IReadOnlyList<PatternTemplate> Templates { get; }
	#endregion PROPERTIES

	#region CONSTRUCTOR
	/// <summary>
	///	Sets the default values of the static fields and properties of <see cref="PatternTemplateSet"/>.
	/// </summary>
	static PatternTemplateSet()
	{
		EMPTY_TEXT = new([new PatternTemplate([])]);
	}

	/// <summary>
	///	Creates a set of possible output templates, enforcing the SQL translation shape limit.
	/// </summary>
	/// <param name="templates">
	///	The templates in the set.
	/// </param>
	/// <exception cref="PatternSyntaxException">
	///	Thrown when the set contains more templates than SQL matching allows.
	/// </exception>
	private PatternTemplateSet(IReadOnlyList<PatternTemplate> templates)
	{
		if (templates.Count > MAXIMUM_TEMPLATES)
		{
			throw CreateTooManyShapesException();
		}

		Templates = templates;
	}
	#endregion CONSTRUCTOR

	#region METHODS
	#region PUBLIC
	/// <summary>
	///	Creates a template set for one literal value.
	/// </summary>
	/// <param name="text">
	///	The literal text to match.
	/// </param>
	/// <returns>
	///	A literal template set, or <see cref="EMPTY_TEXT"/> when <paramref name="text"/> is empty.
	/// </returns>
	public static PatternTemplateSet FromLiteral(string text)
		=>
			text.Length == 0
				? EMPTY_TEXT
				: new PatternTemplateSet([new PatternTemplate([new PatternSegment(PatternSegmentKind.Literal, text, text.Length)])]);

	/// <summary>
	///	Combines several alternative template sets into one set.
	/// </summary>
	/// <param name="sets">
	///	The template sets to combine.
	/// </param>
	/// <returns>
	///	A template set containing every template from every supplied set.
	/// </returns>
	/// <exception cref="PatternSyntaxException">
	///	Thrown when the combined set contains too many templates.
	/// </exception>
	public static PatternTemplateSet Union(IReadOnlyList<PatternTemplateSet> sets)
		=> new PatternTemplateSet([.. sets.SelectMany(set => set.Templates)]);

	/// <summary>
	///	Creates templates for a LIKE character class repeated across a range of lengths.
	/// </summary>
	/// <param name="likeClass">
	///	The SQL LIKE character class, such as [A-Z] or [0-9].
	/// </param>
	/// <param name="minimumLength">
	///	The shortest allowed run length.
	/// </param>
	/// <param name="maximumLength">
	///	The longest allowed run length.
	/// </param>
	/// <returns>
	///	A template set containing one template for each length in the range.
	/// </returns>
	/// <exception cref="PatternSyntaxException">
	///	Thrown when the length range creates too many templates.
	/// </exception>
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
	///	Creates templates for whole numbers in a range, either fixed-width padded or split by unpadded digit width.
	/// </summary>
	/// <param name="minimum">
	///	The inclusive lower bound.
	/// </param>
	/// <param name="maximum">
	///	The inclusive upper bound.
	/// </param>
	/// <param name="digits">
	///	The fixed padding width, or 0 for unpadded numbers.
	/// </param>
	/// <param name="functionName">
	///	The function name used when reporting unsupported negative ranges.
	/// </param>
	/// <param name="isOdd">
	///	The required numeric parity, or <see langword="null"/> to allow both parities.
	/// </param>
	/// <returns>
	///	A template set whose number segments include any needed numeric range checks.
	/// </returns>
	/// <exception cref="PatternSyntaxException">
	///	Thrown when the range contains negative numbers or produces too many templates.
	/// </exception>
	public static PatternTemplateSet ForNumbers(long minimum, long maximum, int digits, string functionName, bool? isOdd = null)
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
			return new PatternTemplateSet([new PatternTemplate([new PatternSegment(PatternSegmentKind.Number, DIGIT_CLASS, digits, minimum, maximum, isOdd)])]);
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
					new PatternSegment(PatternSegmentKind.Number, DIGIT_CLASS, width, Math.Max(minimum, smallest), Math.Min(maximum, largest), isOdd)
				])
			);
		}

		return new PatternTemplateSet(templates);
	}

	/// <summary>
	///	Calculates 10 raised to a non-negative exponent using decimal arithmetic.
	/// </summary>
	/// <param name="exponent">
	///	The exponent to apply.
	/// </param>
	/// <returns>
	///	The decimal value of 10 to the requested power.
	/// </returns>
	public static decimal Pow10(int exponent)
	{
		decimal value = 1;

		for (int index = 0; index < exponent; ++index)
		{
			value *= 10;
		}

		return value;
	}

	/// <summary>
	///	Formats a decimal whole-number bound for use in generated SQL.
	/// </summary>
	/// <param name="value">
	///	The value to format.
	/// </param>
	/// <returns>
	///	The invariant-culture whole-number text without decimal places.
	/// </returns>
	public static string FormatNumber(decimal value) => value.ToString("0", CultureInfo.InvariantCulture);

	/// <summary>
	///	Concatenates each template in this set with each template in another set.
	/// </summary>
	/// <param name="other">
	///	The template set to append.
	/// </param>
	/// <returns>
	///	A template set containing every pairwise concatenation.
	/// </returns>
	/// <exception cref="PatternSyntaxException">
	///	Thrown when the pairwise product would create too many templates.
	/// </exception>
	public PatternTemplateSet Then(PatternTemplateSet other)
	{
		if ((long)Templates.Count * other.Templates.Count > MAXIMUM_TEMPLATES)
		{
			throw CreateTooManyShapesException();
		}

		return new PatternTemplateSet(
			[..
				Templates
					.SelectMany(
						first =>
							other
								.Templates
								.Select(first.Then)
					)
			]
		);
	}
	#endregion PUBLIC

	#region PRIVATE
	/// <summary>
	///	Builds the common error used when a pattern would require too many SQL matching shapes.
	/// </summary>
	/// <returns>
	///	A syntax exception explaining that the pattern is too complex for value matching.
	/// </returns>
	private static PatternSyntaxException CreateTooManyShapesException()
		=> new PatternSyntaxException(
			$"The pattern produces values of more than {MAXIMUM_TEMPLATES} different shapes, which is too many to find values with. "
			+ "Use fewer OR choices, or narrower REPEATED counts and lengths.",
			1
		);
	#endregion PRIVATE
	#endregion METHODS
}
