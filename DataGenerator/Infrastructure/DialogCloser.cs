using System.Windows;

namespace DataGenerator.Infrastructure;

/// <summary>
/// Lets a view model close its dialog: binding <c>DialogResult</c> on a window to a view model property sets
/// <see cref="Window.DialogResult"/> when the property gets a value.
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

	public static bool? GetDialogResult(DependencyObject element)
	{
		ArgumentNullException.ThrowIfNull(element);
		return (bool?)element.GetValue(DIALOG_RESULT_PROPERTY);
	}

	public static void SetDialogResult(DependencyObject element, bool? value)
	{
		ArgumentNullException.ThrowIfNull(element);
		element.SetValue(DIALOG_RESULT_PROPERTY, value);
	}

	private static void OnDialogResultChanged(DependencyObject element, DependencyPropertyChangedEventArgs e)
	{
		if (element is Window window && e.NewValue is bool result && window.IsVisible)
		{
			window.DialogResult = result;
		}
	}
}