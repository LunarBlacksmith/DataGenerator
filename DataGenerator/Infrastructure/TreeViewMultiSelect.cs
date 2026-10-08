using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace DataGenerator.Infrastructure;

/// <summary>
///	Adds Explorer-style multi-selection to a <see cref="TreeView"/>: click selects one item, Ctrl+click toggles an item,
///	Shift+click selects a range, right-click selects an item that is not already selected and Ctrl+Space toggles the
///	focused item. The selection itself lives in the view model; this behaviour only reports what the user did through
///	<see cref="TreeSelectionRequest"/> command parameters.
/// </summary>
public static class TreeViewMultiSelect
{
	#region FIELDS
	public static readonly DependencyProperty SELECT_COMMAND_PROPERTY;
	#endregion FIELDS

	#region CONSTRUCTOR
	/// <summary>
	///	Sets the default values of the static fields and properties of <see cref="TreeViewMultiSelect"/>.
	/// </summary>
	static TreeViewMultiSelect()
	{
		SELECT_COMMAND_PROPERTY =
			DependencyProperty.RegisterAttached(
				"SelectCommand",
				typeof(ICommand),
				typeof(TreeViewMultiSelect),
				new PropertyMetadata(null, OnSelectCommandChanged)
			);
	}
	#endregion CONSTRUCTOR

	#region METHODS
	#region PUBLIC
	/// <summary>
	///	Gets the command that receives tree selection requests.
	/// </summary>
	/// <param name="element">
	///	The element that stores the attached command.
	/// </param>
	/// <returns>
	///	The selection command, or <see langword="null"/> when the behaviour is disabled.
	/// </returns>
	public static ICommand? GetSelectCommand(DependencyObject element) => (ICommand?)element.GetValue(SELECT_COMMAND_PROPERTY);

	/// <summary>
	///	Sets the command that receives tree selection requests.
	/// </summary>
	/// <param name="element">
	///	The element that stores the attached command.
	/// </param>
	/// <param name="value">
	///	The command to execute for selection gestures, or <see langword="null"/> to disable the behaviour.
	/// </param>
	public static void SetSelectCommand(DependencyObject element, ICommand? value) => element.SetValue(SELECT_COMMAND_PROPERTY, value);
	#endregion PUBLIC

	#region PRIVATE
	/// <summary>
	///	Adds or removes the mouse and keyboard handlers when the selection command changes.
	/// </summary>
	/// <param name="sender">
	///	The element whose attached command changed.
	/// </param>
	/// <param name="e">
	///	The old and new command values.
	/// </param>
	private static void OnSelectCommandChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
	{
		if (sender is not TreeView treeView)
		{
			return;
		}

		treeView.PreviewMouseLeftButtonDown  -= OnPreviewMouseButtonDown;
		treeView.PreviewMouseRightButtonDown -= OnPreviewMouseButtonDown;
		treeView.SelectedItemChanged         -= OnSelectedItemChanged;
		treeView.PreviewKeyDown              -= OnPreviewKeyDown;

		if (e.NewValue is not null)
		{
			treeView.PreviewMouseLeftButtonDown  += OnPreviewMouseButtonDown;
			treeView.PreviewMouseRightButtonDown += OnPreviewMouseButtonDown;
			treeView.SelectedItemChanged         += OnSelectedItemChanged;
			treeView.PreviewKeyDown              += OnPreviewKeyDown;
		}
	}

	/// <summary>
	///	Translates left and right mouse clicks on tree items into selection requests.
	/// </summary>
	/// <param name="sender">
	///	The tree view that received the mouse event.
	/// </param>
	/// <param name="e">
	///	The mouse button event raised by WPF.
	/// </param>
	private static void OnPreviewMouseButtonDown(object sender, MouseButtonEventArgs e)
	{
		if (e.OriginalSource is not DependencyObject source)
		{
			return;
		}

		TreeViewItem? item = FindItemContainer(source, out bool isInsideInteractiveElement);

		if (item is null || isInsideInteractiveElement)
		{
			return;
		}

		ModifierKeys      modifiers = Keyboard.Modifiers;
		TreeSelectionMode mode      =
			e.ChangedButton == MouseButton.Right
				? TreeSelectionMode.Context
				: modifiers.HasFlag(ModifierKeys.Shift)
					? TreeSelectionMode.Range
					: modifiers.HasFlag(ModifierKeys.Control)
						? TreeSelectionMode.Toggle
						: TreeSelectionMode.Replace;

		Execute((TreeView)sender, item.DataContext, mode);
	}

	// Keyboard navigation moves the native selection; mouse clicks are already handled above.
	/// <summary>
	///	Translates keyboard-driven native selection changes into view model selection requests.
	/// </summary>
	/// <param name="sender">
	///	The tree view whose selected item changed.
	/// </param>
	/// <param name="e">
	///	The old and new selected items.
	/// </param>
	private static void OnSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
	{
		if (e.NewValue is null || Mouse.LeftButton == MouseButtonState.Pressed)
		{
			return;
		}

		ModifierKeys modifiers = Keyboard.Modifiers;

		if (modifiers.HasFlag(ModifierKeys.Control))
		{
			return;
		}

		Execute(
			(TreeView)sender,
			e.NewValue,
			modifiers.HasFlag(ModifierKeys.Shift) ? TreeSelectionMode.Range : TreeSelectionMode.Replace
		);
	}

	/// <summary>
	///	Handles Ctrl+Space to toggle the currently selected tree item.
	/// </summary>
	/// <param name="sender">
	///	The tree view that received the key event.
	/// </param>
	/// <param name="e">
	///	The preview key event raised by WPF.
	/// </param>
	private static void OnPreviewKeyDown(object sender, KeyEventArgs e)
	{
		TreeView treeView = (TreeView)sender;

		if (	e.Key != Key.Space
				|| !Keyboard.Modifiers.HasFlag(ModifierKeys.Control)
				|| e.OriginalSource is not TreeViewItem
				|| treeView.SelectedItem is null
		)
		{
			return;
		}

		Execute(treeView, treeView.SelectedItem, TreeSelectionMode.Toggle);
		e.Handled = true;
	}

	/// <summary>
	///	Builds and executes a selection request for a tree item when the command can run.
	/// </summary>
	/// <param name="treeView">
	///	The tree view that stores the selection command.
	/// </param>
	/// <param name="item">
	///	The item to select, toggle or use as the range end.
	/// </param>
	/// <param name="mode">
	///	The kind of selection gesture requested by the user.
	/// </param>
	private static void Execute(TreeView treeView, object? item, TreeSelectionMode mode)
	{
		ICommand? command = GetSelectCommand(treeView);

		if (item is null || command is null)
		{
			return;
		}

		TreeSelectionRequest request = new(item, mode);

		if (command.CanExecute(request))
		{
			command.Execute(request);
		}
	}

	/// <summary>
	///	Finds the tree item that contains <paramref name="source"/> and whether the click landed on a control inside the item
	///	(button, check box, text box or combo box) that handles the mouse itself.
	/// </summary>
	/// <param name="source">
	///	The original dependency object under the mouse.
	/// </param>
	/// <param name="isInsideInteractiveElement">
	///	Whether the source is inside a control that should handle the mouse itself.
	/// </param>
	/// <returns>
	///	The containing tree view item, or <see langword="null"/> when none is found.
	/// </returns>
	private static TreeViewItem? FindItemContainer(DependencyObject source, out bool isInsideInteractiveElement)
	{
		isInsideInteractiveElement = false;

		for (DependencyObject? current = source; current is not null; current = GetParent(current))
		{
			switch (current)
			{
				case TreeViewItem item:
				{
					return item;
				}

				case ButtonBase:
				case TextBoxBase:
				case ComboBox:
				case ScrollBar:
				{
					isInsideInteractiveElement = true;
					break;
				}
			}
		}

		return null;
	}

	/// <summary>
	///	Gets the visual or logical parent of an element.
	/// </summary>
	/// <param name="element">
	///	The element whose parent is needed.
	/// </param>
	/// <returns>
	///	The parent dependency object, or <see langword="null"/> when the element has no parent.
	/// </returns>
	private static DependencyObject? GetParent(DependencyObject element)
		=>
			element is Visual or Visual3D
				? VisualTreeHelper.GetParent(element)
				: LogicalTreeHelper.GetParent(element);
	#endregion PRIVATE
	#endregion METHODS
}