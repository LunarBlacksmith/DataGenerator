using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace DataGenerator.Infrastructure;

/// <summary>
/// Blocks typed or pasted characters that can never be part of a valid value of the configured <see cref="TextInputKind"/>,
/// e.g. letters in a number. Range and format checks are still done by view model validation.
/// </summary>
public static class TextInputFilter
{
	public static readonly DependencyProperty KIND_PROPERTY =
		DependencyProperty.RegisterAttached(
			"Kind",
			typeof(TextInputKind),
			typeof(TextInputFilter),
			new PropertyMetadata(TextInputKind.Any, OnKindChanged)
		);

	public static TextInputKind GetKind(DependencyObject element) => (TextInputKind)element.GetValue(KIND_PROPERTY);
	public static void SetKind(DependencyObject element, TextInputKind value) => element.SetValue(KIND_PROPERTY, value);

	public static bool IsAllowed(TextInputKind kind, string text)
	{
		ArgumentNullException.ThrowIfNull(text);

		return text.All(character => IsAllowedCharacter(kind, character));
	}

	private static bool IsAllowedCharacter(TextInputKind kind, char character) => kind switch
	{
		TextInputKind.Integer     => char.IsAsciiDigit(character) || character is '-' or '+',
		TextInputKind.Decimal     => char.IsAsciiDigit(character) || character is '-' or '+' or '.' or ',' or 'e' or 'E',
		TextInputKind.Boolean     => char.IsAsciiLetterOrDigit(character),
		TextInputKind.DateTime    => char.IsAsciiLetterOrDigit(character) || character is ' ' or '-' or '/' or ':' or '.' or '+' or ',',
		TextInputKind.Time        => char.IsAsciiDigit(character) || character is ':' or '.',
		TextInputKind.Guid        => char.IsAsciiHexDigit(character) || character is '-' or '{' or '}' or '(' or ')',
		TextInputKind.Hexadecimal => char.IsAsciiHexDigit(character) || character is 'x' or 'X',
		_                         => true
	};

	private static void OnKindChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
	{
		if (sender is not TextBox textBox)
		{
			return;
		}

		textBox.PreviewTextInput -= OnPreviewTextInput;
		textBox.PreviewKeyDown   -= OnPreviewKeyDown;
		DataObject.RemovePastingHandler(textBox, OnPasting);

		if ((TextInputKind)e.NewValue != TextInputKind.Any)
		{
			textBox.PreviewTextInput += OnPreviewTextInput;
			textBox.PreviewKeyDown   += OnPreviewKeyDown;
			DataObject.AddPastingHandler(textBox, OnPasting);
		}
	}

	private static void OnPreviewTextInput(object sender, TextCompositionEventArgs e)
		=> e.Handled = !IsAllowed(GetKind((DependencyObject)sender), e.Text);

	// TextBox handles the space bar before PreviewTextInput is raised, so spaces have to be filtered here.
	private static void OnPreviewKeyDown(object sender, KeyEventArgs e)
	{
		if (e.Key == Key.Space && !IsAllowedCharacter(GetKind((DependencyObject)sender), ' '))
		{
			e.Handled = true;
		}
	}

	private static void OnPasting(object sender, DataObjectPastingEventArgs e)
	{
		string? pastedText =
			e.SourceDataObject.GetDataPresent(DataFormats.UnicodeText, true)
				? e.SourceDataObject.GetData(DataFormats.UnicodeText, true) as string
				: null;

		if (pastedText is null || !IsAllowed(GetKind((DependencyObject)sender), pastedText.Trim()))
		{
			e.CancelCommand();
		}
	}
}