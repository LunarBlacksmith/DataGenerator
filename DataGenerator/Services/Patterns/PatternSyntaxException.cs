namespace DataGenerator.Services.Patterns;

/// <summary>
/// Thrown when a pattern expression cannot be parsed. <see cref="Position"/> is the one-based character position.
/// </summary>
public sealed class PatternSyntaxException : FormatException
{
	public PatternSyntaxException(string message, int position)
		: base($"{message} (at position {position})")
	{
		Position = position;
	}

	public int Position { get; }
}