using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace DataGenerator.Infrastructure;

/// <summary>
/// Adds Explorer-style multi-selection to a <see cref="TreeView"/>: click selects one item, Ctrl+click toggles an item,
/// Shift+click selects a range, right-click selects an item that is not already selected and Ctrl+Space toggles the
/// focused item. The selection itself lives in the view model; this behaviour only reports what the user did through
/// <see cref="TreeSelectionRequest"/> command parameters.
/// </summary>
public static class TreeViewMultiSelect
{
	public static readonly DependencyProperty SELECT_COMMAND_PROPERTY =
		DependencyProperty.RegisterAttached(
			"SelectCommand",
			typeof(ICommand),
			typeof(TreeViewMultiSelect),
			new PropertyMetadata(null, OnSelectCommandChanged)
		);

	public static ICommand? GetSelectCommand(DependencyObject element) => (ICommand?)element.GetValue(SELECT_COMMAND_PROPERTY);
	public static void SetSelectCommand(DependencyObject element, ICommand? value) => element.SetValue(SELECT_COMMAND_PROPERTY, value);

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
	/// Finds the tree item that contains <paramref name="source"/> and whether the click landed on a control inside the item
	/// (button, check box, text box or combo box) that handles the mouse itself.
	/// </summary>
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

	private static DependencyObject? GetParent(DependencyObject element)
		=> element is Visual or Visual3D
			? VisualTreeHelper.GetParent(element)
			: LogicalTreeHelper.GetParent(element);
}