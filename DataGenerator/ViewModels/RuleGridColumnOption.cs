using DataGenerator.Infrastructure;

namespace DataGenerator.ViewModels;

/// <summary>
///	An optional column of the column rules grid that the user can hide.
/// </summary>
public sealed class RuleGridColumnOption : ObservableObject
{
	#region FIELDS
	private readonly Action<RuleGridColumnOption> _onVisibilityChanged;

	private bool _isVisible;
	#endregion FIELDS

	#region PROPERTIES
	/// <summary>
	///	The name under which the choice is remembered in the user's preferences.
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
	#endregion PROPERTIES

	/// <summary>
	///	Creates a rule-grid column choice and records how visibility changes are saved.
	/// </summary>
	/// <param name="key">
	///	The preference key used to remember the column.
	/// </param>
	/// <param name="displayName">
	///	The label shown in the columns menu.
	/// </param>
	/// <param name="description">
	///	The explanation shown for the column.
	/// </param>
	/// <param name="isVisible">
	///	Whether the column is currently shown.
	/// </param>
	/// <param name="onVisibilityChanged">
	///	The callback invoked after the visibility changes.
	/// </param>
	/// <exception cref="ArgumentException">
	///	Thrown when <paramref name="key"/> or <paramref name="displayName"/> is <see langword="null"/>, empty or white
	///	space.
	/// </exception>
	/// <exception cref="ArgumentNullException">
	///	Thrown when <paramref name="onVisibilityChanged"/> is <see langword="null"/>.
	/// </exception>
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
}