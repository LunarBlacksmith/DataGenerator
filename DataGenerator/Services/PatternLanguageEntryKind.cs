namespace DataGenerator.Services;

/// <summary>
///	Whether an entry of the pattern language is a function such as SEQ(...) or a keyword such as FOLLOWED BY.
/// </summary>
public enum PatternLanguageEntryKind
{
	Function,
	Keyword
}