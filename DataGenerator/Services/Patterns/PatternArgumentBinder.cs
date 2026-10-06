using System.Globalization;

namespace DataGenerator.Services.Patterns;

internal enum PatternArgumentKind
{
	Number     = 0,
	Range      = 1,
	Text       = 2,
	Expression = 3
}

internal sealed class PatternArgument
{
	private static readonly CultureInfo INVARIANT = CultureInfo.InvariantCulture;

	public required string?             Name        { get; init; }
	public required PatternArgumentKind Kind        { get; init; }
	public required string              Text        { get; init; }
	public required int                 Position    { get; init; }
	public decimal                      Number      { get; init; }
	public decimal                      RangeStart  { get; init; }
	public decimal                      RangeEnd    { get; init; }

	/// <summary>
	/// The nested function call of an <see cref="PatternArgumentKind.Expression"/> argument, e.g. TODAY() in RAND_DATE(TODAY(), '2030-12-31').
	/// </summary>
	public PatternNode?                 Expression  { get; init; }

	/// <summary>
	/// Whether the value was produced by a nested function call. Such a value is accepted wherever text is expected,
	/// even when it looks like a number (e.g. TODAY(format='yyyyMMdd')).
	/// </summary>
	public bool                         IsEvaluated { get; init; }

	/// <summary>
	/// The argument with the value its nested function call produced: a number when the value is a number, otherwise text.
	/// </summary>
	public PatternArgument WithEvaluatedValue(string value)
	{
		bool isNumber = decimal.TryParse(
			value,
			NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
			INVARIANT,
			out decimal number
		);

		return new PatternArgument
		{
			Name        = Name,
			Kind        = isNumber ? PatternArgumentKind.Number : PatternArgumentKind.Text,
			Text        = value,
			Position    = Position,
			Number      = isNumber ? number : 0,
			IsEvaluated = true
		};
	}
}

/// <summary>
/// Binds positional and named function arguments (e.g. <c>SEQ(1-1000, start=5, digits=6)</c>) to parameters.
/// A range parameter accepts either a range (<c>1-1000</c>, <c>1..1000</c>, <c>1 TO 1000</c>) or two numbers (<c>1, 1000</c>).
/// </summary>
internal sealed class PatternArgumentBinder
{
	public const long MAXIMUM_MAGNITUDE = 1_000_000_000_000_000_000;

	private static readonly CultureInfo INVARIANT = CultureInfo.InvariantCulture;

	private readonly string                              _functionName;
	private readonly int                                 _position;
	private readonly List<PatternArgument>               _positional = [];
	private readonly Dictionary<string, PatternArgument> _named      = new(StringComparer.OrdinalIgnoreCase);
	private int                                          _nextPositional;

	public PatternArgumentBinder(string functionName, IReadOnlyList<PatternArgument> arguments, int position)
	{
		_functionName = functionName;
		_position     = position;

		foreach (PatternArgument argument in arguments)
		{
			if (argument.Name is null)
			{
				if (_named.Count > 0)
				{
					throw new PatternSyntaxException(
						$"In {_functionName}(...), unnamed values must come before named values such as digits=4.",
						argument.Position
					);
				}

				_positional.Add(argument);
			}
			else if (!_named.TryAdd(argument.Name, argument))
			{
				throw new PatternSyntaxException($"'{argument.Name}' is given more than once in {_functionName}(...).", argument.Position);
			}
		}
	}

	public PatternSyntaxException Error(string message) => new PatternSyntaxException($"{_functionName}: {message}", _position);

	public (decimal Minimum, decimal Maximum) RequireRange(string example)
	{
		if (TryTakeNamed("range", out PatternArgument? namedRange))
		{
			return ToRange(namedRange, example);
		}

		if (_named.ContainsKey("min") || _named.ContainsKey("max"))
		{
			decimal minimum = RequireNamedNumber("min");
			decimal maximum = RequireNamedNumber("max");

			return ValidateRange(minimum, maximum);
		}

		PatternArgument first = TakePositional() ?? throw Error($"a range is required, e.g. {example}.");

		if (first.Kind == PatternArgumentKind.Range)
		{
			return ToRange(first, example);
		}

		if (first.Kind == PatternArgumentKind.Number && PeekPositional() is { Kind: PatternArgumentKind.Number } second)
		{
			_ = TakePositional();
			return ValidateRange(first.Number, second.Number);
		}

		throw new PatternSyntaxException($"{_functionName}: expected a range, e.g. {example}.", first.Position);
	}

	public (decimal Minimum, decimal Maximum) RequireLength(string example)
	{
		PatternArgument? argument =
			TryTakeNamed("length", out PatternArgument? namedLength)
				? namedLength
				: TakePositional();

		if (argument is null)
		{
			throw Error($"a length is required, e.g. {example}.");
		}

		if (argument.Kind == PatternArgumentKind.Range)
		{
			return ToRange(argument, example);
		}

		if (argument.Kind != PatternArgumentKind.Number)
		{
			throw new PatternSyntaxException($"{_functionName}: expected a length, e.g. {example}.", argument.Position);
		}

		if (argument.Name is null && PeekPositional() is { Kind: PatternArgumentKind.Number } maximumLength)
		{
			_ = TakePositional();
			return ValidateRange(argument.Number, maximumLength.Number);
		}

		return (argument.Number, argument.Number);
	}

	public decimal? OptionalNumber(string parameterName)
	{
		PatternArgument? argument =
			TryTakeNamed(parameterName, out PatternArgument? namedArgument)
				? namedArgument
				: TakePositional();

		if (argument is null)
		{
			return null;
		}

		return argument.Kind == PatternArgumentKind.Number
			? argument.Number
			: throw new PatternSyntaxException($"{_functionName}: {parameterName} must be a number.", argument.Position);
	}

	public string? OptionalText(string parameterName)
	{
		PatternArgument? argument =
			TryTakeNamed(parameterName, out PatternArgument? namedArgument)
				? namedArgument
				: TakePositional();

		if (argument is null)
		{
			return null;
		}

		return argument.Kind == PatternArgumentKind.Text || argument.IsEvaluated
			? argument.Text
			: throw new PatternSyntaxException($"{_functionName}: {parameterName} must be text, e.g. '{argument.Text}'.", argument.Position);
	}

	public string RequireText(string parameterName, string example)
		=> OptionalText(parameterName) ?? throw Error($"{parameterName} is required, e.g. {example}.");

	/// <summary>
	/// An optional value that may be text or a number, used as text.
	/// </summary>
	public string? OptionalValue(string parameterName)
	{
		PatternArgument? argument =
			TryTakeNamed(parameterName, out PatternArgument? namedArgument)
				? namedArgument
				: TakePositional();

		if (argument is null)
		{
			return null;
		}

		return argument.Kind is PatternArgumentKind.Text or PatternArgumentKind.Number
			? argument.Text
			: throw new PatternSyntaxException($"{_functionName}: {parameterName} must be text or a number.", argument.Position);
	}

	public string RequireValue(string parameterName, string example)
		=> OptionalValue(parameterName) ?? throw Error($"{parameterName} is required, e.g. {example}.");

	/// <summary>
	/// Whether a range is given, either positionally or as range=, min= or max=.
	/// </summary>
	public bool HasRangeArgument()
		=> PeekPositional() is not null || _named.ContainsKey("range") || _named.ContainsKey("min") || _named.ContainsKey("max");

	public IReadOnlyList<PatternArgument> TakeRemainingPositional()
	{
		List<PatternArgument> remaining = [.. _positional.Skip(_nextPositional)];

		_nextPositional = _positional.Count;
		return remaining;
	}

	public long ToWholeNumber(decimal value, string description)
	{
		if (value != decimal.Truncate(value))
		{
			throw Error($"{description} must be a whole number.");
		}

		if (Math.Abs(value) > MAXIMUM_MAGNITUDE)
		{
			throw Error($"{description} must be between -{MAXIMUM_MAGNITUDE.ToString("N0", INVARIANT)} and {MAXIMUM_MAGNITUDE.ToString("N0", INVARIANT)}.");
		}

		return (long)value;
	}

	public int ToCount(decimal value, string description, int minimum, int maximum)
	{
		if (value != decimal.Truncate(value) || value < minimum || value > maximum)
		{
			throw Error($"{description} must be a whole number from {minimum} to {maximum}.");
		}

		return (int)value;
	}

	public void EnsureComplete(params string[] parameterNames)
	{
		if (_nextPositional < _positional.Count)
		{
			throw new PatternSyntaxException(
				$"{_functionName} received too many values. Parameters: {string.Join(", ", parameterNames)}.",
				_positional[_nextPositional].Position
			);
		}

		foreach (PatternArgument argument in _named.Values)
		{
			throw new PatternSyntaxException(
				$"{_functionName} has no parameter named '{argument.Name}'. Parameters: {string.Join(", ", parameterNames)}.",
				argument.Position
			);
		}
	}

	private bool TryTakeNamed(string parameterName, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out PatternArgument? argument)
		=> _named.Remove(parameterName, out argument);

	private PatternArgument? PeekPositional()
		=> _nextPositional < _positional.Count ? _positional[_nextPositional] : null;

	private PatternArgument? TakePositional()
	{
		PatternArgument? argument = PeekPositional();

		if (argument is not null)
		{
			++_nextPositional;
		}

		return argument;
	}

	private decimal RequireNamedNumber(string parameterName)
	{
		if (!TryTakeNamed(parameterName, out PatternArgument? argument))
		{
			throw Error($"{parameterName} is required when min/max are used.");
		}

		return argument.Kind == PatternArgumentKind.Number
			? argument.Number
			: throw new PatternSyntaxException($"{_functionName}: {parameterName} must be a number.", argument.Position);
	}

	private (decimal Minimum, decimal Maximum) ToRange(PatternArgument argument, string example)
	{
		return argument.Kind switch
		{
			PatternArgumentKind.Range  => ValidateRange(argument.RangeStart, argument.RangeEnd),
			PatternArgumentKind.Number => (argument.Number, argument.Number),
			_                          => throw new PatternSyntaxException($"{_functionName}: expected a range, e.g. {example}.", argument.Position)
		};
	}

	private (decimal Minimum, decimal Maximum) ValidateRange(decimal minimum, decimal maximum)
	{
		if (minimum > maximum)
		{
			throw Error(
				$"the range {minimum.ToString(INVARIANT)} to {maximum.ToString(INVARIANT)} is reversed. "
				+ $"Write the smaller value first, e.g. {maximum.ToString(INVARIANT)}-{minimum.ToString(INVARIANT)}."
			);
		}

		return (minimum, maximum);
	}
}