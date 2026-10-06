using DataGenerator.ViewModels;

namespace DataGenerator.Interfaces;

/// <summary>
/// Opens the dialogs for saving and managing saved column settings.
/// </summary>
public interface ISavedSettingsWindowService
{
	/// <summary>
	/// Shows the "Save column settings" dialog; returns <see langword="true"/> when the setting was saved.
	/// </summary>
	bool ShowSaveDialog(SaveColumnSettingViewModel viewModel);

	void ShowManager();
}