using System.Globalization;

namespace DataGenerator.Services.Patterns;

/// <summary>
/// Recursive-descent parser for the pattern language. Precedence from lowest to highest:
/// concatenation (FOLLOWED BY, THEN, +), choice (OR, |), repetition (REPEATED n [TO m] [TIMES]) and primaries.
/// </summary>
internal sealed class PatternParser
{
	public const int MAXIMUM_REPETITIONS = 1000;

	private static readonly string[] RESERVED_WORDS = ["FOLLOWED", "BY", "THEN", "OR", "REPEATED", "TO", "TIMES"];

	private readonly IReadOnlyList<PatternToken> _tokens;
	private int                                  _index;

	private PatternParser(IReadOnlyList<PatternToken> tokens)
	{
		_tokens = tokens;
	}

	private PatternToken Current => _tokens[_index];

	private PatternToken Next => _tokens[Math.Min(_index + 1, _tokens.Count - 1)];

	public static PatternNode Parse(string expression)
	{
		if (string.IsNullOrWhiteSpace(expression))
		{
			throw new PatternSyntaxException("The pattern is empty. Example: 'P' FOLLOWED BY SEQ(1-1000)", 1);
		}

		PatternParser parser = new PatternParser(PatternLexer.Tokenize(expression));
		PatternNode   node   = parser.ParseConcatenation();

		parser.ExpectEnd();
		return node;
	}

	private void Advance()
	{
		if (_index < _tokens.Count - 1)
		{
			++_index;
		}
	}

	private PatternNode ParseConcatenation()
	{
		List<PatternNode> parts = [ParseChoice()];

		while (TryConsumeConcatenationOperator())
		{
			parts.Add(ParseChoice());
		}

		return parts.Count == 1 ? parts[0] : new ConcatenationPatternNode(parts);
	}

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

	private PatternNode ParseRepetition()
	{
		PatternNode node = ParsePrimary();

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

	private PatternNode ParsePrimary()
	{
		PatternToken token = Current;

		switch (token.Kind)
		{
			case PatternTokenKind.LeftParenthesis:
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

			case PatternTokenKind.Text:
			case PatternTokenKind.Number:
				Advance();
				return new LiteralPatternNode(token.Text);

			case PatternTokenKind.Word:
				if (RESERVED_WORDS.Contains(token.Text, StringComparer.OrdinalIgnoreCase))
				{
					throw new PatternSyntaxException(
						$"'{token.Text}' is a keyword, but text, a number, a function or '(' was expected here. "
						+ $"To use it as text, put it in quotes: '{token.Text}'.",
						token.Position
					);
				}

				Advance();
				return Current.Kind == PatternTokenKind.LeftParenthesis
					? ParseFunction(token)
					: new LiteralPatternNode(token.Text);

			case PatternTokenKind.End:
				throw new PatternSyntaxException("The pattern ended early. Expected text, a number, a function or '('.", token.Position);

			default:
				throw new PatternSyntaxException(
					$"Unexpected '{token.Text}'. Expected text, a number, a function or '('. Put symbols in quotes, e.g. '{token.Text}'.",
					token.Position
				);
		}
	}

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

	private (decimal Value, string Text) ReadSignedNumber(PatternToken nameToken)
	{
		bool isNegative = false;

		if (Current.Kind == PatternTokenKind.Minus)
		{
			isNegative = true;
			Advance();
		}

		PatternToken token = Current;

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

	private PatternSyntaxException CreateMissingOperatorException()
	{
		string found = Current.Kind == PatternTokenKind.Text ? $"'{Current.Text}'" : Current.Text;

		return new PatternSyntaxException(
			$"Expected FOLLOWED BY, THEN, + or OR before {found}. Parts of a pattern must be joined, e.g. A FOLLOWED BY B.",
			Current.Position
		);
	}
}