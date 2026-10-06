namespace DataGenerator.Services;

/// <summary>
///	A function or keyword of the pattern language, with its signature, what it does and an example.
/// </summary>
public sealed record PatternLanguageEntry(
	string                   Name,
	string                   Signature,
	string                   Description,
	string                   Example,
	PatternLanguageEntryKind Kind
)
{
	public bool IsFunction => Kind == PatternLanguageEntryKind.Function;
}