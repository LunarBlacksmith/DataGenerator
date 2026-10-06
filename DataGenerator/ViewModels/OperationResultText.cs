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
public sealed record OperationResultText(string Text, string? Details, bool IsWarning);