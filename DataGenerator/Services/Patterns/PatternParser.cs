using System.Globalization;

namespace DataGenerator.Services.Patterns;

/// <summary>
///	Recursive-descent parser for the pattern language. Precedence from lowest to highest:
///	concatenation (FOLLOWED BY, THEN, +), choice (OR, |), repetition (REPEATED n [TO m] [TIMES]),
///	comparison (GREATER THAN, LESS THAN, AT LEAST, AT MOST) and primaries.
/// </summary>
internal sealed class PatternParser
{
	#region FIELDS
	#region PUBLIC
	public const int MAXIMUM_REPETITIONS = 1000;
	#endregion PUBLIC

	#region PRIVATE
	private static readonly string[] RESERVED_WORDS;

	private readonly IReadOnlyList<PatternToken> _tokens;
	private int                                  _index;
	#endregion PRIVATE
	#endregion FIELDS

	#region PROPERTIES
	#region PRIVATE
	private PatternToken Current => _tokens[_index];

	private PatternToken Next => _tokens[Math.Min(_index + 1, _tokens.Count - 1)];
	#endregion PRIVATE
	#endregion PROPERTIES

	#region CONSTRUCTORS
	#region STATIC
	/// <summary>
	///	Sets the default values of the static fields and properties of <see cref="PatternParser"/>.
	/// </summary>
	static PatternParser()
	{
		RESERVED_WORDS = ["FOLLOWED", "BY", "THEN", "OR", "REPEATED", "TO", "TIMES"];
	}
	#endregion STATIC

	#region PRIVATE
	/// <summary>
	///	Creates a parser over an already-tokenized pattern expression.
	/// </summary>
	/// <param name="tokens">
	///	The tokens to parse, including the final End token.
	/// </param>
	private PatternParser(IReadOnlyList<PatternToken> tokens)
	{
		_index = 0;

		_tokens = tokens;
	}
	#endregion PRIVATE
	#endregion CONSTRUCTORS

	#region METHODS
	#region PUBLIC
	/// <summary>
	///	Parses a full pattern expression into an executable pattern tree.
	/// </summary>
	/// <param name="expression">
	///	The pattern expression to parse.
	/// </param>
	/// <returns>
	///	The root node of the parsed pattern tree.
	/// </returns>
	/// <exception cref="PatternSyntaxException">
	///	Thrown when the expression is empty, tokenization fails, or the parsed tokens do not form a valid pattern.
	/// </exception>
	public static PatternNode Parse(string expression)
	{
		if (string.IsNullOrWhiteSpace(expression))
		{
			throw new PatternSyntaxException("The pattern is empty. Example: 'P' FOLLOWED BY SEQ(1-1000)", 1);
		}

		PatternParser parser = new(PatternLexer.Tokenize(expression));
		PatternNode   node   = parser.ParseConcatenation();

		parser.ExpectEnd();
		return node;
	}
	#endregion PUBLIC

	#region PRIVATE
	/// <summary>
	///	Moves to the next token, stopping at the final End token.
	/// </summary>
	private void Advance()
	{
		if (_index < _tokens.Count - 1)
		{
			++_index;
		}
	}

	/// <summary>
	///	Parses the lowest-precedence concatenation operators: FOLLOWED BY, THEN and +.
	/// </summary>
	/// <returns>
	///	A single child node when no concatenation is present, otherwise a concatenation node.
	/// </returns>
	/// <exception cref="PatternSyntaxException">
	///	Thrown when a child expression is invalid or FOLLOWED is not followed by BY.
	/// </exception>
	private PatternNode ParseConcatenation()
	{
		List<PatternNode> parts = [ParseChoice()];

		while (TryConsumeConcatenationOperator())
		{
			parts.Add(ParseChoice());
		}

		return parts.Count == 1 ? parts[0] : new ConcatenationPatternNode(parts);
	}

	/// <summary>
	///	Consumes a concatenation operator if the current token begins one.
	/// </summary>
	/// <returns>
	///	<see langword="true"/> when an operator was consumed; otherwise <see langword="false"/>.
	/// </returns>
	/// <exception cref="PatternSyntaxException">
	///	Thrown when FOLLOWED is not immediately followed by BY.
	/// </exception>
	private bool TryConsumeConcatenationOperator()
	{
		if (Current.Kind == PatternTokenKind.Plus || Current.IsKeyword("THEN"))
		{
			Advance();
			return true;
		}

		if (!Current.IsKeyword("FOLLOWED"))
		{
			return false;
		}

		Advance();

		if (!Current.IsKeyword("BY"))
		{
			throw new PatternSyntaxException("Expected BY after FOLLOWED.", Current.Position);
		}

		Advance();
		return true;
	}

	/// <summary>
	///	Parses OR and | choice operators.
	/// </summary>
	/// <returns>
	///	A single child node when no choice is present, otherwise a choice node.
	/// </returns>
	/// <exception cref="PatternSyntaxException">
	///	Thrown when an option expression is invalid.
	/// </exception>
	private PatternNode ParseChoice()
	{
		List<PatternNode> options = [ParseRepetition()];

		while (Current.Kind == PatternTokenKind.Pipe || Current.IsKeyword("OR"))
		{
			Advance();
			options.Add(ParseRepetition());
		}

		return options.Count == 1 ? options[0] : new ChoicePatternNode(options);
	}

	/// <summary>
	///	Parses REPEATED counts, count ranges and the optional TIMES keyword.
	/// </summary>
	/// <returns>
	///	The parsed child node, wrapped in repetition nodes for each REPEATED clause.
	/// </returns>
	/// <exception cref="PatternSyntaxException">
	///	Thrown when a repetition count is invalid or a count range is reversed.
	/// </exception>
	private PatternNode ParseRepetition()
	{
		PatternNode node = ParseComparison();

		while (Current.IsKeyword("REPEATED"))
		{
			Advance();

			int minimumCount = ReadRepetitionCount();
			int maximumCount = minimumCount;

			if (Current.IsKeyword("TO") || Current.Kind is PatternTokenKind.Minus or PatternTokenKind.Range)
			{
				Advance();
				maximumCount = ReadRepetitionCount();
			}

			if (maximumCount < minimumCount)
			{
				throw new PatternSyntaxException(
					$"REPEATED {minimumCount} TO {maximumCount} is reversed. Write the smaller count first.",
					Current.Position
				);
			}

			if (Current.IsKeyword("TIMES"))
			{
				Advance();
			}

			node = new RepetitionPatternNode(node, minimumCount, maximumCount);
		}

		return node;
	}

	/// <summary>
	///	Parses a primary followed by comparisons that narrow NUM or RAND_NUM ranges.
	/// </summary>
	/// <returns>
	///	The primary node, or a random-number node narrowed by the parsed comparisons.
	/// </returns>
	/// <exception cref="PatternSyntaxException">
	///	Thrown when a comparison follows a non-number function or leaves no possible values.
	/// </exception>
	private PatternNode ParseComparison()
	{
		PatternNode node = ParsePrimary();

		while (TryReadComparison(out string comparison, out int position))
		{
			long bound = ReadComparisonBound(comparison);

			if (node is not RandomNumberPatternNode numberNode)
			{
				throw new PatternSyntaxException(
					$"{comparison} can only follow NUM(...) or RAND_NUM(...), e.g. NUM(digits=5) GREATER THAN 50.",
					position
				);
			}

			(long minimum, long maximum) = comparison switch
			{
				"GREATER THAN" => (Math.Max(numberNode.Minimum, bound + 1), numberNode.Maximum),
				"LESS THAN"    => (numberNode.Minimum, Math.Min(numberNode.Maximum, bound - 1)),
				"AT LEAST"     => (Math.Max(numberNode.Minimum, bound), numberNode.Maximum),
				_              => (numberNode.Minimum, Math.Min(numberNode.Maximum, bound))
			};

			if (minimum > maximum)
			{
				throw new PatternSyntaxException(
					$"{comparison} {bound} leaves no numbers in the range {numberNode.Minimum} to {numberNode.Maximum}.",
					position
				);
			}

			node = numberNode.WithRange(minimum, maximum);
		}

		return node;
	}

	/// <summary>
	///	Reads a comparison keyword pair such as GREATER THAN or AT MOST.
	/// </summary>
	/// <param name="comparison">
	///	The normalised comparison text when one is found; otherwise an empty string.
	/// </param>
	/// <param name="position">
	///	The one-based token position where the comparison started.
	/// </param>
	/// <returns>
	///	<see langword="true"/> when a comparison was consumed; otherwise <see langword="false"/>.
	/// </returns>
	private bool TryReadComparison(out string comparison, out int position)
	{
		position = Current.Position;

		if ((Current.IsKeyword("GREATER") || Current.IsKeyword("LESS")) && Next.IsKeyword("THAN"))
		{
			comparison = $"{Current.Text.ToUpperInvariant()} THAN";
		}
		else if (Current.IsKeyword("AT") && (Next.IsKeyword("LEAST") || Next.IsKeyword("MOST")))
		{
			comparison = $"AT {Next.Text.ToUpperInvariant()}";
		}
		else
		{
			comparison = string.Empty;
			return false;
		}

		Advance();
		Advance();
		return true;
	}

	/// <summary>
	///	Reads the signed whole-number bound that follows a comparison.
	/// </summary>
	/// <param name="comparison">
	///	The comparison text used in error messages.
	/// </param>
	/// <returns>
	///	The signed comparison bound.
	/// </returns>
	/// <exception cref="PatternSyntaxException">
	///	Thrown when the bound is missing, fractional, too large or not a number.
	/// </exception>
	private long ReadComparisonBound(string comparison)
	{
		bool isNegative = false;

		if (Current.Kind == PatternTokenKind.Minus)
		{
			isNegative = true;
			Advance();
		}

		PatternToken token = Current;

		if (token.Kind != PatternTokenKind.Number
			|| !long.TryParse(token.Text, NumberStyles.None, CultureInfo.InvariantCulture, out long bound)
			|| bound > PatternArgumentBinder.MAXIMUM_MAGNITUDE)
		{
			throw new PatternSyntaxException($"Expected a whole number after {comparison}, e.g. {comparison} 50.", token.Position);
		}

		Advance();
		return isNegative ? -bound : bound;
	}

	/// <summary>
	///	Reads a REPEATED count within the allowed range.
	/// </summary>
	/// <returns>
	///	The parsed repetition count.
	/// </returns>
	/// <exception cref="PatternSyntaxException">
	///	Thrown when the current token is not a whole number from 0 to the maximum repetition count.
	/// </exception>
	private int ReadRepetitionCount()
	{
		PatternToken token = Current;

		if (token.Kind != PatternTokenKind.Number
			|| !int.TryParse(token.Text, NumberStyles.None, CultureInfo.InvariantCulture, out int count)
			|| count > MAXIMUM_REPETITIONS)
		{
			throw new PatternSyntaxException(
				$"Expected a whole number from 0 to {MAXIMUM_REPETITIONS} after REPEATED, e.g. X REPEATED 3 TIMES.",
				token.Position
			);
		}

		Advance();
		return count;
	}

	/// <summary>
	///	Parses a primary pattern part: grouped expression, quoted text, number, bare word or function call.
	/// </summary>
	/// <returns>
	///	The node represented by the primary expression.
	/// </returns>
	/// <exception cref="PatternSyntaxException">
	///	Thrown when the primary is missing, a reserved word is used as text, or a group is malformed.
	/// </exception>
	private PatternNode ParsePrimary()
	{
		PatternToken token = Current;

		switch (token.Kind)
		{
			case PatternTokenKind.LeftParenthesis:
			{
				Advance();
				PatternNode inner = ParseConcatenation();

				if (Current.Kind == PatternTokenKind.End)
				{
					throw new PatternSyntaxException($"The '(' at position {token.Position} is missing its closing ')'.", Current.Position);
				}

				if (Current.Kind != PatternTokenKind.RightParenthesis)
				{
					throw CreateMissingOperatorException();
				}

				Advance();
				return inner;
			}

			case PatternTokenKind.Text:
			case PatternTokenKind.Number:
			{
				Advance();
				return new LiteralPatternNode(token.Text);
			}

			case PatternTokenKind.Word:
			{
				if (RESERVED_WORDS.Contains(token.Text, StringComparer.OrdinalIgnoreCase))
				{
					throw new PatternSyntaxException(
						$"'{token.Text}' is a keyword, but text, a number, a function or '(' was expected here. "
						+ $"To use it as text, put it in quotes: '{token.Text}'.",
						token.Position
					);
				}

				Advance();
				return
					Current.Kind == PatternTokenKind.LeftParenthesis
						? ParseFunction(token)
						: new LiteralPatternNode(token.Text);
			}

			case PatternTokenKind.End:
			{
				throw new PatternSyntaxException("The pattern ended early. Expected text, a number, a function or '('.", token.Position);
			}

			default:
			{
				throw new PatternSyntaxException(
					$"Unexpected '{token.Text}'. Expected text, a number, a function or '('. Put symbols in quotes, e.g. '{token.Text}'.",
					token.Position
				);
			}
		}
	}

	/// <summary>
	///	Parses a function call after its name has been consumed.
	/// </summary>
	/// <param name="nameToken">
	///	The token containing the function name and source position.
	/// </param>
	/// <returns>
	///	The node created for the parsed function call.
	/// </returns>
	/// <exception cref="PatternSyntaxException">
	///	Thrown when arguments are malformed, the closing parenthesis is missing, or the function is invalid.
	/// </exception>
	private PatternNode ParseFunction(PatternToken nameToken)
	{
		List<PatternArgument> arguments = [];

		Advance();

		if (Current.Kind is not (PatternTokenKind.RightParenthesis or PatternTokenKind.End))
		{
			arguments.Add(ParseArgument(nameToken));

			while (Current.Kind == PatternTokenKind.Comma)
			{
				Advance();
				arguments.Add(ParseArgument(nameToken));
			}
		}

		if (Current.Kind == PatternTokenKind.End)
		{
			throw new PatternSyntaxException(
				$"The {nameToken.Text.ToUpperInvariant()}( at position {nameToken.Position} is missing its closing ')'.",
				Current.Position
			);
		}

		if (Current.Kind != PatternTokenKind.RightParenthesis)
		{
			throw new PatternSyntaxException(
				$"Expected ',' or ')' inside {nameToken.Text.ToUpperInvariant()}(...). Text and dates must be in quotes, e.g. '2024-12-31'.",
				Current.Position
			);
		}

		Advance();
		return PatternFunctionFactory.Create(nameToken, arguments);
	}

	/// <summary>
	///	Parses one function argument, including optional name, nested function expression, text, number or range.
	/// </summary>
	/// <param name="nameToken">
	///	The function name token used in argument error messages.
	/// </param>
	/// <returns>
	///	The parsed argument with its source position and value kind.
	/// </returns>
	/// <exception cref="PatternSyntaxException">
	///	Thrown when the argument value is missing or cannot be parsed.
	/// </exception>
	private PatternArgument ParseArgument(PatternToken nameToken)
	{
		string? name     = null;
		int     position = Current.Position;

		if (Current.Kind == PatternTokenKind.Word && Next.Kind == PatternTokenKind.Assign)
		{
			name = Current.Text;
			Advance();
			Advance();
		}

		PatternToken valueToken = Current;

		if (valueToken.Kind == PatternTokenKind.Word && Next.Kind == PatternTokenKind.LeftParenthesis)
		{
			Advance();

			PatternNode expression = ParseFunction(valueToken);

			return new PatternArgument
			{
				Name       = name,
				Kind       = PatternArgumentKind.Expression,
				Text       = $"{valueToken.Text.ToUpperInvariant()}(…)",
				Position   = position,
				Expression = expression
			};
		}

		if (valueToken.Kind is PatternTokenKind.Text or PatternTokenKind.Word)
		{
			Advance();
			return new PatternArgument
			{
				Name     = name,
				Kind     = PatternArgumentKind.Text,
				Text     = valueToken.Text,
				Position = position
			};
		}

		(decimal firstValue, string firstText) = ReadSignedNumber(nameToken);

		if (Current.Kind is PatternTokenKind.Minus or PatternTokenKind.Range || Current.IsKeyword("TO"))
		{
			Advance();

			(decimal secondValue, string secondText) = ReadSignedNumber(nameToken);

			return new PatternArgument
			{
				Name       = name,
				Kind       = PatternArgumentKind.Range,
				Text       = $"{firstText}-{secondText}",
				Position   = position,
				RangeStart = firstValue,
				RangeEnd   = secondValue
			};
		}

		return new PatternArgument
		{
			Name     = name,
			Kind     = PatternArgumentKind.Number,
			Text     = firstText,
			Position = position,
			Number   = firstValue
		};
	}

	/// <summary>
	///	Reads a decimal number with an optional leading minus sign for use in function arguments.
	/// </summary>
	/// <param name="nameToken">
	///	The function name token used in error messages.
	/// </param>
	/// <returns>
	///	The numeric value and the source text including any minus sign.
	/// </returns>
	/// <exception cref="PatternSyntaxException">
	///	Thrown when the current token is not a number, the number is too large, or a range end is a function call.
	/// </exception>
	private (decimal Value, string Text) ReadSignedNumber(PatternToken nameToken)
	{
		bool isNegative = false;

		if (Current.Kind == PatternTokenKind.Minus)
		{
			isNegative = true;
			Advance();
		}

		PatternToken token = Current;

		if (token.Kind == PatternTokenKind.Word && Next.Kind == PatternTokenKind.LeftParenthesis)
		{
			throw new PatternSyntaxException(
				$"A function cannot be one end of a range inside {nameToken.Text.ToUpperInvariant()}(...). "
				+ $"Separate the two values with a comma instead, e.g. {nameToken.Text.ToUpperInvariant()}(1, {token.Text.ToUpperInvariant()}(...)).",
				token.Position
			);
		}

		if (token.Kind != PatternTokenKind.Number)
		{
			throw new PatternSyntaxException(
				$"Expected a number, a range such as 1-100, or quoted text inside {nameToken.Text.ToUpperInvariant()}(...).",
				token.Position
			);
		}

		if (!decimal.TryParse(token.Text, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out decimal value))
		{
			throw new PatternSyntaxException($"The number {token.Text} is too large.", token.Position);
		}

		Advance();
		return isNegative ? (-value, $"-{token.Text}") : (value, token.Text);
	}

	/// <summary>
	///	Checks that parsing consumed the whole token stream.
	/// </summary>
	/// <exception cref="PatternSyntaxException">
	///	Thrown when extra tokens remain or a closing parenthesis has no matching opening parenthesis.
	/// </exception>
	private void ExpectEnd()
	{
		if (Current.Kind == PatternTokenKind.End)
		{
			return;
		}

		if (Current.Kind == PatternTokenKind.RightParenthesis)
		{
			throw new PatternSyntaxException("This ')' has no matching '('.", Current.Position);
		}

		throw CreateMissingOperatorException();
	}

	/// <summary>
	///	Builds the error used when two pattern parts appear without a joining operator.
	/// </summary>
	/// <returns>
	///	A syntax exception positioned at the unexpected token.
	/// </returns>
	private PatternSyntaxException CreateMissingOperatorException()
	{
		string found = Current.Kind == PatternTokenKind.Text ? $"'{Current.Text}'" : Current.Text;

		return new PatternSyntaxException(
			$"Expected FOLLOWED BY, THEN, + or OR before {found}. Parts of a pattern must be joined, e.g. A FOLLOWED BY B.",
			Current.Position
		);
	}
	#endregion PRIVATE
	#endregion METHODS
}