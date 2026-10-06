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
	public PatternToken(PatternTokenKind kind, string text, int position)
	{
		Kind     = kind;
		Text     = text;
		Position = position;
	}

	public PatternTokenKind Kind     { get; }
	public string           Text     { get; }
	public int              Position { get; }

	public bool IsKeyword(string keyword)
		=> Kind == PatternTokenKind.Word && string.Equals(Text, keyword, StringComparison.OrdinalIgnoreCase);
}