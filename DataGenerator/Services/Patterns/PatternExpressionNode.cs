using System.Globalization;
using System.Text;

namespace DataGenerator.Services.Patterns;

internal sealed class PatternExpressionNode : PatternNode
{
	#region FIELDS
	private readonly IReadOnlyList<PatternArgument> _arguments;
	private readonly int                           _position;
	#endregion FIELDS

	#region CONSTRUCTOR
	public PatternExpressionNode(PatternToken token, IReadOnlyList<PatternArgument> arguments)
	{
		FunctionName = token.Text.ToUpperInvariant();
		_arguments   = arguments;
		_position    = token.Position;

		int count = FunctionName switch
		{
			"IF" or "SUBSTRING" => 3,
			"NOT" or "IS_NULL"  => 1,
			"AND" or "OR"       => -1,
			_                  => 2
		};

		if ((count >= 0 && arguments.Count != count) || (count < 0 && arguments.Count < 2))
		{
			throw Error(count < 0 ? "Requires at least two arguments." : $"Requires exactly {count} arguments.");
		}

		if (arguments.Any(argument => argument.Name is not null || argument.Kind == PatternArgumentKind.Range))
		{
			throw Error("Use positional values or nested functions, not named arguments or ranges.");
		}

		int conditionCount = FunctionName switch
		{
			"IF" or "NOT" => 1,
			"AND" or "OR" => arguments.Count,
			_             => 0
		};

		for (int index = 0; index < conditionCount; ++index)
		{
			if (arguments[index].Expression is null)
			{
				_ = ReadBoolean(ReadValue(index, null));
			}
		}

		// Check literal bounds without evaluating row-dependent sources or unused branches.
		if (FunctionName == "SUBSTRING")
		{
			for (int index = 1; index < 3; ++index)
			{
				if (arguments[index].Expression is null)
				{
					_ = ReadIndex(ReadValue(index, null), index == 1 ? 1 : 0);
				}
			}
		}
	}
	#endregion CONSTRUCTOR

	#region METHODS
	#region PUBLIC
	public static bool      IsFunction(string name)
		=> name.ToUpperInvariant() is "IF" or "SUBSTRING" or "EQ" or "NE" or "GT" or "GE" or "LT" or "LE"
			or "AND" or "OR" or "NOT" or "IS_NULL" or "CONTAINS" or "STARTS_WITH" or "ENDS_WITH";

	public override void    Append(StringBuilder builder, PatternContext context)
	{
		_ = builder.Append(FormatValue(Evaluate(context)));
		PatternContext.EnsureLength(builder);
	}

	public override object? Evaluate(PatternContext context)
	{
		switch (FunctionName)
		{
			case "IF":
				return ReadValue(ReadBoolean(ReadValue(0, context)) ? 1 : 2, context);
			case "AND":
				for (int index = 0; index < _arguments.Count; ++index)
				{
					if (!ReadBoolean(ReadValue(index, context)))
					{
						return false;
					}
				}
				return true;
			case "OR":
				for (int index = 0; index < _arguments.Count; ++index)
				{
					if (ReadBoolean(ReadValue(index, context)))
					{
						return true;
					}
				}
				return false;
			case "NOT":
				return !ReadBoolean(ReadValue(0, context));
			case "IS_NULL":
				return ReadValue(0, context) is null;
			case "SUBSTRING":
			{
				string text   = FormatValue(ReadValue(0, context));
				int    start  = ReadIndex(ReadValue(1, context), 1) - 1;
				int    length = ReadIndex(ReadValue(2, context), 0);

				return start >= text.Length ? string.Empty : text.Substring(start, Math.Min(length, text.Length - start));
			}
			default:
				return Compare(ReadValue(0, context), ReadValue(1, context));
		}
	}

	public override void    CollectColumnReferences(ICollection<string> columnNames)
	{
		foreach (PatternArgument argument in _arguments)
		{
			argument.Expression?.CollectColumnReferences(columnNames);
		}
	}
	#endregion PUBLIC

	#region PRIVATE
	private static string         FormatValue(object? value)
		=> value is bool boolean ? (boolean ? "true" : "false") : Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;

	private static bool           TryNumber(object? value, out decimal number)
	{
		if (value is byte or sbyte or short or ushort or int or uint or long or ulong or decimal)
		{
			number = Convert.ToDecimal(value, CultureInfo.InvariantCulture);
			return true;
		}

		if (value is float or double or string)
		{
			return decimal.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture),
				NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out number);
		}

		number = 0;
		return false;
	}

	private object?               ReadValue(int index, PatternContext? context)
	{
		PatternArgument argument = _arguments[index];

		if (argument.Expression is not null)
		{
			return argument.Expression.Evaluate(context ?? throw Error("This value requires a row context."));
		}

		return argument.Kind switch
		{
			PatternArgumentKind.Null    => null,
			PatternArgumentKind.Boolean => string.Equals(argument.Text, "TRUE", StringComparison.OrdinalIgnoreCase),
			PatternArgumentKind.Number  => argument.Number,
			_                           => argument.Text
		};
	}

	private bool                  ReadBoolean(object? value)
	{
		if (value is bool boolean)
		{
			return boolean;
		}

		if (value is string text && bool.TryParse(text, out bool parsed))
		{
			return parsed;
		}

		throw Error("Conditions must be boolean values (TRUE/FALSE or a predicate); numbers and NULL are not truth values.");
	}

	private int                   ReadIndex(object? value, int minimum)
	{
		if (!TryNumber(value, out decimal number) || number != decimal.Truncate(number) || number < minimum || number > int.MaxValue)
		{
			throw Error($"Index/length must be a whole number from {minimum} to {int.MaxValue}.");
		}

		return (int)number;
	}

	private bool                  Compare(object? left, object? right)
	{
		if (FunctionName is "CONTAINS" or "STARTS_WITH" or "ENDS_WITH")
		{
			if (left is not string text || right is not string part)
			{
				throw Error("Text predicates require two non-null text values.");
			}

			return FunctionName switch
			{
				"CONTAINS"    => text.Contains(part, StringComparison.Ordinal),
				"STARTS_WITH" => text.StartsWith(part, StringComparison.Ordinal),
				_             => text.EndsWith(part, StringComparison.Ordinal)
			};
		}

		if (left is null || right is null)
		{
			return FunctionName switch
			{
				"EQ" => left is null && right is null,
				"NE" => !(left is null && right is null),
				_    => throw Error("Ordered comparisons do not accept NULL.")
			};
		}

		int comparison;

		if (left is string leftText && right is string rightText)
		{
			comparison = string.Compare(leftText, rightText, StringComparison.Ordinal);
		}
		else if (TryNumber(left, out decimal leftNumber) && TryNumber(right, out decimal rightNumber))
		{
			comparison = leftNumber.CompareTo(rightNumber);
		}
		else if (left is bool || right is bool)
		{
			if (FunctionName is not ("EQ" or "NE"))
			{
				throw Error("Booleans support only EQ and NE.");
			}

			comparison = ReadBoolean(left).CompareTo(ReadBoolean(right));
		}
		else if (left.GetType() == right.GetType() && left is DateTime or DateTimeOffset or TimeSpan)
		{
			comparison = ((IComparable)left).CompareTo(right);
		}
		else if (left is Guid leftGuid && right is Guid rightGuid && FunctionName is "EQ" or "NE")
		{
			comparison = leftGuid.CompareTo(rightGuid);
		}
		else
		{
			throw Error("The operands have incompatible types.");
		}

		return FunctionName switch
		{
			"EQ" => comparison == 0,
			"NE" => comparison != 0,
			"GT" => comparison > 0,
			"GE" => comparison >= 0,
			"LT" => comparison < 0,
			_    => comparison <= 0
		};
	}

	private PatternSyntaxException Error(string message) => new($"{FunctionName}: {message}", _position);
	#endregion PRIVATE
	#endregion METHODS
}
