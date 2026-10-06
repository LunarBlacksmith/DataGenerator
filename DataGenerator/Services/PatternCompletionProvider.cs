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

	private static bool IsWordCharacter(char character) => char.IsLetterOrDigit(character) || character == '_';

	/// <summary>
	/// Whether <paramref name="position"/> is inside quoted text, where words are plain text rather than functions.
	/// A doubled quote inside quoted text closes and reopens it, so it needs no special handling.
	/// </summary>
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
	/// Whether the entry is already written in full at <paramref name="wordStart"/>, e.g. TODAY( or FOLLOWED BY,
	/// in which case suggesting it would only add a duplicate.
	/// </summary>
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