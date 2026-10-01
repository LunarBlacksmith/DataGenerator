using System.Windows;

namespace DataGenerator.Infrastructure;

/// <summary>
/// Attaches an icon to a button: a character of the Segoe Fluent Icons (or Segoe MDL2 Assets) font. The button templates in
/// Themes\Controls.xaml show the icon in front of the button content.
/// </summary>
public static class ButtonIcon
{
	public static readonly DependencyProperty GLYPH_PROPERTY =
		DependencyProperty.RegisterAttached(
			"Glyph",
			typeof(string),
			typeof(ButtonIcon),
			new FrameworkPropertyMetadata(null)
		);

	public static string? GetGlyph(DependencyObject element) => (string?)element.GetValue(GLYPH_PROPERTY);
	public static void SetGlyph(DependencyObject element, string? value) => element.SetValue(GLYPH_PROPERTY, value);
}