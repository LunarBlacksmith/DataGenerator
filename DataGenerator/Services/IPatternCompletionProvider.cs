namespace DataGenerator.Services;

/// <summary>
/// Suggests the functions and keywords of the pattern language that complete the word being typed.
/// </summary>
public interface IPatternCompletionProvider
{
	/// <summary>
	/// The suggestions for the word at <paramref name="caretIndex"/>, or <see langword="null"/> when nothing should be
	/// suggested (no word, a word inside quoted text, or no matches).
	/// </summary>
	PatternCompletionResult? GetCompletions(string text, int caretIndex);

	/// <summary>
	/// The edit that replaces the word of <paramref name="completions"/> with <paramref name="entry"/>: a function gets
	/// its brackets with the caret between them, a keyword gets a space after it.
	/// </summary>
	PatternCompletionEdit CreateEdit(string text, PatternCompletionResult completions, PatternLanguageEntry entry);
}