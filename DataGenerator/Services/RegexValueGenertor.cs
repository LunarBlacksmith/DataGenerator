using System.Text;
using DataGenerator.Interfaces;

namespace DataGenerator.Services;

public sealed class RegexValueGenerator : IRegexValueGenerator
{
	#region FIELDS
	#region PRIVATE
	private const int MAXIMUM_REPEAT_COUNT = 1000;
	#endregion PRIVATE
	#endregion FIELDS

	#region CONSTRUCTORS
	#region PUBLIC
	/// <summary>
	///	Creates a new <see cref="RegexValueGenerator"/>.
	/// </summary>
	public RegexValueGenerator()
	{
	}
	#endregion PUBLIC
	#endregion CONSTRUCTORS

	#region METHODS
	#region PUBLIC
	/// <summary>
	///	Generates a random value from the supported subset of a regular expression, stopping at the requested length.
	/// </summary>
	/// <param name="pattern">
	///	The regular expression pattern to generate from.
	/// </param>
	/// <param name="maximumLength">
	///	The maximum number of characters to return.
	/// </param>
	/// <returns>
	///	A generated value that follows the supported parts of <paramref name="pattern"/> and is no longer than
	///	<paramref name="maximumLength"/>.
	/// </returns>
	/// <exception cref="ArgumentException">
	///	Thrown when <paramref name="pattern"/> is empty or whitespace.
	/// </exception>
	/// <exception cref="ArgumentOutOfRangeException">
	///	Thrown when <paramref name="maximumLength"/> is less than one.
	/// </exception>
	/// <exception cref="InvalidOperationException">
	///	Thrown when the pattern contains malformed supported syntax.
	/// </exception>
	/// <exception cref="NotSupportedException">
	///	Thrown when the pattern contains a regular-expression construct that this generator does not support.
	/// </exception>
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
	#endregion PUBLIC

	#region PRIVATE
	/// <summary>
	///	Checks whether a pattern character starts a regular-expression construct that the generator cannot expand.
	/// </summary>
	/// <param name="character">
	///	The pattern character to test.
	/// </param>
	/// <returns>
	///	<see langword="true"/> when the character is unsupported; otherwise <see langword="false"/>.
	/// </returns>
	private static bool IsUnsupportedConstruct(char character)
		=> character
				is '('
				or ')'
				or '.'
				or '+'
				or '*'
				or '|';

	/// <summary>
	///	Reads an optional repetition suffix after an atom and chooses the number of generated characters for it.
	/// </summary>
	/// <param name="pattern">
	///	The normalised regular-expression pattern being read.
	/// </param>
	/// <param name="index">
	///	The current index after the atom; advanced past the repetition suffix when one is present.
	/// </param>
	/// <returns>
	///	The exact or randomly selected repetition count for the preceding atom.
	/// </returns>
	/// <exception cref="InvalidOperationException">
	///	Thrown when a repetition block is malformed or outside the supported range.
	/// </exception>
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

		return
			minimum == maximum
				? minimum
				: Random.Shared.Next(minimum, maximum + 1);
	}

	/// <summary>
	///	Expands a character class into the concrete characters from which a generated value may choose.
	/// </summary>
	/// <param name="characterClass">
	///	The contents of a bracketed regular-expression character class.
	/// </param>
	/// <returns>
	///	The characters represented by literals, escapes and ranges in the class.
	/// </returns>
	/// <exception cref="InvalidOperationException">
	///	Thrown when the class is empty, incomplete or contains an invalid range.
	/// </exception>
	/// <exception cref="NotSupportedException">
	///	Thrown when the class is negated.
	/// </exception>
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

		return
			characters.Length == 0
				? throw new InvalidOperationException("The character class did not contain any usable characters.")
				: characters.ToString();
	}
	#endregion PRIVATE
	#endregion METHODS
}