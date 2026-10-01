using System.Text;

namespace DataGenerator.Services.Patterns;

internal static class PatternLexer
{
	public static IReadOnlyList<PatternToken> Tokenize(string expression)
	{
		List<PatternToken> tokens = [];
		int                index  = 0;

		while (index < expression.Length)
		{
			char current  = expression[index];
			int  position = index + 1;

			if (char.IsWhiteSpace(current))
			{
				++index;
				continue;
			}

			PatternTokenKind? symbolKind = current switch
			{
				'('        => PatternTokenKind.LeftParenthesis,
				')'        => PatternTokenKind.RightParenthesis,
				','        => PatternTokenKind.Comma,
				'+'        => PatternTokenKind.Plus,
				'|'        => PatternTokenKind.Pipe,
				'-'        => PatternTokenKind.Minus,
				':' or '=' => PatternTokenKind.Assign,
				_          => null
			};

			if (symbolKind.HasValue)
			{
				tokens.Add(new PatternToken(symbolKind.Value, current.ToString(), position));
				++index;
				continue;
			}

			if (current == '.')
			{
				if (index + 1 < expression.Length && expression[index + 1] == '.')
				{
					tokens.Add(new PatternToken(PatternTokenKind.Range, "..", position));
					index += 2;
					continue;
				}

				throw new PatternSyntaxException("A '.' on its own is not allowed here. Put literal text in quotes, e.g. '.'", position);
			}

			if (current is '\'' or '"')
			{
				tokens.Add(ReadQuotedText(expression, ref index));
				continue;
			}

			if (char.IsLetterOrDigit(current) || current == '_')
			{
				tokens.Add(ReadWordOrNumber(expression, ref index));
				continue;
			}

			throw new PatternSyntaxException(
				$"Unexpected character '{current}'. Put literal text and symbols in quotes, e.g. '{current}'.",
				position
			);
		}

		tokens.Add(new PatternToken(PatternTokenKind.End, string.Empty, expression.Length + 1));
		return tokens;
	}

	private static PatternToken ReadQuotedText(string expression, ref int index)
	{
		char          quote    = expression[index];
		int           position = index + 1;
		StringBuilder builder  = new StringBuilder();

		++index;

		while (index < expression.Length)
		{
			char current = expression[index];

			if (current == quote)
			{
				if (index + 1 < expression.Length && expression[index + 1] == quote)
				{
					_ = builder.Append(quote);
					index += 2;
					continue;
				}

				++index;
				return new PatternToken(PatternTokenKind.Text, builder.ToString(), position);
			}

			_ = builder.Append(current);
			++index;
		}

		throw new PatternSyntaxException($"The text starting with {quote} is missing its closing {quote}.", position);
	}

	private static PatternToken ReadWordOrNumber(string expression, ref int index)
	{
		int  start     = index;
		bool hasLetter = false;

		while (index < expression.Length && (char.IsLetterOrDigit(expression[index]) || expression[index] == '_'))
		{
			hasLetter |= !char.IsAsciiDigit(expression[index]);
			++index;
		}

		if (hasLetter)
		{
			return new PatternToken(PatternTokenKind.Word, expression[start..index], start + 1);
		}

		bool hasFraction =
			index + 1 < expression.Length
			&& expression[index] == '.'
			&& char.IsAsciiDigit(expression[index + 1]);

		if (hasFraction)
		{
			++index;

			while (index < expression.Length && char.IsAsciiDigit(expression[index]))
			{
				++index;
			}
		}

		return new PatternToken(PatternTokenKind.Number, expression[start..index], start + 1);
	}
}