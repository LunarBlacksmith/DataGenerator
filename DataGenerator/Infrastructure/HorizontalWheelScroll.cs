using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace DataGenerator.Infrastructure;

/// <summary>
///	Makes horizontal scrolling with the mouse wheel work throughout the application.
///	<para>
///		<see cref="Register"/> makes Shift + wheel scroll the innermost horizontally scrollable element under the pointer
///		sideways in every window and pop-up, such as the column rules grid, wide text boxes and long sample values.
///	</para>
///	<para>
///		IsEnabled is for horizontally scrolling content inside a scrolling list: the plain wheel scrolls the surrounding list
///		instead of being swallowed. Set PlainWheelIsHorizontal for strips such as row-set tabs where the plain wheel should
///		scroll sideways too.
///	</para>
/// </summary>
public static class HorizontalWheelScroll
{
	#region FIELDS
	#region PUBLIC
	public const double PIXELS_PER_WHEEL_NOTCH = 96;

	public static readonly DependencyProperty IS_ENABLED_PROPERTY;
	public static readonly DependencyProperty PLAIN_WHEEL_IS_HORIZONTAL_PROPERTY;
	#endregion PUBLIC

	#region PRIVATE
	private static bool IS_REGISTERED;
	#endregion PRIVATE
	#endregion FIELDS

	#region CONSTRUCTORS
	#region STATIC
	/// <summary>
	///	Sets the default values of the static fields and properties of <see cref="HorizontalWheelScroll"/>.
	/// </summary>
	static HorizontalWheelScroll()
	{
		IS_REGISTERED = false;

		PLAIN_WHEEL_IS_HORIZONTAL_PROPERTY = DependencyProperty.RegisterAttached(
			"PlainWheelIsHorizontal",
			typeof(bool),
			typeof(HorizontalWheelScroll),
			new PropertyMetadata(false)
		);
		IS_ENABLED_PROPERTY =
			DependencyProperty.RegisterAttached(
				"IsEnabled",
				typeof(bool),
				typeof(HorizontalWheelScroll),
				new FrameworkPropertyMetadata(false, OnIsEnabledChanged)
			);
	}
	#endregion STATIC
	#endregion CONSTRUCTORS

	#region METHODS
	#region PUBLIC
	/// <summary>
	///	Gets whether horizontal wheel handling is enabled on a scroll viewer.
	/// </summary>
	/// <param name="element">
	///	The element that stores the attached setting.
	/// </param>
	/// <returns>
	///	<see langword="true"/> when the behaviour is enabled; otherwise <see langword="false"/>.
	/// </returns>
	public static bool GetIsEnabled(DependencyObject element) => (bool)element.GetValue(IS_ENABLED_PROPERTY);

	/// <summary>
	///	Sets whether horizontal wheel handling is enabled on a scroll viewer.
	/// </summary>
	/// <param name="element">
	///	The element that stores the attached setting.
	/// </param>
	/// <param name="value">
	///	Whether the behaviour should handle the scroll viewer's wheel events.
	/// </param>
	public static void SetIsEnabled(DependencyObject element, bool value) => element.SetValue(IS_ENABLED_PROPERTY, value);

	/// <summary>
	///	Gets whether the plain mouse wheel scrolls sideways instead of being forwarded to a parent.
	/// </summary>
	/// <param name="element">
	///	The scroll viewer storing the setting.
	/// </param>
	/// <returns>
	///	Whether horizontal scrolling also handles the plain wheel.
	/// </returns>
	public static bool GetPlainWheelIsHorizontal(DependencyObject element) => (bool)element.GetValue(PLAIN_WHEEL_IS_HORIZONTAL_PROPERTY);

	/// <summary>
	///	Sets whether the plain mouse wheel scrolls sideways, as on a row-set tab strip.
	/// </summary>
	/// <param name="element">
	///	The scroll viewer storing the setting.
	/// </param>
	/// <param name="value">
	///	Whether to handle the plain wheel horizontally.
	/// </param>
	public static void SetPlainWheelIsHorizontal(DependencyObject element, bool value) => element.SetValue(PLAIN_WHEEL_IS_HORIZONTAL_PROPERTY, value);

	/// <summary>
	///	Makes Shift + mouse wheel scroll sideways in every window and pop-up of the application. Calling it again does nothing.
	/// </summary>
	public static void Register()
	{
		if (IS_REGISTERED)
		{
			return;
		}

		EventManager.RegisterClassHandler(
			typeof(FrameworkElement),
			UIElement.PreviewMouseWheelEvent,
			new MouseWheelEventHandler(OnAnyPreviewMouseWheel)
		);
		IS_REGISTERED = true;
	}

	/// <summary>
	///	Scrolls a scroll viewer sideways in proportion to a wheel movement, so high-resolution wheels and touchpads scroll
	///	smoothly.
	/// </summary>
	/// <param name="scrollViewer">
	///	The scroll viewer to scroll.
	/// </param>
	/// <param name="delta">
	///	The wheel delta; positive values (wheel turned away from the user) scroll left.
	/// </param>
	public static void ScrollHorizontally(ScrollViewer scrollViewer, int delta)
		=> scrollViewer.ScrollToHorizontalOffset(
			scrollViewer.HorizontalOffset - delta * PIXELS_PER_WHEEL_NOTCH / Mouse.MouseWheelDeltaForOneLine
		);
	#endregion PUBLIC

	#region PRIVATE
	/// <summary>
	///	Adds or removes the wheel handler when the attached setting changes.
	/// </summary>
	/// <param name="element">
	///	The element whose attached setting changed.
	/// </param>
	/// <param name="e">
	///	The old and new enabled values.
	/// </param>
	private static void OnIsEnabledChanged(DependencyObject element, DependencyPropertyChangedEventArgs e)
	{
		if (element is not ScrollViewer scrollViewer)
		{
			return;
		}

		scrollViewer.PreviewMouseWheel -= OnPreviewMouseWheel;

		if ((bool)e.NewValue)
		{
			scrollViewer.PreviewMouseWheel += OnPreviewMouseWheel;
		}
	}

	/// <summary>
	///	Scrolls horizontally with Shift+wheel or the tab-strip setting, otherwise forwards the wheel to the parent list.
	/// </summary>
	/// <param name="sender">
	///	The scroll viewer that received the wheel event.
	/// </param>
	/// <param name="e">
	///	The mouse wheel event raised by WPF.
	/// </param>
	private static void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
	{
		if (e.Handled || sender is not ScrollViewer scrollViewer)
		{
			return;
		}

		e.Handled = true;

		if (GetPlainWheelIsHorizontal(scrollViewer) || Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) && scrollViewer.ScrollableWidth > 0)
		{
			ScrollHorizontally(scrollViewer, e.Delta);

			return;
		}

		// The plain wheel goes to the surrounding list, as if the content did not scroll at all.
		if ((scrollViewer.Parent ?? VisualTreeHelper.GetParent(scrollViewer)) is UIElement parent)
		{
			parent.RaiseEvent(new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta)
			{
				RoutedEvent = UIElement.MouseWheelEvent,
				Source      = scrollViewer
			});
		}
	}

	/// <summary>
	///	Scrolls the innermost horizontally scrollable element under the pointer sideways when Shift is held.
	///	<para>
	///		Only the root of each window or pop-up acts, as the first element the tunnelling event reaches. Without Shift, with
	///		nothing to scroll sideways, or while an element has captured the mouse, the wheel is left alone.
	///	</para>
	/// </summary>
	/// <param name="sender">
	///	The element the class handler was called for.
	/// </param>
	/// <param name="e">
	///	The wheel movement and the element under the pointer.
	/// </param>
	private static void OnAnyPreviewMouseWheel(object sender, MouseWheelEventArgs e)
	{
		if (e.Handled
			|| !Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)
			|| Mouse.Captured is not null
			|| sender is not Visual root
			|| VisualTreeHelper.GetParent(root) is not null)
		{
			return;
		}

		ScrollViewer? scrollViewer = FindHorizontalScrollViewer(e.OriginalSource as DependencyObject);

		if (scrollViewer is null)
		{
			return;
		}

		ScrollHorizontally(scrollViewer, e.Delta);
		e.Handled = true;
	}

	/// <summary>
	///	Walks up from an element to the first scroll viewer that can currently scroll sideways.
	/// </summary>
	/// <param name="element">
	///	The element under the pointer.
	/// </param>
	/// <returns>
	///	The innermost horizontally scrollable scroll viewer, or null when there is none.
	/// </returns>
	private static ScrollViewer? FindHorizontalScrollViewer(DependencyObject? element)
	{
		while (element is not null)
		{
			if (element is ScrollViewer scrollViewer
				&& scrollViewer.ScrollableWidth > 0
				&& scrollViewer.HorizontalScrollBarVisibility != ScrollBarVisibility.Disabled)
			{
				return scrollViewer;
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
}