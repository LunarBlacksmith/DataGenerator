using System.Windows;
using System.Windows.Controls;
using DataGenerator.Interfaces;

namespace DataGenerator.Infrastructure;

/// <summary>
///	Suggests pattern language functions and keywords while a pattern is typed into a <see cref="TextBox"/>, e.g. typing
///	RAND suggests RAND_NUM, RAND_DATE and so on. The suggestions appear under the word without taking the focus;
///	↑ and ↓ choose one and pressing Tab twice inserts it. Set <c>Provider</c> to turn the suggestions on.
/// </summary>
public static class PatternAutoComplete
{
	#region FIELDS
	#region PUBLIC
	public static readonly DependencyProperty PROVIDER_PROPERTY;
	#endregion PUBLIC

	#region PRIVATE
	private static readonly DependencyProperty CONTROLLER_PROPERTY;
	#endregion PRIVATE
	#endregion FIELDS

	#region CONSTRUCTORS
	#region STATIC
	/// <summary>
	///	Sets the default values of the static fields and properties of <see cref="PatternAutoComplete"/>.
	/// </summary>
	static PatternAutoComplete()
	{
		PROVIDER_PROPERTY =
			DependencyProperty.RegisterAttached(
				"Provider",
				typeof(IPatternCompletionProvider),
				typeof(PatternAutoComplete),
				new FrameworkPropertyMetadata(null, OnProviderChanged)
			);


		CONTROLLER_PROPERTY =
			DependencyProperty.RegisterAttached(
				"Controller",
				typeof(PatternCompletionController),
				typeof(PatternAutoComplete),
				new FrameworkPropertyMetadata(null)
			);
	}
	#endregion STATIC
	#endregion CONSTRUCTORS

	#region METHODS
	#region PUBLIC
	/// <summary>
	///	Gets the completion provider attached to a text box.
	/// </summary>
	/// <param name="element">
	///	The element that stores the attached provider.
	/// </param>
	/// <returns>
	///	The provider that supplies suggestions, or <see langword="null"/> when suggestions are disabled.
	/// </returns>
	public static IPatternCompletionProvider? GetProvider(DependencyObject element)
		=> (IPatternCompletionProvider?)element.GetValue(PROVIDER_PROPERTY);

	/// <summary>
	///	Sets the completion provider attached to a text box.
	/// </summary>
	/// <param name="element">
	///	The element that stores the attached provider.
	/// </param>
	/// <param name="value">
	///	The provider that supplies suggestions, or <see langword="null"/> to disable suggestions.
	/// </param>
	public static void SetProvider(DependencyObject element, IPatternCompletionProvider? value)
		=> element.SetValue(PROVIDER_PROPERTY, value);

	/// <summary>
	///	Whether suggestions are shown under the text box, so other keyboard handlers leave ↑, ↓ and Tab to them.
	/// </summary>
	/// <param name="element">
	///	The text box to inspect.
	/// </param>
	/// <returns>
	///	<see langword="true"/> when the suggestion pop-up is open; otherwise <see langword="false"/>.
	/// </returns>
	public static bool IsSuggesting(DependencyObject element)
		=> element.GetValue(CONTROLLER_PROPERTY) is PatternCompletionController { IsOpen: true };
	#endregion PUBLIC

	#region PRIVATE
	/// <summary>
	///	Creates, replaces or removes the controller when a text box's provider changes.
	/// </summary>
	/// <param name="element">
	///	The element whose attached provider changed.
	/// </param>
	/// <param name="e">
	///	The old and new provider values.
	/// </param>
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
	#endregion PRIVATE
	#endregion METHODS
}