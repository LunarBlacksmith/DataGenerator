using System.Globalization;
using System.Text;

namespace DataGenerator.Services.Patterns;

internal static class PatternFunctionFactory
{
	#region FIELDS
	#region PUBLIC
	// Every function must be described in PatternLanguageReference, which the reference window and suggestions use.
	public static readonly IReadOnlyList<string> FUNCTION_NAMES;
	#endregion PUBLIC

	#region PRIVATE
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

	private static readonly string[] FUNCTION_ALIASES;
	#endregion PRIVATE
	#endregion FIELDS

	#region CONSTRUCTORS
	#region STATIC
	/// <summary>
	///	Sets the default values of the static fields and properties of <see cref="PatternFunctionFactory"/>.
	/// </summary>
	static PatternFunctionFactory()
	{
		FUNCTION_NAMES   = [..
			PatternLanguageReference
				.FUNCTIONS
				.Select(entry => entry.Name)
		];
		FUNCTION_ALIASES = ["SEQUENCE", "COLUMN"];
	}
	#endregion STATIC
	#endregion CONSTRUCTORS

	#region METHODS
	#region PUBLIC
	/// <summary>
	///	Creates the pattern node for a recognised function call, evaluating static arguments immediately and preserving
	///	nested-function arguments for row-time evaluation.
	/// </summary>
	/// <param name="nameToken">
	///	The token that names the function.
	/// </param>
	/// <param name="arguments">
	///	The parsed arguments supplied to the function.
	/// </param>
	/// <returns>
	///	The node that implements the function call.
	/// </returns>
	/// <exception cref="PatternSyntaxException">
	///	Thrown when the function name is unknown or one of its arguments is invalid.
	/// </exception>
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

		if (
			arguments
				.Any(argument => argument.Kind == PatternArgumentKind.Expression)
		)
		{
			return CreateDynamic(nameToken, arguments);
		}

		PatternArgumentBinder binder = new(functionName, arguments, nameToken.Position);

		PatternNode node = functionName switch
		{
			"SEQ" or "SEQUENCE" => CreateSequence(binder),
			"RAND_NUM"          => CreateRandomNumber(binder, "RAND_NUM(0, 99)"),
			"ODD"               => CreateRandomNumber(binder, "ODD(1, 99)", isOdd: true),
			"EVEN"              => CreateRandomNumber(binder, "EVEN(0, 100)", isOdd: false),
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
	#endregion PUBLIC

	#region PRIVATE
	/// <summary>
	///	Creates a function node whose arguments include nested function calls. When the nested calls do not depend on
	///	other columns, they are tried once now so mistakes such as a wrong parameter are reported while the pattern is typed.
	/// </summary>
	/// <param name="nameToken">
	///	The token that names the outer function.
	/// </param>
	/// <param name="arguments">
	///	The parsed arguments, including expression arguments for nested function calls.
	/// </param>
	/// <returns>
	///	A dynamic function node that evaluates nested calls for each generated row.
	/// </returns>
	/// <exception cref="PatternSyntaxException">
	///	Thrown when an argument-only validation run finds an invalid nested function result.
	/// </exception>
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

	/// <summary>
	///	Creates a SEQ or SEQUENCE node that counts through a whole-number range and wraps at the end.
	/// </summary>
	/// <param name="binder">
	///	The argument binder for the function call.
	/// </param>
	/// <returns>
	///	A sequence node configured with range, start, step and padding.
	/// </returns>
	/// <exception cref="PatternSyntaxException">
	///	Thrown when the range, start, step or digits arguments are invalid.
	/// </exception>
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

	/// <summary>
	///	Creates a random whole-number node from a required inclusive range and optional padding width.
	/// </summary>
	/// <param name="binder">
	///	The argument binder for the function call.
	/// </param>
	/// <param name="example">
	///	An example call used when reporting a missing or malformed range.
	/// </param>
	/// <param name="isOdd">
	///	Restricts values to odd or even numbers; <see langword="null"/> allows both parities.
	/// </param>
	/// <returns>
	///	A random-number node configured with the parsed range and digits.
	/// </returns>
	/// <exception cref="PatternSyntaxException">
	///	Thrown when the range or digits arguments are invalid.
	/// </exception>
	private static PatternNode CreateRandomNumber(PatternArgumentBinder binder, string example, bool? isOdd = null)
	{
		(decimal rangeStart, decimal rangeEnd) = binder.RequireRange(example);

		long minimum = binder.ToWholeNumber(rangeStart, "the range start");
		long maximum = binder.ToWholeNumber(rangeEnd, "the range end");
		int  digits  = ReadDigits(binder, minimum, maximum, NO_PADDING);

		if (isOdd.HasValue && minimum == maximum && (minimum % 2 != 0) != isOdd.Value)
		{
			throw binder.Error($"The range {minimum} to {maximum} contains no {(isOdd.Value ? "odd" : "even")} numbers.");
		}

		binder.EnsureComplete("range", "digits");
		return new RandomNumberPatternNode(minimum, maximum, digits, isOdd);
	}

	/// <summary>
	///	Creates a NUM node. With a range it behaves like RAND_NUM; without a range it uses either the requested digit
	///	width or the default broad number range.
	/// </summary>
	/// <param name="binder">
	///	The argument binder for the function call.
	/// </param>
	/// <returns>
	///	A random-number node for the NUM function.
	/// </returns>
	/// <exception cref="PatternSyntaxException">
	///	Thrown when the optional range or digits arguments are invalid.
	/// </exception>
	private static PatternNode CreateNumber(PatternArgumentBinder binder)
	{
		if (binder.HasRangeArgument())
		{
			return CreateRandomNumber(binder, "NUM(0, 99)");
		}

		decimal? requestedDigits = binder.OptionalNumber("digits");
		int      digits          =
			requestedDigits.HasValue
				? binder.ToCount(requestedDigits.Value, "digits", NO_PADDING, MAXIMUM_NUMBER_DIGITS)
				: NO_PADDING;
		long     maximum         = digits == NO_PADDING ? DEFAULT_NUMBER_MAXIMUM : (long)PatternTemplateSet.Pow10(digits) - 1;

		binder.EnsureComplete("min", "max", "digits");
		return new RandomNumberPatternNode(0, maximum, digits);
	}

	/// <summary>
	///	Creates a ROW node that emits the one-based row number with optional zero padding.
	/// </summary>
	/// <param name="binder">
	///	The argument binder for the function call.
	/// </param>
	/// <returns>
	///	A row-number node configured with the parsed digit width.
	/// </returns>
	/// <exception cref="PatternSyntaxException">
	///	Thrown when the digits argument is outside the allowed range.
	/// </exception>
	private static PatternNode CreateRowNumber(PatternArgumentBinder binder)
	{
		int digits = binder.ToCount(binder.OptionalNumber("digits") ?? NO_PADDING, "digits", NO_PADDING, MAXIMUM_DIGITS);

		binder.EnsureComplete("digits");
		return new RowNumberPatternNode(digits);
	}

	/// <summary>
	///	Creates a CYCLE node that returns the supplied values in row order, wrapping after the last value.
	/// </summary>
	/// <param name="binder">
	///	The argument binder for the function call.
	/// </param>
	/// <returns>
	///	A cycle node containing the positional values.
	/// </returns>
	/// <exception cref="PatternSyntaxException">
	///	Thrown when no values are supplied or a value is written as an unquoted range.
	/// </exception>
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
	///	Creates a FIRST or LAST node that supplies special values for the first or last rows and an optional else value for
	///	all other rows.
	/// </summary>
	/// <param name="binder">
	///	The argument binder for the function call.
	/// </param>
	/// <param name="functionName">
	///	The FIRST or LAST function name used in error messages.
	/// </param>
	/// <param name="fromEnd">
	///	Whether positions are counted from the end of the row set.
	/// </param>
	/// <returns>
	///	A row-position node configured with the chosen rows and other-row value.
	/// </returns>
	/// <exception cref="PatternSyntaxException">
	///	Thrown when the value list is missing, the row count is invalid, or the number of values does not match the count.
	/// </exception>
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

	/// <summary>
	///	Creates a literal node from text converted to upper or lower case.
	/// </summary>
	/// <param name="binder">
	///	The argument binder for the function call.
	/// </param>
	/// <param name="functionName">
	///	The function name used in the example when text is missing.
	/// </param>
	/// <param name="upperCase">
	///	Whether to convert the text to upper case; otherwise it is converted to lower case.
	/// </param>
	/// <returns>
	///	A literal node containing the converted text.
	/// </returns>
	/// <exception cref="PatternSyntaxException">
	///	Thrown when the text argument is missing or invalid.
	/// </exception>
	private static PatternNode CreateCase(PatternArgumentBinder binder, string functionName, bool upperCase)
	{
		string text = binder.RequireValue("text", $"{functionName}(COL(Name))");

		binder.EnsureComplete("text");
		return new LiteralPatternNode(upperCase ? text.ToUpperInvariant() : text.ToLowerInvariant());
	}

	/// <summary>
	///	Creates a literal node from the leftmost or rightmost characters of supplied text.
	/// </summary>
	/// <param name="binder">
	///	The argument binder for the function call.
	/// </param>
	/// <param name="functionName">
	///	The LEFT or RIGHT function name used in examples.
	/// </param>
	/// <param name="fromEnd">
	///	Whether to take characters from the end of the text.
	/// </param>
	/// <returns>
	///	A literal node containing the requested slice, or an empty literal when the length is 0.
	/// </returns>
	/// <exception cref="PatternSyntaxException">
	///	Thrown when the text or length arguments are missing or invalid.
	/// </exception>
	private static PatternNode CreateTextPart(PatternArgumentBinder binder, string functionName, bool fromEnd)
	{
		string  text   = binder.RequireValue("text", $"{functionName}(COL(Name), 3)");
		decimal length = binder.OptionalNumber("length") ?? throw binder.Error($"length is required, e.g. {functionName}(COL(Name), 3).");
		int     count  = Math.Min(binder.ToCount(length, "length", 0, MAXIMUM_TEXT_LENGTH), text.Length);

		binder.EnsureComplete("text", "length");
		return new LiteralPatternNode(fromEnd ? text[^count..] : text[..count]);
	}

	/// <summary>
	///	Creates a literal node from text padded on the left to a requested length with a single character.
	/// </summary>
	/// <param name="binder">
	///	The argument binder for the function call.
	/// </param>
	/// <returns>
	///	A literal node containing the padded text.
	/// </returns>
	/// <exception cref="PatternSyntaxException">
	///	Thrown when text or length is missing, length is invalid, or the pad character is not exactly one character.
	/// </exception>
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

	/// <summary>
	///	Reads positional arguments as literal values and rejects unquoted ranges that probably meant text.
	/// </summary>
	/// <param name="binder">
	///	The argument binder used to create validation errors.
	/// </param>
	/// <param name="arguments">
	///	The positional arguments to read.
	/// </param>
	/// <returns>
	///	The argument texts in their original order.
	/// </returns>
	/// <exception cref="PatternSyntaxException">
	///	Thrown when one of the values was parsed as a range.
	/// </exception>
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

	/// <summary>
	///	Creates a RAND_DECIMAL node from a decimal range and optional number of decimal places.
	/// </summary>
	/// <param name="binder">
	///	The argument binder for the function call.
	/// </param>
	/// <returns>
	///	A random-decimal node configured with range and precision.
	/// </returns>
	/// <exception cref="PatternSyntaxException">
	///	Thrown when the range or decimals arguments are invalid.
	/// </exception>
	private static PatternNode CreateRandomDecimal(PatternArgumentBinder binder)
	{
		(decimal minimum, decimal maximum) = binder.RequireRange("RAND_DECIMAL(0, 100)");

		_ = binder.ToWholeNumber(decimal.Truncate(minimum), "the range start");
		_ = binder.ToWholeNumber(decimal.Truncate(maximum), "the range end");

		int decimals = binder.ToCount(binder.OptionalNumber("decimals") ?? 2, "decimals", 0, MAXIMUM_DECIMALS);

		binder.EnsureComplete("range", "decimals");
		return new RandomDecimalPatternNode(minimum, maximum, decimals);
	}

	/// <summary>
	///	Creates a random text node for letters, digits, or letters and digits, with fixed or ranged length.
	/// </summary>
	/// <param name="binder">
	///	The argument binder for the function call.
	/// </param>
	/// <param name="example">
	///	An example call used when reporting a missing or malformed length.
	/// </param>
	/// <param name="includeLetters">
	///	Whether the generated alphabet includes letters.
	/// </param>
	/// <param name="includeDigits">
	///	Whether the generated alphabet includes digits.
	/// </param>
	/// <returns>
	///	A random-text node configured with alphabet and length bounds.
	/// </returns>
	/// <exception cref="PatternSyntaxException">
	///	Thrown when the length or case arguments are invalid.
	/// </exception>
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

	/// <summary>
	///	Creates a RAND_DATE node from two parsed dates and an optional output format.
	/// </summary>
	/// <param name="binder">
	///	The argument binder for the function call.
	/// </param>
	/// <returns>
	///	A random-date node configured with inclusive date-time bounds and format.
	/// </returns>
	/// <exception cref="PatternSyntaxException">
	///	Thrown when dates are missing, invalid, reversed, or the format is invalid.
	/// </exception>
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

	/// <summary>
	///	Creates a TODAY node using either the current generation time or a random time within today.
	/// </summary>
	/// <param name="binder">
	///	The argument binder for the function call.
	/// </param>
	/// <returns>
	///	A today node configured with the selected time mode and format.
	/// </returns>
	/// <exception cref="PatternSyntaxException">
	///	Thrown when the time mode or date format is invalid.
	/// </exception>
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

	/// <summary>
	///	Creates a choice node that picks one of the supplied values at random.
	/// </summary>
	/// <param name="binder">
	///	The argument binder for the function call.
	/// </param>
	/// <returns>
	///	A choice node containing one literal option per supplied value.
	/// </returns>
	/// <exception cref="PatternSyntaxException">
	///	Thrown when no values are supplied or an unknown named parameter remains.
	/// </exception>
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

	/// <summary>
	///	Creates a GUID node that emits a new identifier in upper or lower case.
	/// </summary>
	/// <param name="binder">
	///	The argument binder for the function call.
	/// </param>
	/// <returns>
	///	A GUID node configured with the requested letter case.
	/// </returns>
	/// <exception cref="PatternSyntaxException">
	///	Thrown when the case argument is not UPPER or LOWER.
	/// </exception>
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
	///	Creates a COL or COLUMN node that reads another column value from the current row.
	/// </summary>
	/// <param name="binder">
	///	The argument binder for the function call.
	/// </param>
	/// <returns>
	///	A column-reference node for the trimmed column name.
	/// </returns>
	/// <exception cref="PatternSyntaxException">
	///	Thrown when the name argument is missing, invalid or empty.
	/// </exception>
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
	///	Reads the optional digits argument for fixed-width numbers and checks it can contain the largest range value.
	/// </summary>
	/// <param name="binder">
	///	The argument binder for the function call.
	/// </param>
	/// <param name="minimum">
	///	The inclusive minimum value that may be formatted.
	/// </param>
	/// <param name="maximum">
	///	The inclusive maximum value that may be formatted.
	/// </param>
	/// <param name="defaultDigits">
	///	The digit width to use when the argument is omitted.
	/// </param>
	/// <returns>
	///	The requested digit width, or 0 for no padding.
	/// </returns>
	/// <exception cref="PatternSyntaxException">
	///	Thrown when the width is outside the allowed range or too small for the values.
	/// </exception>
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

	/// <summary>
	///	Checks that a date-time format string can be used to format a date.
	/// </summary>
	/// <param name="binder">
	///	The argument binder used to create validation errors.
	/// </param>
	/// <param name="format">
	///	The .NET date-time format string to validate.
	/// </param>
	/// <exception cref="PatternSyntaxException">
	///	Thrown when <paramref name="format"/> is not a valid date-time format string.
	/// </exception>
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

	/// <summary>
	///	Parses a date argument using invariant-culture date rules.
	/// </summary>
	/// <param name="binder">
	///	The argument binder used to create validation errors.
	/// </param>
	/// <param name="text">
	///	The text to parse as a date.
	/// </param>
	/// <returns>
	///	The parsed date and time.
	/// </returns>
	/// <exception cref="PatternSyntaxException">
	///	Thrown when <paramref name="text"/> is not a valid date.
	/// </exception>
	private static DateTime ParseDate(PatternArgumentBinder binder, string text)
		=>
			DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out DateTime value)
				? value
				: throw binder.Error($"'{text}' is not a valid date. Use the form 'yyyy-MM-dd', e.g. '2024-12-31'.");
	#endregion PRIVATE
	#endregion METHODS
}