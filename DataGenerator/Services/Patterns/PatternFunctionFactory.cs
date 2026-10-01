using System.Globalization;

namespace DataGenerator.Services.Patterns;

internal static class PatternFunctionFactory
{
	public static readonly IReadOnlyList<string> FUNCTION_NAMES =
	[
		"SEQ",
		"RAND_NUMBER",
		"RAND_DECIMAL",
		"RAND_LETTERS",
		"RAND_DIGITS",
		"RAND_ALPHANUM",
		"RAND_DATE",
		"ONE_OF",
		"GUID"
	];

	private const int    MAXIMUM_TEXT_LENGTH = 1000;
	private const int    MAXIMUM_DECIMALS    = 10;
	private const int    MAXIMUM_DIGITS      = 19;
	private const int    NO_PADDING          = 0;
	private const string DEFAULT_DATE_FORMAT = "yyyy-MM-dd";
	private const string UPPER_LETTERS       = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
	private const string LOWER_LETTERS       = "abcdefghijklmnopqrstuvwxyz";
	private const string DIGITS              = "0123456789";

	public static PatternNode Create(PatternToken nameToken, IReadOnlyList<PatternArgument> arguments)
	{
		string                functionName = nameToken.Text.ToUpperInvariant();
		PatternArgumentBinder binder       = new PatternArgumentBinder(functionName, arguments, nameToken.Position);

		return functionName switch
		{
			"SEQ" or "SEQUENCE" => CreateSequence(binder),
			"RAND_NUMBER"       => CreateRandomNumber(binder),
			"RAND_DECIMAL"      => CreateRandomDecimal(binder),
			"RAND_LETTERS"      => CreateRandomText(binder, "RAND_LETTERS(5)", includeLetters: true, includeDigits: false),
			"RAND_DIGITS"       => CreateRandomText(binder, "RAND_DIGITS(4)", includeLetters: false, includeDigits: true),
			"RAND_ALPHANUM"     => CreateRandomText(binder, "RAND_ALPHANUM(8)", includeLetters: true, includeDigits: true),
			"RAND_DATE"         => CreateRandomDate(binder),
			"ONE_OF"            => CreateOneOf(binder),
			"GUID"              => CreateGuid(binder),
			_                   => throw new PatternSyntaxException(
				$"Unknown function '{nameToken.Text}'. Available functions: {string.Join(", ", FUNCTION_NAMES)}. "
					+ "To use the word as text, put it in quotes.",
				nameToken.Position
			)
		};
	}

	private static PatternNode CreateSequence(PatternArgumentBinder binder)
	{
		(decimal rangeStart, decimal rangeEnd) = binder.RequireRange("SEQ(1-1000)");

		long minimum = binder.ToWholeNumber(rangeStart, "the range start");
		long maximum = binder.ToWholeNumber(rangeEnd, "the range end");
		long start   = binder.ToWholeNumber(binder.OptionalNumber("start") ?? minimum, "start");
		long step    = binder.ToWholeNumber(binder.OptionalNumber("step") ?? 1, "step");
		int  digits  = ReadDigits(binder, minimum, maximum, Math.Max(PatternNumberFormatter.CountDigits(minimum), PatternNumberFormatter.CountDigits(maximum)));

		if (step == 0)
		{
			throw binder.Error("step cannot be 0.");
		}

		if (start < minimum || start > maximum)
		{
			throw binder.Error($"start ({start}) must be inside the range {minimum} to {maximum}.");
		}

		binder.EnsureComplete("range", "start", "step", "digits");
		return new SequencePatternNode(minimum, maximum, start, step, digits);
	}

	private static PatternNode CreateRandomNumber(PatternArgumentBinder binder)
	{
		(decimal rangeStart, decimal rangeEnd) = binder.RequireRange("RAND_NUMBER(0, 99)");

		long minimum = binder.ToWholeNumber(rangeStart, "the range start");
		long maximum = binder.ToWholeNumber(rangeEnd, "the range end");
		int  digits  = ReadDigits(binder, minimum, maximum, NO_PADDING);

		binder.EnsureComplete("range", "digits");
		return new RandomNumberPatternNode(minimum, maximum, digits);
	}

	private static PatternNode CreateRandomDecimal(PatternArgumentBinder binder)
	{
		(decimal minimum, decimal maximum) = binder.RequireRange("RAND_DECIMAL(0, 100)");

		_ = binder.ToWholeNumber(decimal.Truncate(minimum), "the range start");
		_ = binder.ToWholeNumber(decimal.Truncate(maximum), "the range end");

		int decimals = binder.ToCount(binder.OptionalNumber("decimals") ?? 2, "decimals", 0, MAXIMUM_DECIMALS);

		binder.EnsureComplete("range", "decimals");
		return new RandomDecimalPatternNode(minimum, maximum, decimals);
	}

	private static PatternNode CreateRandomText(PatternArgumentBinder binder, string example, bool includeLetters, bool includeDigits)
	{
		(decimal lengthStart, decimal lengthEnd) = binder.RequireLength(example);

		int    minimumLength = binder.ToCount(lengthStart, "the length", 0, MAXIMUM_TEXT_LENGTH);
		int    maximumLength = binder.ToCount(lengthEnd, "the length", 0, MAXIMUM_TEXT_LENGTH);
		string alphabet      = includeDigits ? DIGITS : string.Empty;

		if (includeLetters)
		{
			string letterCase = (binder.OptionalText("case") ?? "UPPER").ToUpperInvariant();

			alphabet += letterCase switch
			{
				"UPPER" => UPPER_LETTERS,
				"LOWER" => LOWER_LETTERS,
				"MIXED" => UPPER_LETTERS + LOWER_LETTERS,
				_       => throw binder.Error($"case must be UPPER, LOWER or MIXED (not '{letterCase}').")
			};

			binder.EnsureComplete("length", "case");
		}
		else
		{
			binder.EnsureComplete("length");
		}

		return new RandomTextPatternNode(alphabet, minimumLength, maximumLength);
	}

	private static PatternNode CreateRandomDate(PatternArgumentBinder binder)
	{
		string   minimumText = binder.RequireText("min", "RAND_DATE('2020-01-01', '2024-12-31')");
		string   maximumText = binder.RequireText("max", "RAND_DATE('2020-01-01', '2024-12-31')");
		string   format      = binder.OptionalText("format") ?? DEFAULT_DATE_FORMAT;
		DateTime minimum     = ParseDate(binder, minimumText);
		DateTime maximum     = ParseDate(binder, maximumText);

		if (maximum.TimeOfDay == TimeSpan.Zero && maximum.Date < DateTime.MaxValue.Date)
		{
			maximum = maximum.AddDays(1).AddTicks(-1);
		}

		if (minimum > maximum)
		{
			throw binder.Error($"the first date ({minimumText}) must not be after the second date ({maximumText}).");
		}

		try
		{
			_ = minimum.ToString(format, CultureInfo.InvariantCulture);
		}
		catch (FormatException)
		{
			throw binder.Error($"'{format}' is not a valid date format. Try 'yyyy-MM-dd' or 'dd/MM/yyyy HH:mm'.");
		}

		binder.EnsureComplete("min", "max", "format");
		return new RandomDatePatternNode(minimum, maximum, format);
	}

	private static PatternNode CreateOneOf(PatternArgumentBinder binder)
	{
		IReadOnlyList<PatternArgument> values = binder.TakeRemainingPositional();

		if (values.Count == 0)
		{
			throw binder.Error("at least one value is required, e.g. ONE_OF('Red', 'Green', 'Blue').");
		}

		binder.EnsureComplete("values");
		return new ChoicePatternNode([.. values.Select(value => new LiteralPatternNode(value.Text))]);
	}

	private static PatternNode CreateGuid(PatternArgumentBinder binder)
	{
		string letterCase = (binder.OptionalText("case") ?? "UPPER").ToUpperInvariant();

		if (letterCase is not ("UPPER" or "LOWER"))
		{
			throw binder.Error($"case must be UPPER or LOWER (not '{letterCase}').");
		}

		binder.EnsureComplete("case");
		return new GuidPatternNode(letterCase == "UPPER");
	}

	/// <summary>
	/// Reads the optional digits argument: the fixed width numbers are zero-padded to, or 0 for no padding.
	/// </summary>
	private static int ReadDigits(PatternArgumentBinder binder, long minimum, long maximum, int defaultDigits)
	{
		decimal? requestedDigits = binder.OptionalNumber("digits");

		if (!requestedDigits.HasValue)
		{
			return defaultDigits;
		}

		int digits = binder.ToCount(requestedDigits.Value, "digits", NO_PADDING, MAXIMUM_DIGITS);

		if (digits == NO_PADDING)
		{
			return digits;
		}

		int requiredDigits = Math.Max(PatternNumberFormatter.CountDigits(minimum), PatternNumberFormatter.CountDigits(maximum));

		if (digits < requiredDigits)
		{
			throw binder.Error(
				$"digits is {digits}, but {Math.Max(Math.Abs(minimum), Math.Abs(maximum))} needs {requiredDigits} digits. "
				+ $"Use digits={requiredDigits} or more, digits=0 for no padding, or narrow the range."
			);
		}

		return digits;
	}

	private static DateTime ParseDate(PatternArgumentBinder binder, string text)
	{
		return DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out DateTime value)
			? value
			: throw binder.Error($"'{text}' is not a valid date. Use the form 'yyyy-MM-dd', e.g. '2024-12-31'.");
	}
}