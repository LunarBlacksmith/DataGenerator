namespace DataGenerator.Services;

/// <summary>
/// Replaces <paramref name="Length"/> characters from <paramref name="Start"/> with <paramref name="Text"/>, then moves
/// the caret to <paramref name="CaretIndex"/>.
/// </summary>
public sealed record PatternCompletionEdit(int Start, int Length, string Text, int CaretIndex);