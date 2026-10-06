using System.Globalization;
using System.Text;

namespace DataGenerator.Services.Patterns;

internal static class PatternNumberFormatter
{
	/// <summary>
	///	Appends a whole number with invariant-culture digits and optional zero padding after any minus sign.
	/// </summary>
	/// <param name="builder">
	///	The builder to append to.
	/// </param>
	/// <param name="value">
	///	The whole number to format.
	/// </param>
	/// <param name="digits">
	///	The minimum number of digits in the absolute value, or 0 for no padding.
	/// </param>
	public static void AppendPadded(StringBuilder builder, long value, int digits)
	{
		if (value < 0)
		{
			_ = builder.Append('-');
		}

		_ = builder.Append(Math.Abs(value).ToString(CultureInfo.InvariantCulture).PadLeft(digits, '0'));
	}

	/// <summary>
	///	Counts the invariant-culture digits in the absolute value of a whole number.
	/// </summary>
	/// <param name="value">
	///	The value whose digit count is needed.
	/// </param>
	/// <returns>
	///	The number of digits in <paramref name="value"/> without its sign.
	/// </returns>
	public static int CountDigits(long value) => Math.Abs(value).ToString(CultureInfo.InvariantCulture).Length;
}

internal sealed class SequencePatternNode : PatternNode
{
	private readonly long _minimum;
	private readonly long _maximum;
	private readonly long _start;
	private readonly long _step;
	private readonly int  _digits;

	/// <summary>
	///	Creates a sequence node that walks a whole-number range by a fixed step and wraps within the range.
	/// </summary>
	/// <param name="minimum">
	///	The inclusive lower bound of the sequence range.
	/// </param>
	/// <param name="maximum">
	///	The inclusive upper bound of the sequence range.
	/// </param>
	/// <param name="start">
	///	The value generated for row index 0.
	/// </param>
	/// <param name="step">
	///	The amount added for each subsequent row.
	/// </param>
	/// <param name="digits">
	///	The padding width for formatted values, or 0 for no padding.
	/// </param>
	public SequencePatternNode(long minimum, long maximum, long start, long step, int digits)
	{
		_minimum = minimum;
		_maximum = maximum;
		_start   = start;
		_step    = step;
		_digits  = digits;
	}

	/// <summary>
	///	Appends the sequence value for the current row, wrapping forwards or backwards inside the configured range.
	/// </summary>
	/// <param name="builder">
	///	The builder receiving the formatted value.
	/// </param>
	/// <param name="context">
	///	The row context that supplies the zero-based row index.
	/// </param>
	public override void Append(StringBuilder builder, PatternContext context)
	{
		Int128 size    = (Int128)_maximum - _minimum + 1;
		Int128 offset  = (Int128)_start - _minimum + ((Int128)_step * context.RowIndex);
		Int128 wrapped = ((offset % size) + size) % size;

		PatternNumberFormatter.AppendPadded(builder, (long)(_minimum + wrapped), _digits);
	}

	/// <summary>
	///	Expands the sequence into numeric templates covering every value in its range.
	/// </summary>
	/// <returns>
	///	A template set for the sequence's formatted whole-number range.
	/// </returns>
	/// <exception cref="PatternSyntaxException">
	///	Thrown when the sequence contains negative numbers, which cannot be translated to SQL pattern matching.
	/// </exception>
	public override PatternTemplateSet ExpandTemplates() => PatternTemplateSet.ForNumbers(_minimum, _maximum, _digits, FunctionName);
}

internal sealed class RandomNumberPatternNode : PatternNode
{
	private readonly int _digits;

	/// <summary>
	///	Creates a random whole-number node for an inclusive range and optional padding width.
	/// </summary>
	/// <param name="minimum">
	///	The inclusive lower bound.
	/// </param>
	/// <param name="maximum">
	///	The inclusive upper bound.
	/// </param>
	/// <param name="digits">
	///	The padding width for formatted values, or 0 for no padding.
	/// </param>
	public RandomNumberPatternNode(long minimum, long maximum, int digits)
	{
		Minimum = minimum;
		Maximum = maximum;
		_digits = digits;
	}

	public long Minimum { get; }
	public long Maximum { get; }

	/// <summary>
	///	Appends a random whole number from the configured inclusive range.
	/// </summary>
	/// <param name="builder">
	///	The builder receiving the formatted value.
	/// </param>
	/// <param name="context">
	///	The row context that supplies the random source.
	/// </param>
	public override void Append(StringBuilder builder, PatternContext context)
		=> PatternNumberFormatter.AppendPadded(builder, context.Random.NextInt64(Minimum, Maximum + 1), _digits);

	/// <summary>
	///	Expands the random number into numeric templates covering its possible values.
	/// </summary>
	/// <returns>
	///	A template set for the formatted whole-number range.
	/// </returns>
	/// <exception cref="PatternSyntaxException">
	///	Thrown when the range contains negative numbers, which cannot be translated to SQL pattern matching.
	/// </exception>
	public override PatternTemplateSet ExpandTemplates() => PatternTemplateSet.ForNumbers(Minimum, Maximum, _digits, FunctionName);

	/// <summary>
	///	Creates the same random-number function limited to a narrower range, e.g. for NUM(...) GREATER THAN 50.
	/// </summary>
	/// <param name="minimum">
	///	The new inclusive lower bound.
	/// </param>
	/// <param name="maximum">
	///	The new inclusive upper bound.
	/// </param>
	/// <returns>
	///	A random-number node with the same padding and function name, but the supplied range.
	/// </returns>
	public RandomNumberPatternNode WithRange(long minimum, long maximum)
		=> new RandomNumberPatternNode(minimum, maximum, _digits) { FunctionName = FunctionName };
}

internal sealed class RandomDecimalPatternNode : PatternNode
{
	private readonly decimal _minimum;
	private readonly decimal _maximum;
	private readonly int     _decimals;

	/// <summary>
	///	Creates a random decimal node for an inclusive range and fixed number of decimal places.
	/// </summary>
	/// <param name="minimum">
	///	The inclusive lower bound.
	/// </param>
	/// <param name="maximum">
	///	The inclusive upper bound.
	/// </param>
	/// <param name="decimals">
	///	The number of decimal places to keep in the formatted value.
	/// </param>
	public RandomDecimalPatternNode(decimal minimum, decimal maximum, int decimals)
	{
		_minimum  = minimum;
		_maximum  = maximum;
		_decimals = decimals;
	}

	/// <summary>
	///	Appends a random decimal in the configured range, rounded and formatted with the configured decimal places.
	/// </summary>
	/// <param name="builder">
	///	The builder receiving the formatted value.
	/// </param>
	/// <param name="context">
	///	The row context that supplies the random source.
	/// </param>
	public override void Append(StringBuilder builder, PatternContext context)
	{
		decimal fraction = (decimal)context.Random.NextDouble();
		decimal value    = Math.Round(_minimum + ((_maximum - _minimum) * fraction), _decimals, MidpointRounding.AwayFromZero);

		_ = builder.Append(Math.Clamp(value, _minimum, _maximum).ToString($"F{_decimals}", CultureInfo.InvariantCulture));
	}
}

internal sealed class RandomTextPatternNode : PatternNode
{
	private readonly string _alphabet;
	private readonly int    _minimumLength;
	private readonly int    _maximumLength;

	/// <summary>
	///	Creates a random text node that draws characters from an alphabet and uses a fixed or ranged length.
	/// </summary>
	/// <param name="alphabet">
	///	The characters that may be chosen for each position.
	/// </param>
	/// <param name="minimumLength">
	///	The shortest generated text length.
	/// </param>
	/// <param name="maximumLength">
	///	The longest generated text length.
	/// </param>
	public RandomTextPatternNode(string alphabet, int minimumLength, int maximumLength)
	{
		_alphabet      = alphabet;
		_minimumLength = minimumLength;
		_maximumLength = maximumLength;
	}

	/// <summary>
	///	Appends a random string whose length is chosen from the configured length range.
	/// </summary>
	/// <param name="builder">
	///	The builder receiving the generated text.
	/// </param>
	/// <param name="context">
	///	The row context that supplies the random source.
	/// </param>
	/// <exception cref="InvalidOperationException">
	///	Thrown when the generated pattern output exceeds the maximum allowed length.
	/// </exception>
	public override void Append(StringBuilder builder, PatternContext context)
	{
		int length = context.Random.Next(_minimumLength, _maximumLength + 1);

		for (int index = 0; index < length; ++index)
		{
			_ = builder.Append(_alphabet[context.Random.Next(_alphabet.Length)]);
		}

		PatternContext.EnsureLength(builder);
	}

	/// <summary>
	///	Expands random text into LIKE character-class templates for each possible length.
	/// </summary>
	/// <returns>
	///	A template set that represents the possible lengths and character class.
	/// </returns>
	/// <exception cref="PatternSyntaxException">
	///	Thrown when the length range produces too many SQL shapes.
	/// </exception>
	public override PatternTemplateSet ExpandTemplates()
	{
		string likeClass =
			_alphabet.All(char.IsDigit)
				? "[0-9]"
				: _alphabet.All(char.IsLetter)
					? "[A-Z]"
					: "[A-Z0-9]";

		return PatternTemplateSet.ForCharacters(likeClass, _minimumLength, _maximumLength);
	}
}

internal sealed class RandomDatePatternNode : PatternNode
{
	private readonly DateTime _minimum;
	private readonly DateTime _maximum;
	private readonly string   _format;

	/// <summary>
	///	Creates a random date node for an inclusive date-time range and output format.
	/// </summary>
	/// <param name="minimum">
	///	The inclusive earliest date and time.
	/// </param>
	/// <param name="maximum">
	///	The inclusive latest date and time.
	/// </param>
	/// <param name="format">
	///	The invariant-culture date-time format used for output.
	/// </param>
	public RandomDatePatternNode(DateTime minimum, DateTime maximum, string format)
	{
		_minimum = minimum;
		_maximum = maximum;
		_format  = format;
	}

	/// <summary>
	///	Appends a random date and time from the configured range using the configured format.
	/// </summary>
	/// <param name="builder">
	///	The builder receiving the formatted date.
	/// </param>
	/// <param name="context">
	///	The row context that supplies the random source.
	/// </param>
	public override void Append(StringBuilder builder, PatternContext context)
	{
		DateTime value = new(context.Random.NextInt64(_minimum.Ticks, _maximum.Ticks + 1));

		_ = builder.Append(value.ToString(_format, CultureInfo.InvariantCulture));
	}
}

/// <summary>
///	Today's date, either with the time the value is generated (NOW) or with a random time of the day (ANY).
/// </summary>
internal sealed class TodayPatternNode : PatternNode
{
	private readonly bool   _anyTime;
	private readonly string _format;

	/// <summary>
	///	Creates a TODAY node for the current time or a random time on the current date.
	/// </summary>
	/// <param name="anyTime">
	///	Whether to choose a random time during today instead of using the current time.
	/// </param>
	/// <param name="format">
	///	The invariant-culture date-time format used for output.
	/// </param>
	public TodayPatternNode(bool anyTime, string format)
	{
		_anyTime = anyTime;
		_format  = format;
	}

	/// <summary>
	///	Appends today's date using either the generation time or a random time from the same day.
	/// </summary>
	/// <param name="builder">
	///	The builder receiving the formatted date.
	/// </param>
	/// <param name="context">
	///	The row context that supplies the clock and random source.
	/// </param>
	public override void Append(StringBuilder builder, PatternContext context)
	{
		DateTime now   = context.Now;
		DateTime value =
			_anyTime
				? now.Date.AddTicks(context.Random.NextInt64(TimeSpan.TicksPerDay))
				: now;

		_ = builder.Append(value.ToString(_format, CultureInfo.InvariantCulture));
	}
}

internal sealed class GuidPatternNode : PatternNode
{
	private const string HEX_CLASS = "[0-9A-F]";

	private static readonly int[] GROUP_LENGTHS = [8, 4, 4, 4, 12];

	private readonly bool _upperCase;

	/// <summary>
	///	Creates a GUID node with a fixed letter case.
	/// </summary>
	/// <param name="upperCase">
	///	Whether hexadecimal letters are written in upper case.
	/// </param>
	public GuidPatternNode(bool upperCase)
	{
		_upperCase = upperCase;
	}

	/// <summary>
	///	Appends a newly generated GUID in the configured letter case.
	/// </summary>
	/// <param name="builder">
	///	The builder receiving the GUID text.
	/// </param>
	/// <param name="context">
	///	The row context; this node does not read values from it.
	/// </param>
	public override void Append(StringBuilder builder, PatternContext context)
	{
		string text = Guid.NewGuid().ToString("D");

		_ = builder.Append(_upperCase ? text.ToUpperInvariant() : text);
	}

	/// <summary>
	///	Expands a GUID into templates for its five hexadecimal groups and hyphens.
	/// </summary>
	/// <returns>
	///	A template set matching 36-character GUID text.
	/// </returns>
	public override PatternTemplateSet ExpandTemplates()
	{
		PatternTemplateSet result = PatternTemplateSet.EMPTY_TEXT;

		for (int index = 0; index < GROUP_LENGTHS.Length; ++index)
		{
			if (index > 0)
			{
				result = result.Then(PatternTemplateSet.FromLiteral("-"));
			}

			result = result.Then(PatternTemplateSet.ForCharacters(HEX_CLASS, GROUP_LENGTHS[index], GROUP_LENGTHS[index]));
		}

		return result;
	}
}

/// <summary>
///	The one-based number of the row within its row set, e.g. ROW(3) → 001, 002, …
/// </summary>
internal sealed class RowNumberPatternNode : PatternNode
{
	private readonly int _digits;

	/// <summary>
	///	Creates a ROW node with optional zero padding.
	/// </summary>
	/// <param name="digits">
	///	The padding width for row numbers, or 0 for no padding.
	/// </param>
	public RowNumberPatternNode(int digits)
	{
		_digits = digits;
	}

	/// <summary>
	///	Appends the current row's one-based number.
	/// </summary>
	/// <param name="builder">
	///	The builder receiving the formatted row number.
	/// </param>
	/// <param name="context">
	///	The row context that supplies the zero-based row index.
	/// </param>
	public override void Append(StringBuilder builder, PatternContext context)
		=> PatternNumberFormatter.AppendPadded(builder, context.RowIndex + 1, _digits);
}

/// <summary>
///	The listed values in turn, one per row, starting again after the last, e.g. CYCLE('A', 'B') → A, B, A, B, …
/// </summary>
internal sealed class CyclePatternNode : PatternNode
{
	private readonly IReadOnlyList<string> _values;

	/// <summary>
	///	Creates a CYCLE node from the ordered values it should repeat through the row set.
	/// </summary>
	/// <param name="values">
	///	The non-empty list of values to cycle through.
	/// </param>
	public CyclePatternNode(IReadOnlyList<string> values)
	{
		_values = values;
	}

	/// <summary>
	///	Appends the cycle value selected by the current row index.
	/// </summary>
	/// <param name="builder">
	///	The builder receiving the selected value.
	/// </param>
	/// <param name="context">
	///	The row context that supplies the zero-based row index.
	/// </param>
	public override void Append(StringBuilder builder, PatternContext context)
		=> _ = builder.Append(_values[(int)(context.RowIndex % _values.Count)]);

	/// <summary>
	///	Expands the cycle values into a union of literal templates.
	/// </summary>
	/// <returns>
	///	A template set matching any listed cycle value.
	/// </returns>
	/// <exception cref="PatternSyntaxException">
	///	Thrown when the cycle has too many different shapes for SQL matching.
	/// </exception>
	public override PatternTemplateSet ExpandTemplates()
		=> PatternTemplateSet.Union([.. _values.Select(PatternTemplateSet.FromLiteral)]);
}

/// <summary>
///	Values for the first or last rows of the row set (FIRST and LAST); every other row gets the else value.
///	LAST(3, 'A', 'B', 'C') gives the third-last row A, the second-last B and the last C.
/// </summary>
internal sealed class RowPositionPatternNode : PatternNode
{
	private readonly bool                  _fromEnd;
	private readonly IReadOnlyList<string> _values;
	private readonly string                _otherValue;

	/// <summary>
	///	Creates a FIRST or LAST node with row-specific values and the value for all other rows.
	/// </summary>
	/// <param name="fromEnd">
	///	Whether the node counts matching rows from the end of the row set.
	/// </param>
	/// <param name="values">
	///	The values for the selected rows, in row order.
	/// </param>
	/// <param name="otherValue">
	///	The value used when the current row is not one of the selected rows.
	/// </param>
	public RowPositionPatternNode(bool fromEnd, IReadOnlyList<string> values, string otherValue)
	{
		_fromEnd    = fromEnd;
		_values     = values;
		_otherValue = otherValue;
	}

	/// <summary>
	///	Appends the configured first-row, last-row or other-row value for the current row.
	/// </summary>
	/// <param name="builder">
	///	The builder receiving the selected value.
	/// </param>
	/// <param name="context">
	///	The row context that supplies row index and, for LAST, row count.
	/// </param>
	public override void Append(StringBuilder builder, PatternContext context)
	{
		// Counted back from the end when the row count is known, so that the last listed value belongs to the last row.
		long position =
			!_fromEnd
				? context.RowIndex
				: context.RowCount.HasValue
					? _values.Count - (context.RowCount.Value - context.RowIndex)
					: -1;

		_ = builder.Append(position >= 0 && position < _values.Count ? _values[(int)position] : _otherValue);
	}

	/// <summary>
	///	Expands row-position values into a union that includes the other-row value.
	/// </summary>
	/// <returns>
	///	A template set matching any possible FIRST or LAST output value.
	/// </returns>
	/// <exception cref="PatternSyntaxException">
	///	Thrown when the values produce too many different shapes for SQL matching.
	/// </exception>
	public override PatternTemplateSet ExpandTemplates()
		=> PatternTemplateSet.Union([.. _values.Append(_otherValue).Select(PatternTemplateSet.FromLiteral)]);
}

/// <summary>
///	The value of another column of the same row, e.g. COL(Colour).
/// </summary>
internal sealed class ColumnReferencePatternNode : PatternNode
{
	private readonly string _columnName;

	/// <summary>
	///	Creates a COL node for another column in the current row.
	/// </summary>
	/// <param name="columnName">
	///	The referenced column name.
	/// </param>
	public ColumnReferencePatternNode(string columnName)
	{
		_columnName = columnName;
	}

	/// <summary>
	///	Appends the referenced column's current-row value.
	/// </summary>
	/// <param name="builder">
	///	The builder receiving the column value.
	/// </param>
	/// <param name="context">
	///	The row context that supplies column values.
	/// </param>
	/// <exception cref="InvalidOperationException">
	///	Thrown when no column-value provider is available, or when the output length limit is exceeded.
	/// </exception>
	public override void Append(StringBuilder builder, PatternContext context)
	{
		_ = builder.Append(context.GetColumnValue(_columnName));
		PatternContext.EnsureLength(builder);
	}

	/// <summary>
	///	Adds the referenced column name once, ignoring case.
	/// </summary>
	/// <param name="columnNames">
	///	The collection receiving referenced column names.
	/// </param>
	public override void CollectColumnReferences(ICollection<string> columnNames)
	{
		if (!columnNames.Contains(_columnName, StringComparer.OrdinalIgnoreCase))
		{
			columnNames.Add(_columnName);
		}
	}
}

/// <summary>
///	A function with nested function calls as arguments, e.g. RAND_DATE(TODAY(), '2030-12-31'). The nested calls are
///	evaluated for every value, then the function is created with their results.
/// </summary>
internal sealed class DynamicFunctionPatternNode : PatternNode
{
	private const char KEY_SEPARATOR = '\u001F';

	private readonly PatternToken                   _nameToken;
	private readonly IReadOnlyList<PatternArgument> _arguments;
	private CachedFunction?                         _lastFunction;

	/// <summary>
	///	Creates a function node whose nested-function arguments are evaluated for each row before the outer function runs.
	/// </summary>
	/// <param name="nameToken">
	///	The token that names the outer function.
	/// </param>
	/// <param name="arguments">
	///	The original parsed arguments, including any nested expression arguments.
	/// </param>
	public DynamicFunctionPatternNode(PatternToken nameToken, IReadOnlyList<PatternArgument> arguments)
	{
		_nameToken = nameToken;
		_arguments = arguments;
	}

	/// <summary>
	///	Evaluates nested function arguments, creates or reuses the matching outer function node, and appends its value.
	/// </summary>
	/// <param name="builder">
	///	The builder receiving the outer function's generated value.
	/// </param>
	/// <param name="context">
	///	The row context used to evaluate nested and outer functions.
	/// </param>
	/// <exception cref="PatternSyntaxException">
	///	Thrown when evaluated nested values make the outer function invalid.
	/// </exception>
	public override void Append(StringBuilder builder, PatternContext context)
	{
		List<PatternArgument> evaluatedArguments = new(_arguments.Count);
		StringBuilder         keyBuilder         = new();

		foreach (PatternArgument argument in _arguments)
		{
			PatternArgument evaluatedArgument = argument;

			if (argument.Expression is not null)
			{
				StringBuilder valueBuilder = new();

				argument.Expression.Append(valueBuilder, context);
				evaluatedArgument = argument.WithEvaluatedValue(valueBuilder.ToString());
			}

			evaluatedArguments.Add(evaluatedArgument);
			_ = keyBuilder.Append(evaluatedArgument.Text).Append(KEY_SEPARATOR);
		}

		// Nested values such as TODAY() rarely change between rows, so the last created function is reused.
		string          key      = keyBuilder.ToString();
		CachedFunction? function = Volatile.Read(ref _lastFunction);

		if (function is null || !string.Equals(function.Key, key, StringComparison.Ordinal))
		{
			function = new CachedFunction(key, PatternFunctionFactory.Create(_nameToken, evaluatedArguments));
			Volatile.Write(ref _lastFunction, function);
		}

		function.Node.Append(builder, context);
	}

	/// <summary>
	///	Collects column references used by nested function arguments.
	/// </summary>
	/// <param name="columnNames">
	///	The collection receiving referenced column names.
	/// </param>
	public override void CollectColumnReferences(ICollection<string> columnNames)
	{
		foreach (PatternArgument argument in _arguments)
		{
			argument.Expression?.CollectColumnReferences(columnNames);
		}
	}

	private sealed record CachedFunction(string Key, PatternNode Node);
}