namespace DataGenerator.Services.Patterns;

/// <summary>
///	Thrown when a pattern expression cannot be parsed. <see cref="Position"/> is the one-based character position.
/// </summary>
public sealed class PatternSyntaxException : FormatException
{
	#region PROPERTIES
	public int Position { get; }
	#endregion PROPERTIES

	/// <summary>
	///	Creates a syntax exception with the pattern message and one-based source position appended.
	/// </summary>
	/// <param name="message">
	///	The message describing the syntax problem.
	/// </param>
	/// <param name="position">
	///	The one-based character position where the problem was found.
	/// </param>
	public PatternSyntaxException(string message, int position)
		: base($"{message} (at position {position})")
	{
		Position = position;
	}
}