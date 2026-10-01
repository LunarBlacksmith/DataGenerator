using System.Text;

namespace DataGenerator.Services;

public sealed class RegexValueGenerator : IRegexValueGenerator
{
	private const int MAXIMUM_REPEAT_COUNT = 1000;

	public string Generate(string pattern, int maximumLength)
	{
		if (string.IsNullOrWhiteSpace(pattern))
		{
			throw new ArgumentException("A regex pattern is required.", nameof(pattern));
		}

		if (maximumLength < 1)
		{
			throw new ArgumentOutOfRangeException(nameof(maximumLength), "The maximum length must be greater than zero.");
		}

		string normalizedPattern = pattern.Trim();

		if (normalizedPattern.StartsWith('^'))
		{
			normalizedPattern = normalizedPattern[1..];
		}

		if (normalizedPattern.EndsWith('$'))
		{
			normalizedPattern = normalizedPattern[..^1];
		}

		StringBuilder result = new();
		int           index  = 0;

		while (index < normalizedPattern.Length && result.Length < maximumLength)
		{
			string characterSet;
			char   currentCharacter = normalizedPattern[index];

			if (currentCharacter == '[')
			{
				int endingBracketIndex = normalizedPattern.IndexOf(']', index + 1);

				if (endingBracketIndex < 0)
				{
					throw new InvalidOperationException("The regex contains an unclosed character class.");
				}

				string characterClass =
					normalizedPattern.Substring(
						index + 1,
						endingBracketIndex - index - 1
					);

				characterSet = ExpandCharacterClass(characterClass);
				index        = endingBracketIndex + 1;
			}
			else if (currentCharacter == '\\')
			{
				if (index + 1 >= normalizedPattern.Length)
				{
					throw new InvalidOperationException("The regex ends with an incomplete escape sequence.");
				}

				char escapedCharacter = normalizedPattern[index + 1];

				characterSet = escapedCharacter switch
				{
					'd' => "0123456789",
					'w' =>	"ABCDEFGHIJKLMNOPQRSTUVWXYZ"
								+ "abcdefghijklmnopqrstuvwxyz"
								+ "0123456789_",
					's' => " ",
					_ => escapedCharacter.ToString()
				};

				index += 2;
			}
			else if (IsUnsupportedConstruct(currentCharacter))
			{
				throw new NotSupportedException($"Regex construct '{currentCharacter}' is not supported by the built-in regex generator.");
			}
			else
			{
				characterSet = currentCharacter.ToString();
				++index;
			}

			int repetitionCount = ReadRepetition(
				normalizedPattern,
				ref index
			);

			for (int repetitionIndex = 0; repetitionIndex < repetitionCount && result.Length < maximumLength; ++repetitionIndex)
			{
				int selectedCharacterIndex = Random.Shared.Next(characterSet.Length);
				_ = result.Append(characterSet[selectedCharacterIndex]);
			}
		}

		return result.ToString();
	}

	private static bool IsUnsupportedConstruct(char character)
		=> character
				is '('
				or ')'
				or '.'
				or '+'
				or '*'
				or '|';

	private static int ReadRepetition(string pattern, ref int index)
	{
		if (index >= pattern.Length)
		{
			return 1;
		}

		if (pattern[index] == '?')
		{
			++index;
			return Random.Shared.Next(0, 2);
		}

		if (pattern[index] != '{')
		{
			return 1;
		}

		int endingBraceIndex = pattern.IndexOf('}', index + 1);

		if (endingBraceIndex < 0)
		{
			throw new InvalidOperationException("The regex contains an unclosed repetition block.");
		}

		string   repetitionText  = pattern.Substring(index + 1, endingBraceIndex - index - 1);
		string[] repetitionParts = repetitionText.Split(',', StringSplitOptions.TrimEntries);

		if (repetitionParts.Length is < 1 or > 2)
		{
			throw new InvalidOperationException($"Invalid repetition block '{{{repetitionText}}}'.");
		}

		if (!int.TryParse(repetitionParts[0], out int minimum))
		{
			throw new InvalidOperationException($"Invalid minimum repetition count '{repetitionParts[0]}'.");
		}

		int maximum = minimum;

		if (repetitionParts.Length == 2)
		{
			if (	string.IsNullOrWhiteSpace(repetitionParts[1])
					|| !int.TryParse(repetitionParts[1], out maximum))
			{
				throw new InvalidOperationException($"Invalid maximum repetition count in '{{{repetitionText}}}'.");
			}
		}

		if (	minimum < 0
				|| maximum < minimum
				|| maximum > MAXIMUM_REPEAT_COUNT
		)
		{
			throw new InvalidOperationException(
				$"Repetition '{{{repetitionText}}}' must have a "
					+ $"minimum of zero, a maximum greater than or equal "
					+ $"to the minimum, and a maximum no greater than "
					+ $"{MAXIMUM_REPEAT_COUNT}."
			);
		}

		index = endingBraceIndex + 1;

		return minimum == maximum
								? minimum
								: Random.Shared.Next(minimum, maximum + 1);
	}

	private static string ExpandCharacterClass(string characterClass)
	{
		if (string.IsNullOrEmpty(characterClass))
		{
			throw new InvalidOperationException("An empty character class cannot generate a value.");
		}

		if (characterClass[0] == '^')
		{
			throw new NotSupportedException("Negated character classes are not supported by the built-in regex generator.");
		}

		StringBuilder characters = new();

		for (int index = 0; index < characterClass.Length; ++index)
		{
			if (characterClass[index] == '\\')
			{
				if (index + 1 >= characterClass.Length)
				{
					throw new InvalidOperationException("The character class ends with an incomplete escape sequence.");
				}

				char escapedCharacter = characterClass[index + 1];

				string escapedCharacters = escapedCharacter switch
				{
					'd' => "0123456789",
					'w' => "ABCDEFGHIJKLMNOPQRSTUVWXYZ"
								+ "abcdefghijklmnopqrstuvwxyz"
								+ "0123456789_",
					's' => " ",
					_ => escapedCharacter.ToString()
				};

				_ = characters.Append(escapedCharacters);
				++index;
				continue;
			}

			if (index + 2 < characterClass.Length && characterClass[index + 1] == '-')
			{
				char rangeStart = characterClass[index];
				char rangeEnd   = characterClass[index + 2];

				if (rangeEnd < rangeStart)
				{
					throw new InvalidOperationException($"Invalid character range '{rangeStart}-{rangeEnd}'.");
				}

				for (char currentCharacter = rangeStart; currentCharacter <= rangeEnd; ++currentCharacter)
				{
					_ = characters.Append(currentCharacter);
				}

				index += 2;
				continue;
			}

			_ = characters.Append(characterClass[index]);
		}

		return characters.Length == 0
					? throw new InvalidOperationException("The character class did not contain any usable characters.")
					: characters.ToString();
	}
}