namespace DataGenerator.Infrastructure;

/// <summary>
///	Sent by <see cref="TreeViewMultiSelect"/> to the view model when the user selects a tree item.
/// </summary>
public sealed record TreeSelectionRequest
{
	#region PROPERTIES
	public object            Item { get; init; }
	public TreeSelectionMode Mode { get; init; }
	#endregion PROPERTIES

	/// <summary>
	///	Creates a new <see cref="TreeSelectionRequest"/> from the supplied values.
	/// </summary>
	/// <param name="item">
	///	The value of <see cref="Item"/>.
	/// </param>
	/// <param name="mode">
	///	The value of <see cref="Mode"/>.
	/// </param>
	public TreeSelectionRequest(object item, TreeSelectionMode mode)
	{
		Item = item;
		Mode = mode;
	}
}