using DataGenerator.Interfaces;

namespace DataGenerator.Services;

public sealed class PatternCompletionProvider : IPatternCompletionProvider
{
	private const int  MINIMUM_PREFIX_LENGTH = 2;
	private const char NO_QUOTE              = '\0';
	private const char OPENING_BRACKET       = '(';
	private const char SPACE                 = ' ';

	private static readonly IReadOnlyList<PatternLanguageEntry> ENTRIES =
		[.. PatternLanguageReference.FUNCTIONS, .. PatternLanguageReference.KEYWORDS];

	/// <summary>
	///	Finds pattern-language functions and keywords that complete the word immediately before the caret.
	/// </summary>
	/// <param name="text">
	///	The pattern text being edited.
	/// </param>
	/// <param name="caretIndex">
	///	The zero-based caret position in <paramref name="text"/>.
	/// </param>
	/// <returns>
	///	The matching completions and replacement range, or <see langword="null"/> when the caret is outside a completable
	///	word, inside quotes or no entries match.
	/// </returns>
	/// <exception cref="ArgumentNullException">
	///	Thrown when <paramref name="text"/> is <see langword="null"/>.
	/// </exception>
	public PatternCompletionResult? GetCompletions(string text, int caretIndex)
	{
		ArgumentNullException.ThrowIfNull(text);

		if (caretIndex < 0 || caretIndex > text.Length)
		{
			return null;
		}

		int wordStart = caretIndex;
		int wordEnd   = caretIndex;

		while (wordStart > 0 && IsWordCharacter(text[wordStart - 1]))
		{
			--wordStart;
		}

		while (wordEnd < text.Length && IsWordCharacter(text[wordEnd]))
		{
			++wordEnd;
		}

		string prefix = text[wordStart..caretIndex];

		if (prefix.Length < MINIMUM_PREFIX_LENGTH || char.IsDigit(prefix[0]) || IsInsideQuotes(text, wordStart))
		{
			return null;
		}

		List<PatternLanguageEntry> entries =
		[
			.. ENTRIES.Where(entry => entry.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && !IsAlreadyWritten(text, wordStart, entry))
		];

		return entries.Count == 0 ? null : new PatternCompletionResult(wordStart, wordEnd - wordStart, prefix, entries);
	}

	/// <summary>
	///	Builds the text replacement and final caret position for accepting a pattern completion entry.
	/// </summary>
	/// <param name="text">
	///	The pattern text being edited.
	/// </param>
	/// <param name="completions">
	///	The completion result that owns the replacement range.
	/// </param>
	/// <param name="entry">
	///	The selected function or keyword.
	/// </param>
	/// <returns>
	///	The edit that inserts the completed entry without duplicating an existing bracket or following space.
	/// </returns>
	/// <exception cref="ArgumentNullException">
	///	Thrown when <paramref name="text"/>, <paramref name="completions"/> or <paramref name="entry"/> is
	///	<see langword="null"/>.
	/// </exception>
	public PatternCompletionEdit CreateEdit(string text, PatternCompletionResult completions, PatternLanguageEntry entry)
	{
		ArgumentNullException.ThrowIfNull(text);
		ArgumentNullException.ThrowIfNull(completions);
		ArgumentNullException.ThrowIfNull(entry);

		int    wordEnd    = completions.WordStart + completions.WordLength;
		int    caretIndex = completions.WordStart + entry.Name.Length + 1;
		string insertion;

		if (entry.IsFunction)
		{
			// The caret goes between the brackets, ready for the arguments.
			bool hasBracket = wordEnd < text.Length && text[wordEnd] == OPENING_BRACKET;
			insertion       = hasBracket ? entry.Name : entry.Name + "()";
		}
		else
		{
			bool hasSpace = wordEnd < text.Length && char.IsWhiteSpace(text[wordEnd]);
			insertion     = hasSpace ? entry.Name : entry.Name + SPACE;
		}

		return new PatternCompletionEdit(completions.WordStart, completions.WordLength, insertion, caretIndex);
	}

	/// <summary>
	///	Checks whether a character can be part of a function or keyword word while completions are located.
	/// </summary>
	/// <param name="character">
	///	The character to test.
	/// </param>
	/// <returns>
	///	<see langword="true"/> when the character is a letter, digit or underscore; otherwise <see langword="false"/>.
	/// </returns>
	private static bool IsWordCharacter(char character) => char.IsLetterOrDigit(character) || character == '_';

	/// <summary>
	///	Checks whether <paramref name="position"/> is inside quoted text, where words are plain text rather than functions.
	///	A doubled quote inside quoted text closes and reopens it, so it needs no special handling.
	/// </summary>
	/// <param name="text">
	///	The pattern text being scanned.
	/// </param>
	/// <param name="position">
	///	The position to test.
	/// </param>
	/// <returns>
	///	<see langword="true"/> when the position is inside a single- or double-quoted span; otherwise
	///	<see langword="false"/>.
	/// </returns>
	private static bool IsInsideQuotes(string text, int position)
	{
		char openQuote = NO_QUOTE;

		for (int index = 0; index < position; ++index)
		{
			char current = text[index];

			if (openQuote == NO_QUOTE)
			{
				if (current is '\'' or '"')
				{
					openQuote = current;
				}
			}
			else if (current == openQuote)
			{
				openQuote = NO_QUOTE;
			}
		}

		return openQuote != NO_QUOTE;
	}

	/// <summary>
	///	Checks whether the entry is already written in full at <paramref name="wordStart"/>, so suggesting it would only
	///	add a duplicate.
	/// </summary>
	/// <param name="text">
	///	The pattern text being edited.
	/// </param>
	/// <param name="wordStart">
	///	The start index of the word being completed.
	/// </param>
	/// <param name="entry">
	///	The function or keyword entry to compare with the text.
	/// </param>
	/// <returns>
	///	<see langword="true"/> when the entry already appears at the word start; otherwise <see langword="false"/>.
	/// </returns>
	private static bool IsAlreadyWritten(string text, int wordStart, PatternLanguageEntry entry)
	{
		string written = entry.IsFunction ? entry.Name + OPENING_BRACKET : entry.Name;

		if (!text.AsSpan(wordStart).StartsWith(written, StringComparison.OrdinalIgnoreCase))
		{
			return false;
		}

		int writtenEnd = wordStart + written.Length;
		return entry.IsFunction || writtenEnd >= text.Length || !IsWordCharacter(text[writtenEnd]);
	}
}