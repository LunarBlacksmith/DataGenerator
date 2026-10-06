namespace DataGenerator.ViewModels;

/// <summary>
///	One entry of a drop-down that chooses a value, with the text and explanation shown to the user.
/// </summary>
public sealed class ChoiceOption<T>
{
	/// <summary>
	///	Creates a choice for a drop-down list.
	/// </summary>
	/// <param name="value">
	///	The value chosen when this option is selected.
	/// </param>
	/// <param name="displayName">
	///	The short text shown in the closed drop-down.
	/// </param>
	/// <param name="description">
	///	The longer explanation shown beside or under the choice.
	/// </param>
	/// <exception cref="ArgumentNullException">
	///	Thrown when <paramref name="displayName"/> or <paramref name="description"/> is <see langword="null"/>.
	/// </exception>
	public ChoiceOption(T value, string displayName, string description)
	{
		Value       = value;
		DisplayName = displayName ?? throw new ArgumentNullException(nameof(displayName));
		Description = description ?? throw new ArgumentNullException(nameof(description));
	}

	public T      Value       { get; }
	public string DisplayName { get; }
	public string Description { get; }

	/// <summary>
	///	Returns the text shown for this choice.
	/// </summary>
	/// <returns>
	///	The display name.
	/// </returns>
	public override string ToString() => DisplayName;
}
