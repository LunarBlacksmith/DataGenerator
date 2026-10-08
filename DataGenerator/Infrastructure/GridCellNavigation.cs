using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace DataGenerator.Infrastructure;

/// <summary>
///	Moves the keyboard focus between the text boxes and drop-downs in the cells of a <see cref="DataGrid"/> with the
///	arrow keys, like a spreadsheet: ← and → go to the box to the left or right in the same row (from a text box only
///	when the caret is at its start or end, or all of its text is selected), ↑ and ↓ go to the box of the same column
///	in the row above or below. Rows without a box in that column are skipped. Alt+↓ or F4 still opens a drop-down.
/// </summary>
public static class GridCellNavigation
{
	#region FIELDS
	public static readonly DependencyProperty IS_ENABLED_PROPERTY;
	#endregion FIELDS

	#region CONSTRUCTOR
	/// <summary>
	///	Sets the default values of the static fields and properties of <see cref="GridCellNavigation"/>.
	/// </summary>
	static GridCellNavigation()
	{
		IS_ENABLED_PROPERTY =
			DependencyProperty.RegisterAttached(
				"IsEnabled",
				typeof(bool),
				typeof(GridCellNavigation),
				new FrameworkPropertyMetadata(false, OnIsEnabledChanged)
			);
	}
	#endregion CONSTRUCTOR

	#region METHODS
	#region PUBLIC
	/// <summary>
	///	Gets whether arrow-key cell navigation is enabled on a data grid.
	/// </summary>
	/// <param name="element">
	///	The element that stores the attached setting.
	/// </param>
	/// <returns>
	///	<see langword="true"/> when the behaviour is enabled; otherwise <see langword="false"/>.
	/// </returns>
	public static bool GetIsEnabled(DependencyObject element) => (bool)element.GetValue(IS_ENABLED_PROPERTY);

	/// <summary>
	///	Sets whether arrow-key cell navigation is enabled on a data grid.
	/// </summary>
	/// <param name="element">
	///	The element that stores the attached setting.
	/// </param>
	/// <param name="value">
	///	Whether the behaviour should handle the data grid's preview key events.
	/// </param>
	public static void SetIsEnabled(DependencyObject element, bool value) => element.SetValue(IS_ENABLED_PROPERTY, value);
	#endregion PUBLIC

	#region PRIVATE
	/// <summary>
	///	Adds or removes the preview key handler when the attached setting changes.
	/// </summary>
	/// <param name="element">
	///	The element whose attached setting changed.
	/// </param>
	/// <param name="e">
	///	The old and new enabled values.
	/// </param>
	private static void OnIsEnabledChanged(DependencyObject element, DependencyPropertyChangedEventArgs e)
	{
		if (element is not DataGrid dataGrid)
		{
			return;
		}

		dataGrid.PreviewKeyDown -= OnPreviewKeyDown;

		if ((bool)e.NewValue)
		{
			dataGrid.PreviewKeyDown += OnPreviewKeyDown;
		}
	}

	/// <summary>
	///	Handles arrow keys from editable cell controls and moves focus to the next matching editor.
	/// </summary>
	/// <param name="sender">
	///	The data grid that raised the key event.
	/// </param>
	/// <param name="e">
	///	The preview key event to handle.
	/// </param>
	private static void OnPreviewKeyDown(object sender, KeyEventArgs e)
	{
		if (	e.Handled
				|| sender is not DataGrid dataGrid
				|| Keyboard.Modifiers != ModifierKeys.None
				|| e.Key is not (Key.Left or Key.Right or Key.Up or Key.Down)
				|| e.OriginalSource is not Control source
				|| !IsEditor(source)
				|| FindVisualAncestor<DataGridCell>(source) is not DataGridCell cell
				|| FindVisualAncestor<DataGridRow>(cell) is not DataGridRow row
				|| !ReferenceEquals(ItemsControl.ItemsControlFromItemContainer(row), dataGrid)
				|| !ShouldNavigate(source, e.Key)
		)
		{
			return;
		}

		Control? target = e.Key switch
		{
			Key.Left  => FindBesideInRow(row, source, -1),
			Key.Right => FindBesideInRow(row, source, 1),
			Key.Up    => FindInNeighbouringRow(dataGrid, row, cell, source, -1),
			_         => FindInNeighbouringRow(dataGrid, row, cell, source, 1)
		};

		// Without a box in that direction the focus stays where it is, rather than moving to a cell without an editor.
		if (target is not null)
		{
			MoveFocus(target);
		}

		e.Handled = true;
	}

	/// <summary>
	///	Checks whether a control is one of the cell editors that can participate in arrow navigation.
	/// </summary>
	/// <param name="control">
	///	The control to inspect.
	/// </param>
	/// <returns>
	///	<see langword="true"/> when the control is an editable single-line text box or closed non-editable combo box;
	///	otherwise <see langword="false"/>.
	/// </returns>
	private static bool IsEditor(Control control)
		=> control switch
		{
			TextBox textBox   => !textBox.IsReadOnly && !textBox.AcceptsReturn,
			ComboBox comboBox => !comboBox.IsEditable && !comboBox.IsDropDownOpen,
			_                 => false
		};

	/// <summary>
	///	Checks whether an editor can currently receive keyboard focus.
	/// </summary>
	/// <param name="control">
	///	The editor to inspect.
	/// </param>
	/// <returns>
	///	<see langword="true"/> when the editor is visible, enabled, focusable and supported; otherwise
	///	<see langword="false"/>.
	/// </returns>
	private static bool IsAvailable(Control control)
		=> control.IsVisible && control.IsEnabled && control.Focusable && IsEditor(control);

	/// <summary>
	///	In a text box ← and → move the caret until it reaches the start or end of the text, and ↑ and ↓ leave the
	///	suggestion list of a pattern alone.
	/// </summary>
	/// <param name="source">
	///	The focused cell editor that received the key.
	/// </param>
	/// <param name="key">
	///	The arrow key being handled.
	/// </param>
	/// <returns>
	///	<see langword="true"/> when focus should move to another editor; otherwise <see langword="false"/>.
	/// </returns>
	private static bool ShouldNavigate(Control source, Key key)
	{
		if (source is not TextBox textBox)
		{
			return true;
		}

		if (PatternAutoComplete.IsSuggesting(textBox))
		{
			return false;
		}

		bool isAllSelected = textBox.SelectionLength > 0 && textBox.SelectionLength == textBox.Text.Length;

		return key switch
		{
			Key.Left  => isAllSelected || (textBox.SelectionLength == 0 && textBox.CaretIndex == 0),
			Key.Right => isAllSelected || (textBox.SelectionLength == 0 && textBox.CaretIndex == textBox.Text.Length),
			_         => true
		};
	}

	/// <summary>
	///	Finds the available editor immediately to the left or right of the source editor in the same row.
	/// </summary>
	/// <param name="row">
	///	The row that contains the source editor.
	/// </param>
	/// <param name="source">
	///	The currently focused editor.
	/// </param>
	/// <param name="direction">
	///	-1 to search left, or 1 to search right.
	/// </param>
	/// <returns>
	///	The neighbouring editor, or <see langword="null"/> when there is none in that direction.
	/// </returns>
	private static Control? FindBesideInRow(DataGridRow row, Control source, int direction)
	{
		List<Control> editors = GetEditors(row);
		int           index   = editors.IndexOf(source);
		int           target  = index + direction;

		return index >= 0 && target >= 0 && target < editors.Count ? editors[target] : null;
	}

	/// <summary>
	///	The box at the same position of the same column in the nearest row above or below that has one.
	/// </summary>
	/// <param name="dataGrid">
	///	The grid that owns the rows.
	/// </param>
	/// <param name="row">
	///	The current row.
	/// </param>
	/// <param name="cell">
	///	The current cell, used to keep navigation in the same column.
	/// </param>
	/// <param name="source">
	///	The currently focused editor.
	/// </param>
	/// <param name="direction">
	///	-1 to search upwards, or 1 to search downwards.
	/// </param>
	/// <returns>
	///	The editor in the nearest row in that direction, or <see langword="null"/> when no matching row contains one.
	/// </returns>
	private static Control? FindInNeighbouringRow(DataGrid dataGrid, DataGridRow row, DataGridCell cell, Control source, int direction)
	{
		List<Control> cellEditors = GetEditors(cell);
		int           slot        = Math.Max(0, cellEditors.IndexOf(source));
		int           rowIndex    = dataGrid.ItemContainerGenerator.IndexFromContainer(row);

		if (rowIndex < 0 || cell.Column is not DataGridColumn column)
		{
			return null;
		}

		for (int index = rowIndex + direction; index >= 0 && index < dataGrid.Items.Count; index += direction)
		{
			if (	GetRow(dataGrid, index) is not DataGridRow targetRow
					|| column.GetCellContent(targetRow) is not FrameworkElement content
					|| FindVisualAncestor<DataGridCell>(content) is not DataGridCell targetCell
			)
			{
				continue;
			}

			List<Control> editors = GetEditors(targetCell);

			if (editors.Count > 0)
			{
				return editors[Math.Min(slot, editors.Count - 1)];
			}
		}

		return null;
	}

	/// <summary>
	///	The row container of an item, scrolling it into view first when the grid has not created it yet.
	/// </summary>
	/// <param name="dataGrid">
	///	The grid that owns the item.
	/// </param>
	/// <param name="index">
	///	The zero-based item index whose row is needed.
	/// </param>
	/// <returns>
	///	The realised row container, or <see langword="null"/> when WPF still cannot provide one.
	/// </returns>
	private static DataGridRow? GetRow(DataGrid dataGrid, int index)
	{
		if (dataGrid.ItemContainerGenerator.ContainerFromIndex(index) is DataGridRow row)
		{
			return row;
		}

		dataGrid.ScrollIntoView(dataGrid.Items[index]);
		dataGrid.UpdateLayout();
		return dataGrid.ItemContainerGenerator.ContainerFromIndex(index) as DataGridRow;
	}

	/// <summary>
	///	The available boxes inside an element in visual order: column by column, then left to right within a cell.
	/// </summary>
	/// <param name="root">
	///	The row, cell or other visual root to search.
	/// </param>
	/// <returns>
	///	The available editors found under <paramref name="root"/>, in navigation order.
	/// </returns>
	private static List<Control> GetEditors(DependencyObject root)
	{
		List<Control> editors = [];

		if (root is DataGridRow row)
		{
			DataGridCellsPresenter? presenter = FindVisualDescendant<DataGridCellsPresenter>(row);

			if (presenter is null)
			{
				return editors;
			}

			List<DataGridCell> cells = [];

			CollectCells(presenter, cells);

			foreach (DataGridCell cell in
				cells
					.OrderBy(cell => cell.Column?.DisplayIndex ?? int.MaxValue)
			)
			{
				editors.AddRange(GetEditors(cell));
			}

			return editors;
		}

		CollectEditors(root, editors);
		return [.. editors.Where(IsAvailable)];
	}

	/// <summary>
	///	Collects all data grid cells below a visual parent.
	/// </summary>
	/// <param name="parent">
	///	The visual parent to search.
	/// </param>
	/// <param name="found">
	///	The list that receives the cells in visual tree order.
	/// </param>
	private static void CollectCells(DependencyObject parent, List<DataGridCell> found)
	{
		int childCount = VisualTreeHelper.GetChildrenCount(parent);

		for (int index = 0; index < childCount; ++index)
		{
			DependencyObject child = VisualTreeHelper.GetChild(parent, index);

			if (child is DataGridCell cell)
			{
				found.Add(cell);
				continue;
			}

			CollectCells(child, found);
		}
	}

	/// <summary>
	///	Collects text boxes and combo boxes below a visual parent without descending into their templates.
	/// </summary>
	/// <param name="parent">
	///	The visual parent to search.
	/// </param>
	/// <param name="found">
	///	The list that receives candidate editors in visual tree order.
	/// </param>
	private static void CollectEditors(DependencyObject parent, List<Control> found)
	{
		int childCount = VisualTreeHelper.GetChildrenCount(parent);

		for (int index = 0; index < childCount; ++index)
		{
			DependencyObject child = VisualTreeHelper.GetChild(parent, index);

			// The parts inside a text box or drop-down (such as the text box of a drop-down's template) are not boxes of their own.
			if (child is TextBox or ComboBox)
			{
				found.Add((Control)child);
				continue;
			}

			CollectEditors(child, found);
		}
	}

	/// <summary>
	///	Finds the first visual descendant of the requested type below a parent.
	/// </summary>
	/// <typeparam name="T">
	///	The descendant type to find.
	/// </typeparam>
	/// <param name="parent">
	///	The visual parent to search.
	/// </param>
	/// <returns>
	///	The first matching descendant, or <see langword="null"/> when none is found.
	/// </returns>
	private static T? FindVisualDescendant<T>(DependencyObject parent) where T : DependencyObject
	{
		int childCount = VisualTreeHelper.GetChildrenCount(parent);

		for (int index = 0; index < childCount; ++index)
		{
			DependencyObject child = VisualTreeHelper.GetChild(parent, index);

			if (child is T match)
			{
				return match;
			}

			if (FindVisualDescendant<T>(child) is T descendant)
			{
				return descendant;
			}
		}

		return null;
	}

	// Only the visual tree is followed, so boxes inside pop-ups (which are not visual children of the cell) are ignored.
	/// <summary>
	///	Finds the first visual ancestor of the requested type above an element.
	/// </summary>
	/// <typeparam name="T">
	///	The ancestor type to find.
	/// </typeparam>
	/// <param name="element">
	///	The element whose parents are searched.
	/// </param>
	/// <returns>
	///	The first matching ancestor, or <see langword="null"/> when none is found.
	/// </returns>
	private static T? FindVisualAncestor<T>(DependencyObject element) where T : DependencyObject
	{
		DependencyObject? current = element is Visual ? VisualTreeHelper.GetParent(element) : null;

		while (current is not null)
		{
			if (current is T match)
			{
				return match;
			}

			current = VisualTreeHelper.GetParent(current);
		}

		return null;
	}

	/// <summary>
	///	Moves keyboard focus to an editor and selects all text when the target is a text box.
	/// </summary>
	/// <param name="target">
	///	The editor that should receive focus.
	/// </param>
	private static void MoveFocus(Control target)
	{
		target.BringIntoView();
		_ = target.Focus();

		if (target is TextBox textBox)
		{
			textBox.SelectAll();
		}
	}
	#endregion PRIVATE
	#endregion METHODS
}
