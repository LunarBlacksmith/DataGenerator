using DataGenerator.Services;

namespace DataGenerator.Interfaces;

/// <summary>
///	Suggests the functions and keywords of the pattern language that complete the word being typed.
/// </summary>
public interface IPatternCompletionProvider
{
	/// <summary>
	///	The suggestions for the word at <paramref name="caretIndex"/>, or <see langword="null"/> when nothing should be
	///	suggested (no word, a word inside quoted text, or no matches).
	/// </summary>
	/// <param name="text">
	///	The full pattern text being edited.
	/// </param>
	/// <param name="caretIndex">
	///	The zero-based caret position within <paramref name="text"/>.
	/// </param>
	/// <returns>
	///	The matching completions and replacement span, or <see langword="null"/> when the caret is outside the text, the
	///	prefix is too short or no entry matches.
	/// </returns>
	/// <exception cref="ArgumentNullException">
	///	Thrown when <paramref name="text"/> is <see langword="null"/>.
	/// </exception>
	PatternCompletionResult? GetCompletions(string text, int caretIndex);

	/// <summary>
	///	The edit that replaces the word of <paramref name="completions"/> with <paramref name="entry"/>: a function gets
	///	its brackets with the caret between them, a keyword gets a space after it.
	/// </summary>
	/// <param name="text">
	///	The full pattern text being edited.
	/// </param>
	/// <param name="completions">
	///	The completion span previously returned by <see cref="GetCompletions"/>.
	/// </param>
	/// <param name="entry">
	///	The selected function or keyword.
	/// </param>
	/// <returns>
	///	The replacement text, replacement span and new caret position to apply to the editor.
	/// </returns>
	/// <exception cref="ArgumentNullException">
	///	Thrown when <paramref name="text"/>, <paramref name="completions"/> or <paramref name="entry"/> is
	///	<see langword="null"/>.
	/// </exception>
	PatternCompletionEdit CreateEdit(string text, PatternCompletionResult completions, PatternLanguageEntry entry);
}