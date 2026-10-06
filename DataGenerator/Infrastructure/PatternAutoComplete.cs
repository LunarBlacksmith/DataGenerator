using System.Windows;
using System.Windows.Controls;
using DataGenerator.Interfaces;

namespace DataGenerator.Infrastructure;

/// <summary>
/// Suggests pattern language functions and keywords while a pattern is typed into a <see cref="TextBox"/>, e.g. typing
/// RAND suggests RAND_NUM, RAND_DATE and so on. The suggestions appear under the word without taking the focus;
/// ↑ and ↓ choose one and pressing Tab twice inserts it. Set <c>Provider</c> to turn the suggestions on.
/// </summary>
public static class PatternAutoComplete
{
	public static readonly DependencyProperty PROVIDER_PROPERTY =
		DependencyProperty.RegisterAttached(
			"Provider",
			typeof(IPatternCompletionProvider),
			typeof(PatternAutoComplete),
			new FrameworkPropertyMetadata(null, OnProviderChanged)
		);

	private static readonly DependencyProperty CONTROLLER_PROPERTY =
		DependencyProperty.RegisterAttached(
			"Controller",
			typeof(PatternCompletionController),
			typeof(PatternAutoComplete),
			new FrameworkPropertyMetadata(null)
		);

	public static IPatternCompletionProvider? GetProvider(DependencyObject element)
		=> (IPatternCompletionProvider?)element.GetValue(PROVIDER_PROPERTY);

	public static void SetProvider(DependencyObject element, IPatternCompletionProvider? value)
		=> element.SetValue(PROVIDER_PROPERTY, value);

	/// <summary>
	/// Whether suggestions are shown under the text box, so other keyboard handlers leave ↑, ↓ and Tab to them.
	/// </summary>
	public static bool IsSuggesting(DependencyObject element)
		=> element.GetValue(CONTROLLER_PROPERTY) is PatternCompletionController { IsOpen: true };

	private static void OnProviderChanged(DependencyObject element, DependencyPropertyChangedEventArgs e)
	{
		if (element is not TextBox textBox)
		{
			return;
		}

		if (textBox.GetValue(CONTROLLER_PROPERTY) is PatternCompletionController previous)
		{
			previous.Detach();
			textBox.ClearValue(CONTROLLER_PROPERTY);
		}

		if (e.NewValue is IPatternCompletionProvider provider)
		{
			textBox.SetValue(CONTROLLER_PROPERTY, new PatternCompletionController(textBox, provider));
		}
	}
}