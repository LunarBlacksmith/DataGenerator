using System.Windows;

namespace DataGenerator.Infrastructure;

/// <summary>
///	Attaches an icon to a button: a character of the Segoe Fluent Icons (or Segoe MDL2 Assets) font. The button templates in
///	Themes\Controls.xaml show the icon in front of the button content.
/// </summary>
public static class ButtonIcon
{
	#region FIELDS
	#region PUBLIC
	public static readonly DependencyProperty GLYPH_PROPERTY;
	#endregion PUBLIC
	#endregion FIELDS

	#region CONSTRUCTORS
	#region STATIC
	/// <summary>
	///	Sets the default values of the static fields and properties of <see cref="ButtonIcon"/>.
	/// </summary>
	static ButtonIcon()
	{
		GLYPH_PROPERTY =
			DependencyProperty.RegisterAttached(
				"Glyph",
				typeof(string),
				typeof(ButtonIcon),
				new FrameworkPropertyMetadata(null)
			);
	}
	#endregion STATIC
	#endregion CONSTRUCTORS

	#region METHODS
	#region PUBLIC
	/// <summary>
	///	Gets the icon glyph attached to a button or other dependency object.
	/// </summary>
	/// <param name="element">
	///	The element that stores the attached glyph.
	/// </param>
	/// <returns>
	///	The glyph string, or <see langword="null"/> when none is set.
	/// </returns>
	public static string? GetGlyph(DependencyObject element) => (string?)element.GetValue(GLYPH_PROPERTY);

	/// <summary>
	///	Sets the icon glyph attached to a button or other dependency object.
	/// </summary>
	/// <param name="element">
	///	The element that stores the attached glyph.
	/// </param>
	/// <param name="value">
	///	The glyph string to show, or <see langword="null"/> to clear the value.
	/// </param>
	public static void SetGlyph(DependencyObject element, string? value) => element.SetValue(GLYPH_PROPERTY, value);
	#endregion PUBLIC
	#endregion METHODS
}