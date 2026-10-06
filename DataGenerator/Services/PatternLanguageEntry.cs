namespace DataGenerator.Services;

/// <summary>
///	A function or keyword of the pattern language, with its signature, what it does and an example.
/// </summary>
public sealed record PatternLanguageEntry
{
	#region PROPERTIES
	#region PUBLIC
	public string                   Name        { get; init; }
	public string                   Signature   { get; init; }
	public string                   Description { get; init; }
	public string                   Example     { get; init; }
	public PatternLanguageEntryKind Kind        { get; init; }
	public bool IsFunction => Kind == PatternLanguageEntryKind.Function;
	#endregion PUBLIC
	#endregion PROPERTIES

	#region CONSTRUCTORS
	#region PUBLIC
	/// <summary>
	///	Creates a new <see cref="PatternLanguageEntry"/> from the supplied values.
	/// </summary>
	/// <param name="name">
	///	The value of <see cref="Name"/>.
	/// </param>
	/// <param name="signature">
	///	The value of <see cref="Signature"/>.
	/// </param>
	/// <param name="description">
	///	The value of <see cref="Description"/>.
	/// </param>
	/// <param name="example">
	///	The value of <see cref="Example"/>.
	/// </param>
	/// <param name="kind">
	///	The value of <see cref="Kind"/>.
	/// </param>
	public PatternLanguageEntry(
		string                   name,
		string                   signature,
		string                   description,
		string                   example,
		PatternLanguageEntryKind kind
	)
	{
		Name        = name;
		Signature   = signature;
		Description = description;
		Example     = example;
		Kind        = kind;
	}
	#endregion PUBLIC
	#endregion CONSTRUCTORS
}