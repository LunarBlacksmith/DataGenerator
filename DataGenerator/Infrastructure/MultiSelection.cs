using System.Collections;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace DataGenerator.Infrastructure;

/// <summary>
/// Keeps the selected items of a multi-select control (e.g. a DataGrid with SelectionMode Extended) and a view model
/// collection the same, in both directions. A new collection starts with nothing selected.
/// </summary>
public static class MultiSelection
{
	public static readonly DependencyProperty SELECTED_ITEMS_PROPERTY = DependencyProperty.RegisterAttached(
		"SelectedItems",
		typeof(IList),
		typeof(MultiSelection),
		new PropertyMetadata(null, OnSelectedItemsChanged)
	);

	private static readonly DependencyProperty SYNCHRONIZER_PROPERTY = DependencyProperty.RegisterAttached(
		"Synchronizer",
		typeof(Synchronizer),
		typeof(MultiSelection),
		new PropertyMetadata(null)
	);

	public static IList? GetSelectedItems(DependencyObject element)
		=> (IList?)(element ?? throw new ArgumentNullException(nameof(element))).GetValue(SELECTED_ITEMS_PROPERTY);

	public static void SetSelectedItems(DependencyObject element, IList? value)
		=> (element ?? throw new ArgumentNullException(nameof(element))).SetValue(SELECTED_ITEMS_PROPERTY, value);

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

	private sealed class Synchronizer
	{
		private readonly MultiSelector _selector;
		private readonly IList         _items;
		private bool                   _isSynchronizing;

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

		public void Detach()
		{
			_selector.SelectionChanged -= OnSelectorSelectionChanged;

			if (_items is INotifyCollectionChanged observableItems)
			{
				observableItems.CollectionChanged -= OnItemsChanged;
			}
		}

		private void OnSelectorSelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			// Selection changes of nested selectors (e.g. a ComboBox in a cell) bubble up to the grid.
			if (_isSynchronizing || !ReferenceEquals(e.OriginalSource, _selector))
			{
				return;
			}

			Synchronize(_selector.SelectedItems, _items);
		}

		private void OnItemsChanged(object? sender, NotifyCollectionChangedEventArgs e)
		{
			if (!_isSynchronizing)
			{
				Synchronize(_items, _selector.SelectedItems);
			}
		}

		/// <summary>
		/// Makes the target hold the items of the source that are shown by the selector, changing only what differs.
		/// </summary>
		private void Synchronize(IList source, IList target)
		{
			_isSynchronizing = true;

			try
			{
				List<object> wanted = [.. source.Cast<object>().Where(item => _selector.Items.Contains(item))];

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
	}
}