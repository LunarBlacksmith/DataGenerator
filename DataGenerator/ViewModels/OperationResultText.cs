namespace DataGenerator.ViewModels;

/// <summary>
///	A short message about an action the user took, with the details shown when hovering over it.
/// </summary>
/// <param name="Text">
///	One line for the user.
/// </param>
/// <param name="Details">
///	More lines, e.g. why columns were skipped; <see langword="null"/> when there is nothing more to say.
/// </param>
/// <param name="IsWarning">
///	Whether part of the action could not be done.
/// </param>
public sealed record OperationResultText
{
	#region PROPERTIES
	#region PUBLIC
	public string  Text      { get; init; }
	public string? Details   { get; init; }
	public bool    IsWarning { get; init; }
	#endregion PUBLIC
	#endregion PROPERTIES

	#region CONSTRUCTORS
	#region PUBLIC
	/// <summary>
	///	Creates a new <see cref="OperationResultText"/> from the supplied values.
	/// </summary>
	/// <param name="text">
	///	The value of <see cref="Text"/>.
	/// </param>
	/// <param name="details">
	///	The value of <see cref="Details"/>.
	/// </param>
	/// <param name="isWarning">
	///	The value of <see cref="IsWarning"/>.
	/// </param>
	public OperationResultText(string text, string? details, bool isWarning)
	{
		Text      = text;
		Details   = details;
		IsWarning = isWarning;
	}
	#endregion PUBLIC
	#endregion CONSTRUCTORS
}