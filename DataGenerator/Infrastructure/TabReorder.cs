using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace DataGenerator.Infrastructure;

/// <summary>
///	Reorders the tabs of a <see cref="TabControl"/> by dragging their headers: a plain drag moves a tab before or after
///	another tab, and Shift+drag swaps two tabs.
///	<para>
///		The drag captures the mouse instead of using OLE drag and drop, so the header strip keeps scrolling while a tab is
///		dragged: hold the pointer near either end of the strip, or turn the mouse wheel.
///	</para>
/// </summary>
public static class TabReorder
{
	#region FIELDS
	#region PUBLIC
	public static readonly DependencyProperty MOVE_COMMAND_PROPERTY;
	public static readonly DependencyProperty SWAP_COMMAND_PROPERTY;
	#endregion PUBLIC

	#region PRIVATE
	private const string ACCENT_BRUSH_KEY                  = "AccentBrush";
	private const double EDGE_ZONE                         = 40;
	private const double EDGE_SCROLL_STEP                  = 14;
	private const double MIN_EDGE_SCROLL_FACTOR            = 0.2;
	private const double MAX_EDGE_SCROLL_FACTOR            = 2.5;
	private const int    EDGE_SCROLL_INTERVAL_MILLISECONDS = 16;
	private const double DRAGGED_TAB_OPACITY               = 0.55;

	private static readonly DependencyProperty STATE_PROPERTY;
	#endregion PRIVATE
	#endregion FIELDS

	#region CONSTRUCTOR
	/// <summary>
	///	Registers the move and swap commands and the per-control drag state.
	/// </summary>
	static TabReorder()
	{
		MOVE_COMMAND_PROPERTY = DependencyProperty.RegisterAttached(
			"MoveCommand",
			typeof(ICommand),
			typeof(TabReorder),
			new PropertyMetadata(null, OnCommandChanged)
		);
		SWAP_COMMAND_PROPERTY = DependencyProperty.RegisterAttached(
			"SwapCommand",
			typeof(ICommand),
			typeof(TabReorder),
			new PropertyMetadata(null, OnCommandChanged)
		);
		STATE_PROPERTY = DependencyProperty.RegisterAttached(
			"State",
			typeof(DragState),
			typeof(TabReorder),
			new PropertyMetadata(null)
		);
	}
	#endregion CONSTRUCTOR

	#region METHODS
	#region PUBLIC
	/// <summary>
	///	Gets the command used by a plain drag to move a tab before or after another tab.
	/// </summary>
	/// <param name="element">
	///	The tab control storing the command.
	/// </param>
	/// <returns>
	///	The command receiving the source item, the target item and whether to insert after the target.
	/// </returns>
	public static ICommand? GetMoveCommand(DependencyObject element) => (ICommand?)element.GetValue(MOVE_COMMAND_PROPERTY);

	/// <summary>
	///	Sets the command used by a plain drag to move a tab before or after another tab.
	/// </summary>
	/// <param name="element">
	///	The tab control storing the command.
	/// </param>
	/// <param name="value">
	///	The move command, or null to disable moving.
	/// </param>
	public static void SetMoveCommand(DependencyObject element, ICommand? value) => element.SetValue(MOVE_COMMAND_PROPERTY, value);

	/// <summary>
	///	Gets the command used by Shift+drag to swap two tabs.
	/// </summary>
	/// <param name="element">
	///	The tab control storing the command.
	/// </param>
	/// <returns>
	///	The command receiving a tuple of the source and target items.
	/// </returns>
	public static ICommand? GetSwapCommand(DependencyObject element) => (ICommand?)element.GetValue(SWAP_COMMAND_PROPERTY);

	/// <summary>
	///	Sets the command used by Shift+drag to swap two tabs.
	/// </summary>
	/// <param name="element">
	///	The tab control storing the command.
	/// </param>
	/// <param name="value">
	///	The swap command, or null to disable swapping.
	/// </param>
	public static void SetSwapCommand(DependencyObject element, ICommand? value) => element.SetValue(SWAP_COMMAND_PROPERTY, value);

	/// <summary>
	///	Calculates how far the header strip scrolls on one auto-scroll tick for a pointer near, or beyond, either end.
	///	<para>
	///		The speed grows the deeper the pointer is inside the edge zone and keeps growing, up to a limit, beyond the strip.
	///	</para>
	/// </summary>
	/// <param name="pointerX">
	///	The pointer's horizontal position relative to the header strip.
	/// </param>
	/// <param name="stripWidth">
	///	The visible width of the header strip.
	/// </param>
	/// <returns>
	///	A negative offset to scroll left, a positive offset to scroll right, or zero outside the edge zones.
	/// </returns>
	public static double GetEdgeScrollStep(double pointerX, double stripWidth)
	{
		if (pointerX < EDGE_ZONE)
		{
			return -EDGE_SCROLL_STEP * Math.Clamp((EDGE_ZONE - pointerX) / EDGE_ZONE, MIN_EDGE_SCROLL_FACTOR, MAX_EDGE_SCROLL_FACTOR);
		}

		if (pointerX > stripWidth - EDGE_ZONE)
		{
			return EDGE_SCROLL_STEP * Math.Clamp((pointerX - (stripWidth - EDGE_ZONE)) / EDGE_ZONE, MIN_EDGE_SCROLL_FACTOR, MAX_EDGE_SCROLL_FACTOR);
		}

		return 0;
	}
	#endregion PUBLIC

	#region PRIVATE
	/// <summary>
	///	Connects the pointer handlers while either command is set and removes them, and any drag in progress, otherwise.
	/// </summary>
	/// <param name="element">
	///	The tab control whose command changed.
	/// </param>
	/// <param name="e">
	///	The old and new commands.
	/// </param>
	private static void OnCommandChanged(DependencyObject element, DependencyPropertyChangedEventArgs e)
	{
		if (element is not TabControl tabs)
		{
			return;
		}

		if (tabs.GetValue(STATE_PROPERTY) is DragState state)
		{
			EndDrag(tabs, state);
		}

		tabs.PreviewMouseLeftButtonDown -= OnMouseDown;
		tabs.PreviewMouseMove           -= OnMouseMove;
		tabs.RemoveHandler(UIElement.PreviewMouseLeftButtonUpEvent, new MouseButtonEventHandler(OnMouseUp));
		tabs.PreviewMouseWheel          -= OnMouseWheel;
		tabs.PreviewKeyDown             -= OnKeyDown;
		tabs.RemoveHandler(Mouse.LostMouseCaptureEvent, new MouseEventHandler(OnLostMouseCapture));
		tabs.SelectionChanged           -= OnSelectionChanged;
		tabs.Unloaded                   -= OnUnloaded;

		if (GetMoveCommand(tabs) is not null || GetSwapCommand(tabs) is not null)
		{
			tabs.PreviewMouseLeftButtonDown += OnMouseDown;
			tabs.PreviewMouseMove           += OnMouseMove;
			tabs.AddHandler(UIElement.PreviewMouseLeftButtonUpEvent, new MouseButtonEventHandler(OnMouseUp), true);
			tabs.PreviewMouseWheel          += OnMouseWheel;
			tabs.PreviewKeyDown             += OnKeyDown;
			tabs.AddHandler(Mouse.LostMouseCaptureEvent, new MouseEventHandler(OnLostMouseCapture), true);
			tabs.SelectionChanged           += OnSelectionChanged;
			tabs.Unloaded                   += OnUnloaded;
		}
	}

	/// <summary>
	///	Reveals a newly selected header after its container has been laid out, including newly added tabs.
	/// </summary>
	/// <param name="sender">
	///	The tab control whose selection changed.
	/// </param>
	/// <param name="e">
	///	The selection change, ignored when it originates in a child control.
	/// </param>
	private static void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		TabControl tabs = (TabControl)sender;

		if (e.OriginalSource != tabs)
		{
			return;
		}

		_ = tabs.Dispatcher.BeginInvoke(
			new Action(
				() =>
				{
					if (tabs.SelectedItem is object selected
						&& tabs.ItemContainerGenerator.ContainerFromItem(selected) is TabItem tab)
					{
						tab.BringIntoView();
					}
				}
			)
		);
	}

	/// <summary>
	///	Records a possible drag origin when the pointer is pressed over a tab header.
	/// </summary>
	/// <param name="sender">
	///	The tab control receiving the pointer event.
	/// </param>
	/// <param name="e">
	///	The pointer position and original hit element.
	/// </param>
	private static void OnMouseDown(object sender, MouseButtonEventArgs e)
	{
		TabControl tabs = (TabControl)sender;

		if (tabs.GetValue(STATE_PROPERTY) is DragState { IsDragging: true })
		{
			return;
		}

		TabItem? tab = FindTab(tabs, e.OriginalSource as DependencyObject);

		tabs.SetValue(STATE_PROPERTY, tab is null ? null : new DragState(tab, e.GetPosition(tabs)));
	}

	/// <summary>
	///	Starts dragging once the system's drag distance is exceeded, then keeps the drop indicator under the pointer.
	/// </summary>
	/// <param name="sender">
	///	The tab control receiving pointer movement.
	/// </param>
	/// <param name="e">
	///	The current pointer position and button state.
	/// </param>
	private static void OnMouseMove(object sender, MouseEventArgs e)
	{
		TabControl tabs = (TabControl)sender;

		if (tabs.GetValue(STATE_PROPERTY) is not DragState state)
		{
			return;
		}

		if (e.LeftButton != MouseButtonState.Pressed)
		{
			EndDrag(tabs, state);
			return;
		}

		if (state.IsDragging)
		{
			UpdateDrag(tabs, state);
			e.Handled = true;
			return;
		}

		Point position = e.GetPosition(tabs);

		if (Math.Abs(position.X - state.Origin.X) < SystemParameters.MinimumHorizontalDragDistance
			&& Math.Abs(position.Y - state.Origin.Y) < SystemParameters.MinimumVerticalDragDistance)
		{
			return;
		}

		BeginDrag(tabs, state);
		e.Handled = true;
	}

	/// <summary>
	///	Drops the dragged tab, running the move or swap command for a valid target, or forgets an unstarted drag.
	/// </summary>
	/// <param name="sender">
	///	The tab control receiving the button release.
	/// </param>
	/// <param name="e">
	///	The pointer position of the release.
	/// </param>
	private static void OnMouseUp(object sender, MouseButtonEventArgs e)
	{
		TabControl tabs = (TabControl)sender;

		if (tabs.GetValue(STATE_PROPERTY) is not DragState state)
		{
			return;
		}

		if (!state.IsDragging)
		{
			tabs.ClearValue(STATE_PROPERTY);
			return;
		}

		DropOperation? operation;
		try
		{
			operation = ResolveDrop(
				tabs,
				state.Tab,
				e.GetPosition(tabs),
				Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)
			);
		}
		finally
		{
			EndDrag(tabs, state);
		}

		if (operation is not null)
		{
			operation.Command.Execute(operation.Parameter);
		}

		e.Handled = true;
	}

	/// <summary>
	///	Scrolls the header strip with the mouse wheel while a tab is being dragged.
	/// </summary>
	/// <param name="sender">
	///	The tab control, which holds the mouse capture during a drag.
	/// </param>
	/// <param name="e">
	///	The wheel movement.
	/// </param>
	private static void OnMouseWheel(object sender, MouseWheelEventArgs e)
	{
		TabControl tabs = (TabControl)sender;

		if (tabs.GetValue(STATE_PROPERTY) is not DragState { IsDragging: true } state || state.Scroller is null)
		{
			return;
		}

		HorizontalWheelScroll.ScrollHorizontally(state.Scroller, e.Delta);
		state.Scroller.UpdateLayout();
		UpdateDrag(tabs, state);
		e.Handled = true;
	}

	/// <summary>
	///	Cancels a drag with Escape, and refreshes the indicator when Shift switches between moving and swapping.
	/// </summary>
	/// <param name="sender">
	///	The tab control.
	/// </param>
	/// <param name="e">
	///	The key pressed.
	/// </param>
	private static void OnKeyDown(object sender, KeyEventArgs e)
	{
		TabControl tabs = (TabControl)sender;

		if (tabs.GetValue(STATE_PROPERTY) is not DragState { IsDragging: true } state)
		{
			return;
		}

		if (e.Key == Key.Escape)
		{
			EndDrag(tabs, state);
			e.Handled = true;
		}
		else
		{
			UpdateDrag(tabs, state);
		}
	}

	/// <summary>
	///	Cancels a drag whose mouse capture was taken away, for example by another window or application.
	/// </summary>
	/// <param name="sender">
	///	The tab control that lost the capture.
	/// </param>
	/// <param name="e">
	///	The capture change.
	/// </param>
	private static void OnLostMouseCapture(object sender, MouseEventArgs e)
	{
		TabControl tabs = (TabControl)sender;

		if (!tabs.IsMouseCaptured && tabs.GetValue(STATE_PROPERTY) is DragState { IsDragging: true } state)
		{
			EndDrag(tabs, state);
		}
	}

	/// <summary>
	///	Restores drag state when the control leaves the visual tree.
	/// </summary>
	private static void OnUnloaded(object sender, RoutedEventArgs e)
	{
		TabControl tabs = (TabControl)sender;

		if (tabs.GetValue(STATE_PROPERTY) is DragState state)
		{
			EndDrag(tabs, state);
		}
	}

	/// <summary>
	///	Captures the mouse, dims the dragged header and starts the edge auto-scroll timer.
	/// </summary>
	/// <param name="tabs">
	///	The tab control whose tab is dragged.
	/// </param>
	/// <param name="state">
	///	The pending drag.
	/// </param>
	private static void BeginDrag(TabControl tabs, DragState state)
	{
		// Capture can synchronously reenter the input handlers, so save restoration state first.
		state.IsDragging      = true;
		state.OriginalOpacity = state.Tab.Opacity;
		state.PreviousCursor  = Mouse.OverrideCursor;
		try
		{
			if (!tabs.CaptureMouse())
			{
				EndDrag(tabs, state);
				return;
			}

			if (!state.IsDragging || !ReferenceEquals(tabs.GetValue(STATE_PROPERTY), state))
			{
				return;
			}

			state.Scroller = FindHeaderScroller(tabs, state.Tab);
			state.Tab.SetCurrentValue(UIElement.OpacityProperty, DRAGGED_TAB_OPACITY);
			state.Timer = new DispatcherTimer(DispatcherPriority.Input, tabs.Dispatcher)
			{
				Interval = TimeSpan.FromMilliseconds(EDGE_SCROLL_INTERVAL_MILLISECONDS)
			};

			state.Timer.Tick += (_, _) => OnDragTick(tabs, state);
			state.Timer.Start();
			UpdateDrag(tabs, state);
		}
		catch
		{
			EndDrag(tabs, state);
			throw;
		}
	}

	/// <summary>
	///	Scrolls the header strip while the pointer is held near one of its ends and refreshes the drop indicator.
	/// </summary>
	/// <param name="tabs">
	///	The tab control whose tab is dragged.
	/// </param>
	/// <param name="state">
	///	The drag in progress.
	/// </param>
	private static void OnDragTick(TabControl tabs, DragState state)
	{
		if (!state.IsDragging)
		{
			return;
		}

		if (Mouse.LeftButton != MouseButtonState.Pressed || !tabs.IsMouseCaptured)
		{
			EndDrag(tabs, state);
			return;
		}

		ScrollViewer? scroller = state.Scroller;

		if (scroller is not null && scroller.ScrollableWidth > 0)
		{
			Point  position = Mouse.GetPosition(scroller);
			double step     = GetEdgeScrollStep(position.X, scroller.ActualWidth);

			if (step != 0 && position.Y >= -EDGE_ZONE && position.Y <= scroller.ActualHeight + EDGE_ZONE)
			{
				scroller.ScrollToHorizontalOffset(scroller.HorizontalOffset + step);
				scroller.UpdateLayout();
			}
		}

		UpdateDrag(tabs, state);
	}

	/// <summary>
	///	Shows where the dragged tab would go and whether it can be dropped there.
	/// </summary>
	/// <param name="tabs">
	///	The tab control whose tab is dragged.
	/// </param>
	/// <param name="state">
	///	The drag in progress.
	/// </param>
	private static void UpdateDrag(TabControl tabs, DragState state)
	{
		if (!state.IsDragging || !ReferenceEquals(tabs.GetValue(STATE_PROPERTY), state))
		{
			return;
		}

		DropOperation? operation = ResolveDrop(
			tabs,
			state.Tab,
			Mouse.GetPosition(tabs),
			Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)
		);

		Mouse.OverrideCursor =
			operation is null
				? Cursors.No
				: Cursors.Hand;

		if (operation is null)
		{
			RemoveIndicator(state);
			return;
		}

		if (state.Indicator is null || !ReferenceEquals(state.Indicator.AdornedElement, operation.Target))
		{
			RemoveIndicator(state);

			AdornerLayer? layer = AdornerLayer.GetAdornerLayer(operation.Target);

			if (layer is null)
			{
				return;
			}

			Brush brush = tabs.TryFindResource(ACCENT_BRUSH_KEY) as Brush ?? SystemColors.HighlightBrush;

			state.Indicator = new DropIndicatorAdorner(operation.Target, brush);
			layer.Add(state.Indicator);
		}

		state.Indicator.Show(operation.IsSwap, operation.After);
	}

	/// <summary>
	///	Ends a drag: restores the header, cursor and capture, and removes the indicator and timer.
	/// </summary>
	/// <param name="tabs">
	///	The tab control whose tab was dragged.
	/// </param>
	/// <param name="state">
	///	The drag to end.
	/// </param>
	private static void EndDrag(TabControl tabs, DragState state)
	{
		// The state is cleared first so the capture release below does not end the drag a second time.
		if (ReferenceEquals(tabs.GetValue(STATE_PROPERTY), state))
		{
			tabs.ClearValue(STATE_PROPERTY);
		}

		if (!state.IsDragging)
		{
			return;
		}

		state.IsDragging = false;
		state.Timer?.Stop();
		RemoveIndicator(state);
		state.Tab.SetCurrentValue(UIElement.OpacityProperty, state.OriginalOpacity);
		Mouse.OverrideCursor = state.PreviousCursor;

		if (tabs.IsMouseCaptured)
		{
			tabs.ReleaseMouseCapture();
		}
	}

	/// <summary>
	///	Removes the drop indicator from its header, if one is shown.
	/// </summary>
	/// <param name="state">
	///	The drag owning the indicator.
	/// </param>
	private static void RemoveIndicator(DragState state)
	{
		if (state.Indicator is null)
		{
			return;
		}

		AdornerLayer.GetAdornerLayer(state.Indicator.AdornedElement)?.Remove(state.Indicator);
		state.Indicator = null;
	}

	/// <summary>
	///	Works out what dropping the dragged tab at a position would do.
	///	<para>
	///		Moving uses the nearest header within the strip's height and its left or right half as the insertion edge;
	///		swapping needs the pointer directly over another header. Drops that would change nothing are rejected.
	///	</para>
	/// </summary>
	/// <param name="tabs">
	///	The tab control whose tab is dragged.
	/// </param>
	/// <param name="source">
	///	The dragged tab.
	/// </param>
	/// <param name="position">
	///	The pointer position relative to <paramref name="tabs"/>.
	/// </param>
	/// <param name="swap">
	///	Whether Shift is held, which swaps instead of moving.
	/// </param>
	/// <returns>
	///	The command, parameter and target of the drop, or null when the drop is not possible.
	/// </returns>
	private static DropOperation? ResolveDrop(TabControl tabs, TabItem source, Point position, bool swap)
	{
		object sourceItem = tabs.ItemContainerGenerator.ItemFromContainer(source);

		if (sourceItem == DependencyProperty.UnsetValue)
		{
			return null;
		}

		TabItem? target =
			swap
				? FindTab(tabs, tabs.InputHitTest(position) as DependencyObject)
				: FindNearestTab(tabs, FindHeaderScroller(tabs, source), position);

		if (target is null || ReferenceEquals(source, target))
		{
			return null;
		}

		object targetItem = tabs.ItemContainerGenerator.ItemFromContainer(target);

		if (targetItem == DependencyProperty.UnsetValue)
		{
			return null;
		}

		if (swap)
		{
			ICommand?             swapCommand = GetSwapCommand(tabs);
			Tuple<object, object> pair        = Tuple.Create(sourceItem, targetItem);

			return
				swapCommand?.CanExecute(pair) == true
					? new DropOperation(swapCommand, pair, target, true, false)
					: null;
		}

		bool after       = tabs.TranslatePoint(position, target).X >= target.ActualWidth / 2;
		int  sourceIndex = tabs.Items.IndexOf(sourceItem);
		int  targetIndex = tabs.Items.IndexOf(targetItem);

		// Dropping on the edge next to the dragged tab would leave it where it is.
		if ((after && targetIndex == sourceIndex - 1) || (!after && targetIndex == sourceIndex + 1))
		{
			return null;
		}

		ICommand?                   moveCommand = GetMoveCommand(tabs);
		Tuple<object, object, bool> move        = Tuple.Create(sourceItem, targetItem, after);

		return
			moveCommand?.CanExecute(move) == true
				? new DropOperation(moveCommand, move, target, false, after)
				: null;
	}

	/// <summary>
	///	Finds the header horizontally nearest to the pointer, provided the pointer is level with the header strip.
	/// </summary>
	/// <param name="tabs">
	///	The tab control whose headers are searched.
	/// </param>
	/// <param name="scroller">
	///	The header strip, or null when the template has none, in which case only a header under the pointer counts.
	/// </param>
	/// <param name="position">
	///	The pointer position relative to <paramref name="tabs"/>.
	/// </param>
	/// <returns>
	///	The nearest visible header, or null when the pointer is above or below the strip.
	/// </returns>
	private static TabItem? FindNearestTab(TabControl tabs, ScrollViewer? scroller, Point position)
	{
		if (scroller is null)
		{
			return FindTab(tabs, tabs.InputHitTest(position) as DependencyObject);
		}

		Point stripPosition = tabs.TranslatePoint(position, scroller);

		if (stripPosition.Y < -EDGE_ZONE || stripPosition.Y > scroller.ActualHeight + EDGE_ZONE)
		{
			return null;
		}

		// Beyond either end of the strip, the nearest header still in view is the target rather than one scrolled out of view.
		Point    pointer         = scroller.TranslatePoint(
			new Point(Math.Clamp(stripPosition.X, 0, scroller.ActualWidth), stripPosition.Y),
			tabs
		);
		TabItem? nearest         = null;
		double   nearestDistance = double.MaxValue;

		foreach (object item in tabs.Items)
		{
			if (tabs.ItemContainerGenerator.ContainerFromItem(item) is not TabItem tab || !tab.IsVisible)
			{
				continue;
			}

			double x = tabs.TranslatePoint(pointer, tab).X;
			double distance =
				x < 0
					? -x
					: x > tab.ActualWidth
						? x - tab.ActualWidth
						: 0;

			if (distance < nearestDistance)
			{
				nearest         = tab;
				nearestDistance = distance;
			}
		}

		return nearest;
	}

	/// <summary>
	///	Finds the scroll viewer that holds the header strip of a tab control.
	/// </summary>
	/// <param name="tabs">
	///	The tab control whose template is searched.
	/// </param>
	/// <param name="tab">
	///	One of its headers.
	/// </param>
	/// <returns>
	///	The nearest scroll viewer above the header inside the tab control, or null when the headers do not scroll.
	/// </returns>
	private static ScrollViewer? FindHeaderScroller(TabControl tabs, TabItem tab)
	{
		DependencyObject? element = VisualTreeHelper.GetParent(tab);

		while (element is not null && !ReferenceEquals(element, tabs))
		{
			if (element is ScrollViewer scroller)
			{
				return scroller;
			}

			element = VisualTreeHelper.GetParent(element);
		}

		return null;
	}

	/// <summary>
	///	Walks a header's visual ancestors to find its owning tab.
	/// </summary>
	/// <param name="tabs">
	///	The expected tab control.
	/// </param>
	/// <param name="element">
	///	The element hit by the pointer.
	/// </param>
	/// <returns>
	///	A tab belonging to the control, or null when the pointer is over its content.
	/// </returns>
	private static TabItem? FindTab(TabControl tabs, DependencyObject? element)
	{
		while (element is not null)
		{
			if (element is TabItem tab)
			{
				return
					ItemsControl.ItemsControlFromItemContainer(tab) == tabs
						? tab
						: null;
			}

			element =
				element is Visual
					? VisualTreeHelper.GetParent(element)
					: LogicalTreeHelper.GetParent(element);
		}

		return null;
	}
	#endregion PRIVATE
	#endregion METHODS

	#region TYPES
	private sealed class DragState
	{
		#region PROPERTIES
		public TabItem               Tab             { get; }
		public Point                 Origin          { get; }
		public bool                  IsDragging      { get; set; }
		public ScrollViewer?         Scroller        { get; set; }
		public DispatcherTimer?      Timer           { get; set; }
		public DropIndicatorAdorner? Indicator       { get; set; }
		public Cursor?               PreviousCursor  { get; set; }
		public double                OriginalOpacity { get; set; }
		#endregion PROPERTIES

		/// <summary>
		///	Captures the tab and pointer position at the start of a possible drag.
		/// </summary>
		/// <param name="tab">
		///	The source header.
		/// </param>
		/// <param name="origin">
		///	The pointer's initial position relative to the control.
		/// </param>
		public DragState(TabItem tab, Point origin)
		{
			IsDragging      = false;
			Scroller        = null;
			Timer           = null;
			Indicator       = null;
			PreviousCursor  = null;
			OriginalOpacity = 1;

			Tab    = tab;
			Origin = origin;
		}
	}

	private sealed class DropOperation
	{
		#region PROPERTIES
		public ICommand Command   { get; }
		public object   Parameter { get; }
		public TabItem  Target    { get; }
		public bool     IsSwap    { get; }
		public bool     After     { get; }
		#endregion PROPERTIES

		/// <summary>
		///	Describes a possible drop.
		/// </summary>
		/// <param name="command">
		///	The command that performs it.
		/// </param>
		/// <param name="parameter">
		///	The command parameter.
		/// </param>
		/// <param name="target">
		///	The header the drop refers to.
		/// </param>
		/// <param name="isSwap">
		///	Whether the drop swaps instead of moving.
		/// </param>
		/// <param name="after">
		///	Whether a move inserts after the target instead of before it.
		/// </param>
		public DropOperation(ICommand command, object parameter, TabItem target, bool isSwap, bool after)
		{
			Command   = command;
			Parameter = parameter;
			Target    = target;
			IsSwap    = isSwap;
			After     = after;
		}
	}

	private sealed class DropIndicatorAdorner : Adorner
	{
		#region FIELDS
		private const double SWAP_OUTLINE_THICKNESS = 2;
		private const double INSERTION_BAR_WIDTH    = 3;

		private readonly Brush _brush;
		private bool           _isSwap;
		private bool           _after;
		#endregion FIELDS

		#region CONSTRUCTOR
		/// <summary>
		///	Creates an indicator drawn over a header without taking part in hit testing.
		/// </summary>
		/// <param name="target">
		///	The header to draw over.
		/// </param>
		/// <param name="brush">
		///	The accent brush of the current theme.
		/// </param>
		public DropIndicatorAdorner(UIElement target, Brush brush) : base(target)
		{
			_isSwap = false;
			_after  = false;

			_brush           = brush;
			IsHitTestVisible = false;
		}
		#endregion CONSTRUCTOR

		#region METHODS
		#region PUBLIC
		/// <summary>
		///	Switches between an outline for a swap and an insertion bar on the left or right edge for a move.
		/// </summary>
		/// <param name="isSwap">
		///	Whether the drop swaps.
		/// </param>
		/// <param name="after">
		///	Whether a move inserts after the header.
		/// </param>
		public void Show(bool isSwap, bool after)
		{
			if (_isSwap == isSwap && _after == after)
			{
				return;
			}

			_isSwap = isSwap;
			_after  = after;
			InvalidateVisual();
		}
		#endregion PUBLIC

		#region PROTECTED
		/// <summary>
		///	Draws the outline or insertion bar.
		/// </summary>
		/// <param name="drawingContext">
		///	The drawing target.
		/// </param>
		protected override void OnRender(DrawingContext drawingContext)
		{
			Size size = AdornedElement.RenderSize;

			if (_isSwap)
			{
				drawingContext.DrawRectangle(
					null,
					new Pen(_brush, SWAP_OUTLINE_THICKNESS),
					new Rect(
						SWAP_OUTLINE_THICKNESS / 2,
						SWAP_OUTLINE_THICKNESS / 2,
						Math.Max(0, size.Width - SWAP_OUTLINE_THICKNESS),
						Math.Max(0, size.Height - SWAP_OUTLINE_THICKNESS)
					)
				);

				return;
			}

			double left =
				_after
					? size.Width - INSERTION_BAR_WIDTH
					: 0;

			drawingContext.DrawRectangle(_brush, null, new Rect(left, 0, INSERTION_BAR_WIDTH, size.Height));
		}
		#endregion PROTECTED
		#endregion METHODS
	}
	#endregion TYPES
}
