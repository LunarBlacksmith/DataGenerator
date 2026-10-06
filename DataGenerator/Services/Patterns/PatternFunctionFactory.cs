using System.Globalization;
using System.Text;

namespace DataGenerator.Services.Patterns;

internal static class PatternFunctionFactory
{
	// Every function must be described in PatternLanguageReference, which the reference window and suggestions use.
	public static readonly IReadOnlyList<string> FUNCTION_NAMES = [.. PatternLanguageReference.FUNCTIONS.Select(entry => entry.Name)];

	private static readonly string[] FUNCTION_ALIASES = ["SEQUENCE", "COLUMN"];

	private const int    MAXIMUM_TEXT_LENGTH    = 1000;
	private const int    MAXIMUM_DECIMALS       = 10;
	private const int    MAXIMUM_DIGITS         = 19;
	private const int    MAXIMUM_NUMBER_DIGITS  = 18;
	private const int    MAXIMUM_POSITION_ROWS  = 1000;
	private const int    NO_PADDING             = 0;
	private const long   DEFAULT_NUMBER_MAXIMUM = 999_999_999;
	private const string PAD_CHARACTER          = "0";
	private const string DEFAULT_DATE_FORMAT      = "yyyy-MM-dd";
	private const string DEFAULT_DATE_TIME_FORMAT = "yyyy-MM-dd HH:mm:ss";
	private const string TIME_NOW                 = "NOW";
	private const string TIME_ANY                 = "ANY";
	private const string UPPER_LETTERS            = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
	private const string LOWER_LETTERS            = "abcdefghijklmnopqrstuvwxyz";
	private const string DIGITS                   = "0123456789";

	public static PatternNode Create(PatternToken nameToken, IReadOnlyList<PatternArgument> arguments)
	{
		string functionName = nameToken.Text.ToUpperInvariant();

		if (!FUNCTION_NAMES.Contains(functionName) && !FUNCTION_ALIASES.Contains(functionName))
		{
			throw new PatternSyntaxException(
				$"Unknown function '{nameToken.Text}'. Available functions: {string.Join(", ", FUNCTION_NAMES)}. "
					+ "To use the word as text, put it in quotes.",
				nameToken.Position
			);
		}

		if (arguments.Any(argument => argument.Kind == PatternArgumentKind.Expression))
		{
			return CreateDynamic(nameToken, arguments);
		}

		PatternArgumentBinder binder = new(functionName, arguments, nameToken.Position);

		PatternNode node = functionName switch
		{
			"SEQ" or "SEQUENCE" => CreateSequence(binder),
			"RAND_NUM"          => CreateRandomNumber(binder, "RAND_NUM(0, 99)"),
			"NUM"               => CreateNumber(binder),
			"RAND_DECIMAL"      => CreateRandomDecimal(binder),
			"RAND_LETTERS"      => CreateRandomText(binder, "RAND_LETTERS(5)", includeLetters: true, includeDigits: false),
			"RAND_DIGITS"       => CreateRandomText(binder, "RAND_DIGITS(4)", includeLetters: false, includeDigits: true),
			"RAND_ALPHANUM"     => CreateRandomText(binder, "RAND_ALPHANUM(8)", includeLetters: true, includeDigits: true),
			"RAND_DATE"         => CreateRandomDate(binder),
			"TODAY"             => CreateToday(binder),
			"ONE_OF"            => CreateOneOf(binder),
			"CYCLE"             => CreateCycle(binder),
			"FIRST"             => CreateRowPosition(binder, functionName, fromEnd: false),
			"LAST"              => CreateRowPosition(binder, functionName, fromEnd: true),
			"ROW"               => CreateRowNumber(binder),
			"UPPER"             => CreateCase(binder, functionName, upperCase: true),
			"LOWER"             => CreateCase(binder, functionName, upperCase: false),
			"LEFT"              => CreateTextPart(binder, functionName, fromEnd: false),
			"RIGHT"             => CreateTextPart(binder, functionName, fromEnd: true),
			"PAD"               => CreatePad(binder),
			"GUID"              => CreateGuid(binder),
			_                   => CreateColumnReference(binder)
		};

		node.FunctionName = functionName;
		return node;
	}

	/// <summary>
	/// A function whose arguments include nested function calls. When the nested calls do not depend on other columns,
	/// they are tried once now so that mistakes such as a wrong parameter are reported while the pattern is typed.
	/// </summary>
	private static PatternNode CreateDynamic(PatternToken nameToken, IReadOnlyList<PatternArgument> arguments)
	{
		DynamicFunctionPatternNode node              = new(nameToken, arguments) { FunctionName = nameToken.Text.ToUpperInvariant() };
		List<string>               referencedColumns = [];

		node.CollectColumnReferences(referencedColumns);

		if (referencedColumns.Count == 0)
		{
			try
			{
				node.Append(new StringBuilder(), new PatternContext(0, null, new Random(0), TimeProvider.System));
			}
			catch (InvalidOperationException exception)
			{
				throw new PatternSyntaxException(exception.Message, nameToken.Position);
			}
		}

		return node;
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

	private static PatternNode CreateRandomNumber(PatternArgumentBinder binder, string example)
	{
		(decimal rangeStart, decimal rangeEnd) = binder.RequireRange(example);

		long minimum = binder.ToWholeNumber(rangeStart, "the range start");
		long maximum = binder.ToWholeNumber(rangeEnd, "the range end");
		int  digits  = ReadDigits(binder, minimum, maximum, NO_PADDING);

		binder.EnsureComplete("range", "digits");
		return new RandomNumberPatternNode(minimum, maximum, digits);
	}

	/// <summary>
	/// NUM(min, max, digits) with every parameter optional: without a range, NUM(digits=5) is 00000 to 99999 and NUM()
	/// is 0 to 999,999,999.
	/// </summary>
	private static PatternNode CreateNumber(PatternArgumentBinder binder)
	{
		if (binder.HasRangeArgument())
		{
			return CreateRandomNumber(binder, "NUM(0, 99)");
		}

		decimal? requestedDigits = binder.OptionalNumber("digits");
		int      digits          = requestedDigits.HasValue
			? binder.ToCount(requestedDigits.Value, "digits", NO_PADDING, MAXIMUM_NUMBER_DIGITS)
			: NO_PADDING;
		long     maximum         = digits == NO_PADDING ? DEFAULT_NUMBER_MAXIMUM : (long)PatternTemplateSet.Pow10(digits) - 1;

		binder.EnsureComplete("min", "max", "digits");
		return new RandomNumberPatternNode(0, maximum, digits);
	}

	private static PatternNode CreateRowNumber(PatternArgumentBinder binder)
	{
		int digits = binder.ToCount(binder.OptionalNumber("digits") ?? NO_PADDING, "digits", NO_PADDING, MAXIMUM_DIGITS);

		binder.EnsureComplete("digits");
		return new RowNumberPatternNode(digits);
	}

	private static PatternNode CreateCycle(PatternArgumentBinder binder)
	{
		IReadOnlyList<string> values = ReadValues(binder, binder.TakeRemainingPositional());

		if (values.Count == 0)
		{
			throw binder.Error("at least one value is required, e.g. CYCLE('A', 'B', 'C').");
		}

		binder.EnsureComplete("values");
		return new CyclePatternNode(values);
	}

	/// <summary>
	/// FIRST(value) / LAST(value) for the first or last row, or FIRST(n, v1, …, vn) / LAST(n, v1, …, vn) for the first or
	/// last n rows (one value per row, or one value for all of them). Other rows get else= (empty by default).
	/// </summary>
	private static PatternNode CreateRowPosition(PatternArgumentBinder binder, string functionName, bool fromEnd)
	{
		IReadOnlyList<PatternArgument> arguments  = binder.TakeRemainingPositional();
		string                         otherValue = binder.OptionalValue("else") ?? string.Empty;

		binder.EnsureComplete("count", "values", "else");

		if (arguments.Count == 0)
		{
			throw binder.Error($"a value is required, e.g. {functionName}('-END') or {functionName}(2, 'A', 'B').");
		}

		if (arguments.Count == 1)
		{
			return new RowPositionPatternNode(fromEnd, ReadValues(binder, arguments), otherValue);
		}

		if (arguments[0].Kind != PatternArgumentKind.Number || arguments[0].IsEvaluated)
		{
			throw binder.Error(
				$"with more than one value, the first one is the number of rows, e.g. {functionName}(2, 'A', 'B') or {functionName}(3, 'X')."
			);
		}

		int                   rowCount = binder.ToCount(arguments[0].Number, "the number of rows", 1, MAXIMUM_POSITION_ROWS);
		IReadOnlyList<string> values   = ReadValues(binder, [.. arguments.Skip(1)]);

		if (values.Count == 1)
		{
			values = [.. Enumerable.Repeat(values[0], rowCount)];
		}
		else if (values.Count != rowCount)
		{
			throw binder.Error(
				$"{functionName}({rowCount}, …) needs {rowCount} values (one per row) or a single value for all of them, but {values.Count} were given."
			);
		}

		return new RowPositionPatternNode(fromEnd, values, otherValue);
	}

	private static PatternNode CreateCase(PatternArgumentBinder binder, string functionName, bool upperCase)
	{
		string text = binder.RequireValue("text", $"{functionName}(COL(Name))");

		binder.EnsureComplete("text");
		return new LiteralPatternNode(upperCase ? text.ToUpperInvariant() : text.ToLowerInvariant());
	}

	private static PatternNode CreateTextPart(PatternArgumentBinder binder, string functionName, bool fromEnd)
	{
		string  text   = binder.RequireValue("text", $"{functionName}(COL(Name), 3)");
		decimal length = binder.OptionalNumber("length") ?? throw binder.Error($"length is required, e.g. {functionName}(COL(Name), 3).");
		int     count  = Math.Min(binder.ToCount(length, "length", 0, MAXIMUM_TEXT_LENGTH), text.Length);

		binder.EnsureComplete("text", "length");
		return new LiteralPatternNode(fromEnd ? text[^count..] : text[..count]);
	}

	private static PatternNode CreatePad(PatternArgumentBinder binder)
	{
		string  text      = binder.RequireValue("text", "PAD(COL(Number), 6)");
		decimal length    = binder.OptionalNumber("length") ?? throw binder.Error("length is required, e.g. PAD(COL(Number), 6).");
		string  character = binder.OptionalValue("character") ?? PAD_CHARACTER;

		if (character.Length != 1)
		{
			throw binder.Error($"character must be a single character, e.g. PAD(COL(Number), 6, '0'), not '{character}'.");
		}

		binder.EnsureComplete("text", "length", "character");
		return new LiteralPatternNode(text.PadLeft(binder.ToCount(length, "length", 0, MAXIMUM_TEXT_LENGTH), character[0]));
	}

	private static IReadOnlyList<string> ReadValues(PatternArgumentBinder binder, IReadOnlyList<PatternArgument> arguments)
	{
		foreach (PatternArgument argument in arguments)
		{
			if (argument.Kind == PatternArgumentKind.Range)
			{
				throw binder.Error($"'{argument.Text}' looks like a range; put it in quotes to use it as text, e.g. '{argument.Text}'.");
			}
		}

		return [.. arguments.Select(argument => argument.Text)];
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

		EnsureValidDateFormat(binder, format);
		binder.EnsureComplete("min", "max", "format");
		return new RandomDatePatternNode(minimum, maximum, format);
	}

	private static PatternNode CreateToday(PatternArgumentBinder binder)
	{
		string timeText = binder.OptionalText("time") ?? TIME_NOW;
		string time     = timeText.ToUpperInvariant();
		string format   = binder.OptionalText("format") ?? DEFAULT_DATE_TIME_FORMAT;

		if (time is not (TIME_NOW or TIME_ANY))
		{
			throw binder.Error(
				$"time must be NOW (the time the value is generated) or ANY (a random time of today), not '{timeText}'. "
				+ "A format goes second, e.g. TODAY(NOW, 'dd/MM/yyyy'), or is named, e.g. TODAY(format='dd/MM/yyyy')."
			);
		}

		EnsureValidDateFormat(binder, format);
		binder.EnsureComplete("time", "format");
		return new TodayPatternNode(time == TIME_ANY, format);
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

	private static PatternNode CreateColumnReference(PatternArgumentBinder binder)
	{
		string columnName = binder.RequireText("name", "COL(Colour) or COL('Shirt colour')").Trim();

		if (columnName.Length == 0)
		{
			throw binder.Error("the column name is empty, e.g. COL(Colour).");
		}

		binder.EnsureComplete("name");
		return new ColumnReferencePatternNode(columnName);
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

	private static void EnsureValidDateFormat(PatternArgumentBinder binder, string format)
	{
		try
		{
			_ = DateTime.Now.ToString(format, CultureInfo.InvariantCulture);
		}
		catch (FormatException)
		{
			throw binder.Error($"'{format}' is not a valid date format. Try 'yyyy-MM-dd' or 'dd/MM/yyyy HH:mm'.");
		}
	}

	private static DateTime ParseDate(PatternArgumentBinder binder, string text)
	{
		return DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out DateTime value)
			? value
			: throw binder.Error($"'{text}' is not a valid date. Use the form 'yyyy-MM-dd', e.g. '2024-12-31'.");
	}
}