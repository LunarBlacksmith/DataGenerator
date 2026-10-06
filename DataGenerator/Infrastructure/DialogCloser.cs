using System.Windows;

namespace DataGenerator.Infrastructure;

/// <summary>
///	Lets a view model close its dialog: binding <c>DialogResult</c> on a window to a view model property sets
///	<see cref="Window.DialogResult"/> when the property gets a value.
/// </summary>
public static class DialogCloser
{
	public static readonly DependencyProperty DIALOG_RESULT_PROPERTY =
		DependencyProperty.RegisterAttached(
			"DialogResult",
			typeof(bool?),
			typeof(DialogCloser),
			new PropertyMetadata(null, OnDialogResultChanged)
		);

	/// <summary>
	///	Gets the dialog result attached to an element.
	/// </summary>
	/// <param name="element">
	///	The element that stores the attached dialog result.
	/// </param>
	/// <returns>
	///	The result to apply to the window, or <see langword="null"/> when no result is set.
	/// </returns>
	/// <exception cref="ArgumentNullException">
	///	Thrown when <paramref name="element"/> is <see langword="null"/>.
	/// </exception>
	public static bool? GetDialogResult(DependencyObject element)
	{
		ArgumentNullException.ThrowIfNull(element);
		return (bool?)element.GetValue(DIALOG_RESULT_PROPERTY);
	}

	/// <summary>
	///	Sets the dialog result attached to an element.
	/// </summary>
	/// <param name="element">
	///	The element that stores the attached dialog result.
	/// </param>
	/// <param name="value">
	///	The result to apply to the window, or <see langword="null"/> to leave it open.
	/// </param>
	/// <exception cref="ArgumentNullException">
	///	Thrown when <paramref name="element"/> is <see langword="null"/>.
	/// </exception>
	public static void SetDialogResult(DependencyObject element, bool? value)
	{
		ArgumentNullException.ThrowIfNull(element);
		element.SetValue(DIALOG_RESULT_PROPERTY, value);
	}

	/// <summary>
	///	Copies a non-null attached value to the visible window's dialog result.
	/// </summary>
	/// <param name="element">
	///	The element whose attached value changed.
	/// </param>
	/// <param name="e">
	///	The old and new attached dialog result values.
	/// </param>
	private static void OnDialogResultChanged(DependencyObject element, DependencyPropertyChangedEventArgs e)
	{
		if (element is Window window && e.NewValue is bool result && window.IsVisible)
		{
			window.DialogResult = result;
		}
	}
}