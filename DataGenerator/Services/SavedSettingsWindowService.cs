using System.Windows;
using DataGenerator.Interfaces;
using DataGenerator.ViewModels;
using DataGenerator.Views;

namespace DataGenerator.Services;

public sealed class SavedSettingsWindowService : ISavedSettingsWindowService
{
	private readonly SavedSettingsLibrary _library;
	private readonly IDialogService       _dialogService;
	private readonly IFileDialogService   _fileDialogService;

	/// <summary>
	///	Creates the window service used to show saved-setting dialogs and the settings manager.
	/// </summary>
	/// <param name="library">
	///	The saved settings library used by the manager window.
	/// </param>
	/// <param name="dialogService">
	///	The dialog service used by the manager view model.
	/// </param>
	/// <param name="fileDialogService">
	///	The file dialog service used by the manager view model.
	/// </param>
	/// <exception cref="ArgumentNullException">
	///	Thrown when any dependency is <see langword="null"/>.
	/// </exception>
	public SavedSettingsWindowService(SavedSettingsLibrary library, IDialogService dialogService, IFileDialogService fileDialogService)
	{
		_library           = library ?? throw new ArgumentNullException(nameof(library));
		_dialogService     = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
		_fileDialogService = fileDialogService ?? throw new ArgumentNullException(nameof(fileDialogService));
	}

	/// <summary>
	///	Shows the modal dialog that lets the user name and save the current column settings.
	/// </summary>
	/// <param name="viewModel">
	///	The view model that backs the save dialog.
	/// </param>
	/// <returns>
	///	<see langword="true"/> when the dialog is accepted; otherwise <see langword="false"/>.
	/// </returns>
	/// <exception cref="ArgumentNullException">
	///	Thrown when <paramref name="viewModel"/> is <see langword="null"/>.
	/// </exception>
	public bool ShowSaveDialog(SaveColumnSettingViewModel viewModel)
	{
		ArgumentNullException.ThrowIfNull(viewModel);

		SaveColumnSettingWindow window = new()
		{
			DataContext = viewModel,
			Owner       = GetOwner()
		};

		return window.ShowDialog() == true;
	}

	/// <summary>
	///	Shows the modal manager window for saved settings, including import and export actions.
	/// </summary>
	public void ShowManager()
	{
		using SavedSettingsManagerViewModel viewModel = new(_library, _dialogService, _fileDialogService);

		SavedSettingsManagerWindow window = new()
		{
			DataContext = viewModel,
			Owner       = GetOwner()
		};

		_ = window.ShowDialog();
	}

	/// <summary>
	///	Finds the active application window to own a dialog, falling back to the main window.
	/// </summary>
	/// <returns>
	///	The active window, the main window or <see langword="null"/> when no application window is available.
	/// </returns>
	private static Window? GetOwner()
		=> Application.Current?.Windows.OfType<Window>().FirstOrDefault(window => window.IsActive)
			?? Application.Current?.MainWindow;
}