using System.Collections;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace DataGenerator.Infrastructure;

/// <summary>
///	Keeps the selected items of a multi-select control (e.g. a DataGrid with SelectionMode Extended) and a view model
///	collection the same, in both directions. A new collection starts with nothing selected.
/// </summary>
public static class MultiSelection
{
	#region FIELDS
	#region PUBLIC
	public static readonly DependencyProperty SELECTED_ITEMS_PROPERTY;
	#endregion PUBLIC

	#region PRIVATE
	private static readonly DependencyProperty SYNCHRONIZER_PROPERTY;
	#endregion PRIVATE
	#endregion FIELDS

	#region CONSTRUCTOR
	/// <summary>
	///	Sets the default values of the static fields and properties of <see cref="MultiSelection"/>.
	/// </summary>
	static MultiSelection()
	{
		SELECTED_ITEMS_PROPERTY = DependencyProperty.RegisterAttached(
			"SelectedItems",
			typeof(IList),
			typeof(MultiSelection),
			new PropertyMetadata(null, OnSelectedItemsChanged)
		);


		SYNCHRONIZER_PROPERTY = DependencyProperty.RegisterAttached(
			"Synchronizer",
			typeof(Synchronizer),
			typeof(MultiSelection),
			new PropertyMetadata(null)
		);
	}
	#endregion CONSTRUCTOR

	#region METHODS
	#region PUBLIC
	/// <summary>
	///	Gets the view model collection bound to the selector's selected items.
	/// </summary>
	/// <param name="element">
	///	The selector that stores the attached collection.
	/// </param>
	/// <returns>
	///	The bound selected-items collection, or <see langword="null"/> when none is set.
	/// </returns>
	/// <exception cref="ArgumentNullException">
	///	Thrown when <paramref name="element"/> is <see langword="null"/>.
	/// </exception>
	public static IList? GetSelectedItems(DependencyObject element)
		=> (IList?)(element ?? throw new ArgumentNullException(nameof(element))).GetValue(SELECTED_ITEMS_PROPERTY);

	/// <summary>
	///	Sets the view model collection bound to the selector's selected items.
	/// </summary>
	/// <param name="element">
	///	The selector that stores the attached collection.
	/// </param>
	/// <param name="value">
	///	The collection to keep in sync with the selector, or <see langword="null"/> to detach synchronisation.
	/// </param>
	/// <exception cref="ArgumentNullException">
	///	Thrown when <paramref name="element"/> is <see langword="null"/>.
	/// </exception>
	public static void SetSelectedItems(DependencyObject element, IList? value)
		=> (element ?? throw new ArgumentNullException(nameof(element))).SetValue(SELECTED_ITEMS_PROPERTY, value);
	#endregion PUBLIC

	#region PRIVATE
	/// <summary>
	///	Replaces any existing synchroniser when a selector is bound to a new collection.
	/// </summary>
	/// <param name="element">
	///	The element whose attached collection changed.
	/// </param>
	/// <param name="e">
	///	The old and new attached collection values.
	/// </param>
	private static void OnSelectedItemsChanged(DependencyObject element, DependencyPropertyChangedEventArgs e)
	{
		if (element is not MultiSelector selector)
		{
			return;
		}

		(selector.GetValue(SYNCHRONIZER_PROPERTY) as Synchronizer)?.Detach();
		selector.ClearValue(SYNCHRONIZER_PROPERTY);

		if (e.NewValue is IList items)
		{
			selector.SetValue(SYNCHRONIZER_PROPERTY, new Synchronizer(selector, items));
		}
	}
	#endregion PRIVATE
	#endregion METHODS

	#region TYPES
	private sealed class Synchronizer
	{
		#region FIELDS
		private readonly MultiSelector _selector;
		private readonly IList         _items;
		private bool                   _isSynchronizing;
		#endregion FIELDS

		#region CONSTRUCTOR
		/// <summary>
		///	Creates a synchroniser for a selector and a bound collection, starting with no selected items.
		/// </summary>
		/// <param name="selector">
		///	The selector whose selection is mirrored.
		/// </param>
		/// <param name="items">
		///	The collection that receives the selected items and may also drive them.
		/// </param>
		public Synchronizer(MultiSelector selector, IList items)
		{
			_selector        = selector;
			_items           = items;
			_isSynchronizing = true;

			try
			{
				_items.Clear();
				_selector.UnselectAll();
			}
			finally
			{
				_isSynchronizing = false;
			}

			_selector.SelectionChanged += OnSelectorSelectionChanged;

			if (_items is INotifyCollectionChanged observableItems)
			{
				observableItems.CollectionChanged += OnItemsChanged;
			}
		}
		#endregion CONSTRUCTOR

		#region METHODS
		#region PUBLIC
		/// <summary>
		///	Stops listening to selector and collection changes.
		/// </summary>
		public void Detach()
		{
			_selector.SelectionChanged -= OnSelectorSelectionChanged;

			if (_items is INotifyCollectionChanged observableItems)
			{
				observableItems.CollectionChanged -= OnItemsChanged;
			}
		}
		#endregion PUBLIC

		#region PRIVATE
		/// <summary>
		///	Copies user selection changes from the selector into the bound collection.
		/// </summary>
		/// <param name="sender">
		///	The selector that raised the event.
		/// </param>
		/// <param name="e">
		///	The selection change raised by WPF.
		/// </param>
		private void OnSelectorSelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			// Selection changes of nested selectors (e.g. a ComboBox in a cell) bubble up to the grid.
			if (_isSynchronizing || !ReferenceEquals(e.OriginalSource, _selector))
			{
				return;
			}

			Synchronize(_selector.SelectedItems, _items);
		}

		/// <summary>
		///	Copies bound collection changes back into the selector.
		/// </summary>
		/// <param name="sender">
		///	The observable collection that raised the event.
		/// </param>
		/// <param name="e">
		///	The collection change notification.
		/// </param>
		private void OnItemsChanged(object? sender, NotifyCollectionChangedEventArgs e)
		{
			if (!_isSynchronizing)
			{
				Synchronize(_items, _selector.SelectedItems);
			}
		}

		/// <summary>
		///	Makes the target hold the items of the source that are shown by the selector, changing only what differs.
		/// </summary>
		/// <param name="source">
		///	The collection whose items should be mirrored.
		/// </param>
		/// <param name="target">
		///	The collection to update.
		/// </param>
		private void Synchronize(IList source, IList target)
		{
			_isSynchronizing = true;

			try
			{
				List<object> wanted = [..
					source
						.Cast<object>()
						.Where(item => _selector.Items.Contains(item))
				];

				for (int index = target.Count - 1; index >= 0; --index)
				{
					if (!wanted.Contains(target[index]!))
					{
						target.RemoveAt(index);
					}
				}

				foreach (object item in wanted)
				{
					if (!target.Contains(item))
					{
						_ = target.Add(item);
					}
				}
			}
			finally
			{
				_isSynchronizing = false;
			}
		}
		#endregion PRIVATE
		#endregion METHODS
	}
	#endregion TYPES
}