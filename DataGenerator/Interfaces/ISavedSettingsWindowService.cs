using DataGenerator.ViewModels;

namespace DataGenerator.Interfaces;

/// <summary>
///	Opens the dialogs for saving and managing saved column settings.
/// </summary>
public interface ISavedSettingsWindowService
{
	/// <summary>
	///	Shows the "Save column settings" dialog; returns <see langword="true"/> when the setting was saved.
	/// </summary>
	/// <param name="viewModel">
	///	The view model that supplies the setting fields and performs the save command.
	/// </param>
	/// <returns>
	///	<see langword="true"/> when the dialog closes with a saved setting; otherwise <see langword="false"/>.
	/// </returns>
	/// <exception cref="ArgumentNullException">
	///	Thrown when <paramref name="viewModel"/> is <see langword="null"/>.
	/// </exception>
	bool ShowSaveDialog(SaveColumnSettingViewModel viewModel);

	/// <summary>
	///	Shows the saved-settings manager dialog.
	/// </summary>
	void ShowManager();
}