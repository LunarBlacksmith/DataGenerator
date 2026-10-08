namespace DataGenerator.Services;

/// <summary>
///	The word being typed in a pattern and the functions and keywords that complete it.
///	<see cref="Prefix"/> is the part of the word before the caret.
/// </summary>
public sealed record PatternCompletionResult
{
	#region PROPERTIES
	public int                                 WordStart  { get; init; }
	public int                                 WordLength { get; init; }
	public string                              Prefix     { get; init; }
	public IReadOnlyList<PatternLanguageEntry> Entries    { get; init; }
	#endregion PROPERTIES

	/// <summary>
	///	Creates a new <see cref="PatternCompletionResult"/> from the supplied values.
	/// </summary>
	/// <param name="wordStart">
	///	The value of <see cref="WordStart"/>.
	/// </param>
	/// <param name="wordLength">
	///	The value of <see cref="WordLength"/>.
	/// </param>
	/// <param name="prefix">
	///	The value of <see cref="Prefix"/>.
	/// </param>
	/// <param name="entries">
	///	The value of <see cref="Entries"/>.
	/// </param>
	public PatternCompletionResult(
		int                                 wordStart,
		int                                 wordLength,
		string                              prefix,
		IReadOnlyList<PatternLanguageEntry> entries
	)
	{
		WordStart  = wordStart;
		WordLength = wordLength;
		Prefix     = prefix;
		Entries    = entries;
	}
}