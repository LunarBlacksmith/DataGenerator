namespace DataGenerator.Services;

/// <summary>
///	Replaces <see cref="Length"/> characters from <see cref="Start"/> with <see cref="Text"/>, then moves
///	the caret to <see cref="CaretIndex"/>.
/// </summary>
public sealed record PatternCompletionEdit
{
	#region PROPERTIES
	#region PUBLIC
	public int    Start      { get; init; }
	public int    Length     { get; init; }
	public string Text       { get; init; }
	public int    CaretIndex { get; init; }
	#endregion PUBLIC
	#endregion PROPERTIES

	#region CONSTRUCTORS
	#region PUBLIC
	/// <summary>
	///	Creates a new <see cref="PatternCompletionEdit"/> from the supplied values.
	/// </summary>
	/// <param name="start">
	///	The value of <see cref="Start"/>.
	/// </param>
	/// <param name="length">
	///	The value of <see cref="Length"/>.
	/// </param>
	/// <param name="text">
	///	The value of <see cref="Text"/>.
	/// </param>
	/// <param name="caretIndex">
	///	The value of <see cref="CaretIndex"/>.
	/// </param>
	public PatternCompletionEdit(int start, int length, string text, int caretIndex)
	{
		Start      = start;
		Length     = length;
		Text       = text;
		CaretIndex = caretIndex;
	}
	#endregion PUBLIC
	#endregion CONSTRUCTORS
}