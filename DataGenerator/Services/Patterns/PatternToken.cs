namespace DataGenerator.Services.Patterns;

internal enum PatternTokenKind
{
	Word             = 0,
	Number           = 1,
	Text             = 2,
	LeftParenthesis  = 3,
	RightParenthesis = 4,
	Comma            = 5,
	Plus             = 6,
	Pipe             = 7,
	Minus            = 8,
	Range            = 9,
	Assign           = 10,
	End              = 11
}

internal sealed class PatternToken
{
	/// <summary>
	///	Creates a token with its kind, source text and one-based position in the pattern.
	/// </summary>
	/// <param name="kind">
	///	The syntactic kind of token.
	/// </param>
	/// <param name="text">
	///	The token text, or the unescaped text for quoted literals.
	/// </param>
	/// <param name="position">
	///	The one-based character position where the token starts.
	/// </param>
	public PatternToken(PatternTokenKind kind, string text, int position)
	{
		Kind     = kind;
		Text     = text;
		Position = position;
	}

	public PatternTokenKind Kind     { get; }
	public string           Text     { get; }
	public int              Position { get; }

	/// <summary>
	///	Checks whether this token is a word matching a pattern keyword, ignoring case.
	/// </summary>
	/// <param name="keyword">
	///	The keyword text to compare with.
	/// </param>
	/// <returns>
	///	<see langword="true"/> when this token is the requested keyword; otherwise <see langword="false"/>.
	/// </returns>
	public bool IsKeyword(string keyword)
		=> Kind == PatternTokenKind.Word && string.Equals(Text, keyword, StringComparison.OrdinalIgnoreCase);
}