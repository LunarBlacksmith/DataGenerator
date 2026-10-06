using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace DataGenerator.Infrastructure;

/// <summary>
///	Shows an element when the bound value is <see langword="false"/> and collapses it when the value is <see langword="true"/>.
/// </summary>
public sealed class InverseBooleanToVisibilityConverter : IValueConverter
{
	#region CONSTRUCTORS
	#region PUBLIC
	/// <summary>
	///	Creates a new <see cref="InverseBooleanToVisibilityConverter"/>.
	/// </summary>
	public InverseBooleanToVisibilityConverter()
	{
	}
	#endregion PUBLIC
	#endregion CONSTRUCTORS

	#region METHODS
	#region PUBLIC
	/// <summary>
	///	Converts a boolean value to the opposite visibility state.
	/// </summary>
	/// <param name="value">
	///	The bound value to convert.
	/// </param>
	/// <param name="targetType">
	///	The target binding type requested by WPF.
	/// </param>
	/// <param name="parameter">
	///	An optional converter parameter, which is not used.
	/// </param>
	/// <param name="culture">
	///	The culture supplied by the binding engine.
	/// </param>
	/// <returns>
	///	<see cref="Visibility.Collapsed"/> when <paramref name="value"/> is <see langword="true"/>; otherwise
	///	<see cref="Visibility.Visible"/>.
	/// </returns>
	public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
		=> value is true ? Visibility.Collapsed : Visibility.Visible;

	/// <summary>
	///	Converts visibility back to the boolean value represented by this inverse converter.
	/// </summary>
	/// <param name="value">
	///	The visibility value to convert.
	/// </param>
	/// <param name="targetType">
	///	The target binding type requested by WPF.
	/// </param>
	/// <param name="parameter">
	///	An optional converter parameter, which is not used.
	/// </param>
	/// <param name="culture">
	///	The culture supplied by the binding engine.
	/// </param>
	/// <returns>
	///	<see langword="true"/> when <paramref name="value"/> is not visible, <see langword="false"/> when it is visible,
	///	or <see cref="Binding.DoNothing"/> when the value is not a <see cref="Visibility"/>.
	/// </returns>
	public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
		=> value is Visibility visibility ? visibility != Visibility.Visible : Binding.DoNothing;
	#endregion PUBLIC
	#endregion METHODS
}