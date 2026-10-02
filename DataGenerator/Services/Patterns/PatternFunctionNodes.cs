using System.Globalization;
using System.Text;

namespace DataGenerator.Services.Patterns;

internal static class PatternNumberFormatter
{
	public static void AppendPadded(StringBuilder builder, long value, int digits)
	{
		if (value < 0)
		{
			_ = builder.Append('-');
		}

		_ = builder.Append(Math.Abs(value).ToString(CultureInfo.InvariantCulture).PadLeft(digits, '0'));
	}

	public static int CountDigits(long value) => Math.Abs(value).ToString(CultureInfo.InvariantCulture).Length;
}

internal sealed class SequencePatternNode : PatternNode
{
	private readonly long _minimum;
	private readonly long _maximum;
	private readonly long _start;
	private readonly long _step;
	private readonly int  _digits;

	public SequencePatternNode(long minimum, long maximum, long start, long step, int digits)
	{
		_minimum = minimum;
		_maximum = maximum;
		_start   = start;
		_step    = step;
		_digits  = digits;
	}

	public override void Append(StringBuilder builder, PatternContext context)
	{
		Int128 size    = (Int128)_maximum - _minimum + 1;
		Int128 offset  = (Int128)_start - _minimum + ((Int128)_step * context.RowIndex);
		Int128 wrapped = ((offset % size) + size) % size;

		PatternNumberFormatter.AppendPadded(builder, (long)(_minimum + wrapped), _digits);
	}
}

internal sealed class RandomNumberPatternNode : PatternNode
{
	private readonly long _minimum;
	private readonly long _maximum;
	private readonly int  _digits;

	public RandomNumberPatternNode(long minimum, long maximum, int digits)
	{
		_minimum = minimum;
		_maximum = maximum;
		_digits  = digits;
	}

	public override void Append(StringBuilder builder, PatternContext context)
		=> PatternNumberFormatter.AppendPadded(builder, context.Random.NextInt64(_minimum, _maximum + 1), _digits);
}

internal sealed class RandomDecimalPatternNode : PatternNode
{
	private readonly decimal _minimum;
	private readonly decimal _maximum;
	private readonly int     _decimals;

	public RandomDecimalPatternNode(decimal minimum, decimal maximum, int decimals)
	{
		_minimum  = minimum;
		_maximum  = maximum;
		_decimals = decimals;
	}

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

	public RandomTextPatternNode(string alphabet, int minimumLength, int maximumLength)
	{
		_alphabet      = alphabet;
		_minimumLength = minimumLength;
		_maximumLength = maximumLength;
	}

	public override void Append(StringBuilder builder, PatternContext context)
	{
		int length = context.Random.Next(_minimumLength, _maximumLength + 1);

		for (int index = 0; index < length; ++index)
		{
			_ = builder.Append(_alphabet[context.Random.Next(_alphabet.Length)]);
		}

		PatternContext.EnsureLength(builder);
	}
}

internal sealed class RandomDatePatternNode : PatternNode
{
	private readonly DateTime _minimum;
	private readonly DateTime _maximum;
	private readonly string   _format;

	public RandomDatePatternNode(DateTime minimum, DateTime maximum, string format)
	{
		_minimum = minimum;
		_maximum = maximum;
		_format  = format;
	}

	public override void Append(StringBuilder builder, PatternContext context)
	{
		DateTime value = new DateTime(context.Random.NextInt64(_minimum.Ticks, _maximum.Ticks + 1));

		_ = builder.Append(value.ToString(_format, CultureInfo.InvariantCulture));
	}
}

/// <summary>
/// Today's date, either with the time the value is generated (NOW) or with a random time of the day (ANY).
/// </summary>
internal sealed class TodayPatternNode : PatternNode
{
	private readonly bool   _anyTime;
	private readonly string _format;

	public TodayPatternNode(bool anyTime, string format)
	{
		_anyTime = anyTime;
		_format  = format;
	}

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
	private readonly bool _upperCase;

	public GuidPatternNode(bool upperCase)
	{
		_upperCase = upperCase;
	}

	public override void Append(StringBuilder builder, PatternContext context)
	{
		string text = Guid.NewGuid().ToString("D");

		_ = builder.Append(_upperCase ? text.ToUpperInvariant() : text);
	}
}