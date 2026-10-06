namespace DataGenerator.ViewModels;

/// <summary>
/// One entry of a drop-down that chooses a value, with the text and explanation shown to the user.
/// </summary>
public sealed class ChoiceOption<T>
{
	public ChoiceOption(T value, string displayName, string description)
	{
		Value       = value;
		DisplayName = displayName ?? throw new ArgumentNullException(nameof(displayName));
		Description = description ?? throw new ArgumentNullException(nameof(description));
	}

	public T      Value       { get; }
	public string DisplayName { get; }
	public string Description { get; }

	public override string ToString() => DisplayName;
}
