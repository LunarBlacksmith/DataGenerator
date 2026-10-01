namespace DataGenerator.Infrastructure;

/// <summary>
/// Sent by <see cref="TreeViewMultiSelect"/> to the view model when the user selects a tree item.
/// </summary>
public sealed record TreeSelectionRequest(object Item, TreeSelectionMode Mode);