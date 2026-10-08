using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace DataGenerator.Infrastructure;

/// <summary>
///	Blocks typed or pasted characters that can never be part of a valid value of the configured <see cref="TextInputKind"/>,
///	e.g. letters in a number. Range and format checks are still done by view model validation.
/// </summary>
public static class TextInputFilter
{
	#region FIELDS
	public static readonly DependencyProperty KIND_PROPERTY;
	#endregion FIELDS

	#region CONSTRUCTOR
	/// <summary>
	///	Sets the default values of the static fields and properties of <see cref="TextInputFilter"/>.
	/// </summary>
	static TextInputFilter()
	{
		KIND_PROPERTY =
			DependencyProperty.RegisterAttached(
				"Kind",
				typeof(TextInputKind),
				typeof(TextInputFilter),
				new PropertyMetadata(TextInputKind.Any, OnKindChanged)
			);
	}
	#endregion CONSTRUCTOR

	#region METHODS
	#region PUBLIC
	/// <summary>
	///	Gets the kind of value allowed by a text box.
	/// </summary>
	/// <param name="element">
	///	The element that stores the attached input kind.
	/// </param>
	/// <returns>
	///	The configured input kind.
	/// </returns>
	public static TextInputKind GetKind(DependencyObject element) => (TextInputKind)element.GetValue(KIND_PROPERTY);

	/// <summary>
	///	Sets the kind of value allowed by a text box.
	/// </summary>
	/// <param name="element">
	///	The element that stores the attached input kind.
	/// </param>
	/// <param name="value">
	///	The input kind whose impossible characters should be blocked.
	/// </param>
	public static void SetKind(DependencyObject element, TextInputKind value) => element.SetValue(KIND_PROPERTY, value);

	/// <summary>
	///	Checks whether every character in some text can appear in a value of the given kind.
	/// </summary>
	/// <param name="kind">
	///	The kind of value the text box accepts.
	/// </param>
	/// <param name="text">
	///	The text to test.
	/// </param>
	/// <returns>
	///	<see langword="true"/> when every character is allowed; otherwise <see langword="false"/>.
	/// </returns>
	/// <exception cref="ArgumentNullException">
	///	Thrown when <paramref name="text"/> is <see langword="null"/>.
	/// </exception>
	public static bool IsAllowed(TextInputKind kind, string text)
	{
		ArgumentNullException.ThrowIfNull(text);

		return text.All(character => IsAllowedCharacter(kind, character));
	}
	#endregion PUBLIC

	#region PRIVATE
	/// <summary>
	///	Checks whether one character can appear in a value of the given kind.
	/// </summary>
	/// <param name="kind">
	///	The kind of value the text box accepts.
	/// </param>
	/// <param name="character">
	///	The character to test.
	/// </param>
	/// <returns>
	///	<see langword="true"/> when the character is allowed; otherwise <see langword="false"/>.
	/// </returns>
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

	/// <summary>
	///	Adds or removes input filtering handlers when a text box's input kind changes.
	/// </summary>
	/// <param name="sender">
	///	The element whose attached input kind changed.
	/// </param>
	/// <param name="e">
	///	The old and new input kind values.
	/// </param>
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

	/// <summary>
	///	Blocks typed text that contains characters outside the configured input kind.
	/// </summary>
	/// <param name="sender">
	///	The text box receiving text input.
	/// </param>
	/// <param name="e">
	///	The text composition event raised by WPF.
	/// </param>
	private static void OnPreviewTextInput(object sender, TextCompositionEventArgs e)
		=> e.Handled = !IsAllowed(GetKind((DependencyObject)sender), e.Text);

	// TextBox handles the space bar before PreviewTextInput is raised, so spaces have to be filtered here.
	/// <summary>
	///	Blocks the space key when the configured input kind does not allow spaces.
	/// </summary>
	/// <param name="sender">
	///	The text box receiving the key event.
	/// </param>
	/// <param name="e">
	///	The preview key event raised by WPF.
	/// </param>
	private static void OnPreviewKeyDown(object sender, KeyEventArgs e)
	{
		if (e.Key == Key.Space && !IsAllowedCharacter(GetKind((DependencyObject)sender), ' '))
		{
			e.Handled = true;
		}
	}

	/// <summary>
	///	Cancels a paste when the pasted text contains characters outside the configured input kind.
	/// </summary>
	/// <param name="sender">
	///	The text box receiving the paste command.
	/// </param>
	/// <param name="e">
	///	The paste event supplied by WPF.
	/// </param>
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
	#endregion PRIVATE
	#endregion METHODS
}