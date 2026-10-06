using DataGenerator.Infrastructure;

namespace DataGenerator.ViewModels;

/// <summary>
/// An optional column of the column rules grid that the user can hide.
/// </summary>
public sealed class RuleGridColumnOption : ObservableObject
{
	private readonly Action<RuleGridColumnOption> _onVisibilityChanged;

	private bool _isVisible;

	public RuleGridColumnOption(
		string                       key,
		string                       displayName,
		string                       description,
		bool                         isVisible,
		Action<RuleGridColumnOption> onVisibilityChanged
	)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(key);
		ArgumentException.ThrowIfNullOrWhiteSpace(displayName);

		Key                  = key;
		DisplayName          = displayName;
		Description          = description ?? string.Empty;
		_isVisible           = isVisible;
		_onVisibilityChanged = onVisibilityChanged ?? throw new ArgumentNullException(nameof(onVisibilityChanged));
	}

	/// <summary>
	/// The name under which the choice is remembered in the user's preferences.
	/// </summary>
	public string Key { get; }

	public string DisplayName { get; }

	public string Description { get; }

	public bool IsVisible
	{
		get => _isVisible;
		set
		{
			if (SetProperty(ref _isVisible, value))
			{
				_onVisibilityChanged(this);
			}
		}
	}
}