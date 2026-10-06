namespace DataGenerator.Infrastructure;

public enum TreeSelectionMode
{
	/// <summary>
	///	Plain click: select only this item.
	/// </summary>
	Replace = 0,

	/// <summary>
	///	Ctrl+click: add the item to, or remove it from, the selection.
	/// </summary>
	Toggle  = 1,

	/// <summary>
	///	Shift+click: select every visible item between the last clicked item and this one.
	/// </summary>
	Range   = 2,

	/// <summary>
	///	Right-click: keep the selection when the item is part of it, otherwise select only this item.
	/// </summary>
	Context = 3
}