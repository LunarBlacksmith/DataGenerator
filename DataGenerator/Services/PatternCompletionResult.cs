namespace DataGenerator.Services;

/// <summary>
///	The word being typed in a pattern and the functions and keywords that complete it.
///	<paramref name="Prefix"/> is the part of the word before the caret.
/// </summary>
public sealed record PatternCompletionResult(
	int                                 WordStart,
	int                                 WordLength,
	string                              Prefix,
	IReadOnlyList<PatternLanguageEntry> Entries
);