using System.Windows;
using DataGenerator.ViewModels;
using DataGenerator.Views;

namespace DataGenerator.Services;

public sealed class SavedSettingsWindowService : ISavedSettingsWindowService
{
	private readonly SavedSettingsLibrary _library;
	private readonly IDialogService       _dialogService;
	private readonly IFileDialogService   _fileDialogService;

	public SavedSettingsWindowService(SavedSettingsLibrary library, IDialogService dialogService, IFileDialogService fileDialogService)
	{
		_library           = library ?? throw new ArgumentNullException(nameof(library));
		_dialogService     = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
		_fileDialogService = fileDialogService ?? throw new ArgumentNullException(nameof(fileDialogService));
	}

	public bool ShowSaveDialog(SaveColumnSettingViewModel viewModel)
	{
		ArgumentNullException.ThrowIfNull(viewModel);

		SaveColumnSettingWindow window = new SaveColumnSettingWindow
		{
			DataContext = viewModel,
			Owner       = GetOwner()
		};

		return window.ShowDialog() == true;
	}

	public void ShowManager()
	{
		using SavedSettingsManagerViewModel viewModel = new SavedSettingsManagerViewModel(_library, _dialogService, _fileDialogService);

		SavedSettingsManagerWindow window = new SavedSettingsManagerWindow
		{
			DataContext = viewModel,
			Owner       = GetOwner()
		};

		_ = window.ShowDialog();
	}

	private static Window? GetOwner()
		=> Application.Current?.Windows.OfType<Window>().FirstOrDefault(window => window.IsActive)
			?? Application.Current?.MainWindow;
}