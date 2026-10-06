using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace DataGenerator.Infrastructure;

/// <summary>
///	Makes a horizontally scrolling <see cref="ScrollViewer"/> inside a scrolling list behave as expected with the mouse wheel:
///	Shift + wheel scrolls the content sideways, and the plain wheel scrolls the surrounding list instead of being swallowed.
/// </summary>
public static class HorizontalWheelScroll
{
	public static readonly DependencyProperty IS_ENABLED_PROPERTY =
		DependencyProperty.RegisterAttached(
			"IsEnabled",
			typeof(bool),
			typeof(HorizontalWheelScroll),
			new FrameworkPropertyMetadata(false, OnIsEnabledChanged)
		);

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
	///	Turns Shift+wheel into horizontal scrolling and forwards the plain wheel to the parent list.
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

		if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) && scrollViewer.ScrollableWidth > 0)
		{
			if (e.Delta > 0)
			{
				scrollViewer.LineLeft();
			}
			else
			{
				scrollViewer.LineRight();
			}

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
}