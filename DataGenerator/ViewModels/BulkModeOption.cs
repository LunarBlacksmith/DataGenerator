using DataGenerator.Models;

namespace DataGenerator.ViewModels;

/// <summary>
///	A generation mode that can be given to several selected columns at once, with how many of them can use it.
/// </summary>
public sealed class BulkModeOption
{
	/// <summary>
	///	Creates a bulk mode choice and records how many selected columns can use it.
	/// </summary>
	/// <param name="option">
	///	The generation mode option shown to the user.
	/// </param>
	/// <param name="supportedCount">
	///	How many selected columns can use <paramref name="option"/>.
	/// </param>
	/// <param name="selectedCount">
	///	How many columns are currently selected.
	/// </param>
	/// <exception cref="ArgumentNullException">
	///	Thrown when <paramref name="option"/> is <see langword="null"/>.
	/// </exception>
	public BulkModeOption(GenerationModeOption option, int supportedCount, int selectedCount)
	{
		Option         = option ?? throw new ArgumentNullException(nameof(option));
		SupportedCount = supportedCount;
		SelectedCount  = selectedCount;
	}

	public GenerationModeOption Option         { get; }
	public int                  SupportedCount { get; }
	public int                  SelectedCount  { get; }

	public ValueGenerationMode Mode => Option.Mode;

	public bool IsSupportedByAll => SupportedCount == SelectedCount;

	public string DisplayName
		=>
			IsSupportedByAll
				? Option.DisplayName
				: $"{Option.DisplayName} ({SupportedCount:N0} of {SelectedCount:N0})";

	public string Description
		=>
			IsSupportedByAll
				? Option.Description
				: $"{Option.Description} Only {SupportedCount:N0} of the {SelectedCount:N0} selected columns can use it; the others keep their mode.";

	/// <summary>
	///	Returns the text shown for this bulk mode choice.
	/// </summary>
	/// <returns>
	///	The display name, including the supported count when not every selected column can use the mode.
	/// </returns>
	public override string ToString() => DisplayName;
}