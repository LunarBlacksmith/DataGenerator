using System.Collections.ObjectModel;
using System.IO;
using DataGenerator.Infrastructure;
using DataGenerator.Interfaces;
using DataGenerator.Models;
using DataGenerator.Services;

namespace DataGenerator.ViewModels;

/// <summary>
///	The saved-settings manager: rename, delete, import, export and choose which settings new row sets use automatically.
/// </summary>
public sealed class SavedSettingsManagerViewModel : ObservableObject, IDisposable
{
	private const string DIALOG_TITLE = "Saved column settings";

	private readonly SavedSettingsLibrary _library;
	private readonly IDialogService       _dialogService;
	private readonly IFileDialogService   _fileDialogService;

	private SavedSettingRowViewModel? _selectedSetting;
	private string                    _statusText;
	private bool                      _isDisposed;

	/// <summary>
	///	Creates the saved-settings manager and loads the current saved settings.
	/// </summary>
	/// <param name="library">
	///	The saved settings library.
	/// </param>
	/// <param name="dialogService">
	///	The dialog service used for confirmations and errors.
	/// </param>
	/// <param name="fileDialogService">
	///	The file dialog service used to import and export settings.
	/// </param>
	/// <exception cref="ArgumentNullException">
	///	Thrown when any service argument is <see langword="null"/>.
	/// </exception>
	public SavedSettingsManagerViewModel(
		SavedSettingsLibrary library,
		IDialogService       dialogService,
		IFileDialogService   fileDialogService
	)
	{
		_library           = library ?? throw new ArgumentNullException(nameof(library));
		_dialogService     = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
		_fileDialogService = fileDialogService ?? throw new ArgumentNullException(nameof(fileDialogService));
		_statusText        = string.Empty;

		DeleteCommand      = new RelayCommand(_ => DeleteSelected(), _ => _selectedSetting is not null);
		ImportCommand      = new RelayCommand(_ => Import());
		ExportCommand      = new RelayCommand(_ => Export(), _ => Settings.Count > 0);
		ApplyToOpenCommand = new RelayCommand(_ => ApplyAutomaticSettingsToExistingRowSets(), _ => HasAutomaticSettings);

		_library.Changed += OnLibraryChanged;
		RebuildRows();
	}

	public ObservableCollection<SavedSettingRowViewModel> Settings { get; } = [];

	public RelayCommand DeleteCommand      { get; }
	public RelayCommand ImportCommand      { get; }
	public RelayCommand ExportCommand      { get; }
	public RelayCommand ApplyToOpenCommand { get; }

	public SavedSettingRowViewModel? SelectedSetting
	{
		get => _selectedSetting;
		set
		{
			if (SetProperty(ref _selectedSetting, value))
			{
				DeleteCommand.NotifyCanExecuteChanged();
			}
		}
	}

	public bool   HasSettings          => Settings.Count > 0;
	public bool   HasAutomaticSettings => _library.Settings.Any(setting => setting.ApplyAutomatically);
	public string FilePath             => _library.FilePath;

	/// <summary>
	///	The result of the last import, export or apply.
	/// </summary>
	public string StatusText
	{
		get => _statusText;
		private set => SetProperty(ref _statusText, value);
	}

	/// <summary>
	///	Stops listening for saved-settings library changes.
	/// </summary>
	public void Dispose()
	{
		if (_isDisposed)
		{
			return;
		}

		_isDisposed       = true;
		_library.Changed -= OnLibraryChanged;
	}

	/// <summary>
	///	Refreshes rows and commands after the saved settings library changes.
	/// </summary>
	/// <param name="sender">
	///	The saved settings library that changed.
	/// </param>
	/// <param name="e">
	///	The event data for the library change.
	/// </param>
	private void OnLibraryChanged(object? sender, EventArgs e)
	{
		bool isSameList =
			Settings.Count == _library.Settings.Count
			&& Settings.Select(row => row.Setting).SequenceEqual(_library.Settings);

		if (isSameList)
		{
			foreach (SavedSettingRowViewModel row in Settings)
			{
				row.Refresh();
			}
		}
		else
		{
			RebuildRows();
		}

		OnPropertyChanged(nameof(HasAutomaticSettings));
		ApplyToOpenCommand.NotifyCanExecuteChanged();
	}

	/// <summary>
	///	Rebuilds the rows from the library, keeping the previous selection by name when possible.
	/// </summary>
	private void RebuildRows()
	{
		string? selectedName = _selectedSetting?.Setting.Name;

		Settings.Clear();

		foreach (SavedColumnSetting setting in _library.Settings)
		{
			Settings.Add(new SavedSettingRowViewModel(setting, Rename, SetApplyAutomatically));
		}

		SelectedSetting = Settings.FirstOrDefault(
			row => string.Equals(row.Setting.Name, selectedName, StringComparison.OrdinalIgnoreCase)
		);

		OnPropertyChanged(nameof(HasSettings));
		ExportCommand.NotifyCanExecuteChanged();
	}

	/// <summary>
	///	Renames a saved setting if the new name is not already used.
	/// </summary>
	/// <param name="row">
	///	The row being renamed.
	/// </param>
	/// <param name="newName">
	///	The new validated name.
	/// </param>
	/// <returns>
	///	<see langword="null"/> when the rename succeeded; otherwise the validation error to show on the row.
	/// </returns>
	private string? Rename(SavedSettingRowViewModel row, string newName)
	{
		SavedColumnSetting? existing = _library.Find(newName);

		return
			existing is not null && !ReferenceEquals(existing, row.Setting)
				? $"A saved setting called '{existing.Name}' already exists."
				: TryChange(() => _library.Rename(row.Setting.Name, newName))
					? null
					: "The new name could not be saved.";
	}

	/// <summary>
	///	Saves whether a row's setting should be applied automatically.
	/// </summary>
	/// <param name="row">
	///	The row whose setting is being changed.
	/// </param>
	/// <param name="applyAutomatically">
	///	Whether the setting should be applied to new matching row sets.
	/// </param>
	private void SetApplyAutomatically(SavedSettingRowViewModel row, bool applyAutomatically)
		=> _ = TryChange(() => _library.SetApplyAutomatically(row.Setting.Name, applyAutomatically));

	/// <summary>
	///	Deletes the selected saved setting after confirmation.
	/// </summary>
	private void DeleteSelected()
	{
		if (_selectedSetting is null)
		{
			return;
		}

		string name = _selectedSetting.Setting.Name;

		bool confirmed = _dialogService.Confirm(
			DIALOG_TITLE,
			$"Delete the saved setting '{name}'?{Environment.NewLine}{Environment.NewLine}Column rules that use it keep their current values."
		);

		if (!confirmed)
		{
			return;
		}

		if (TryChange(() => _library.Remove(name)))
		{
			StatusText = $"Deleted '{name}'.";
		}
	}

	/// <summary>
	///	Imports saved column settings from a file and asks how to handle names that already exist.
	/// </summary>
	private void Import()
	{
		string? filePath = _fileDialogService.SelectSettingsFileToImport();

		if (filePath is null)
		{
			return;
		}

		IReadOnlyList<SavedColumnSetting> settings;

		try
		{
			settings = _library.ReadImportFile(filePath);
		}
		catch (Exception exception) when (IsFileProblem(exception))
		{
			_dialogService.ShowError(DIALOG_TITLE, $"The settings could not be imported.{Environment.NewLine}{Environment.NewLine}{exception.Message}");
			return;
		}

		if (settings.Count == 0)
		{
			StatusText = $"'{Path.GetFileName(filePath)}' contains no settings.";
			return;
		}

		List<string> existingNames = [.. settings.Where(setting => _library.Find(setting.Name) is not null).Select(setting => setting.Name)];
		bool         replace       = false;

		if (existingNames.Count > 0)
		{
			DialogChoice choice = _dialogService.AskYesNoCancel(
				DIALOG_TITLE,
				$"These settings already exist:{Environment.NewLine}{Environment.NewLine}{string.Join(Environment.NewLine, existingNames)}"
					+ $"{Environment.NewLine}{Environment.NewLine}Replace them with the imported settings?"
					+ $"{Environment.NewLine}{Environment.NewLine}Yes: replace them.  No: keep your settings and import only the new ones."
			);

			if (choice == DialogChoice.Cancel)
			{
				return;
			}

			replace = choice == DialogChoice.Yes;
		}

		int importedCount = 0;

		if (TryChange(() => importedCount = _library.Import(settings, replace)))
		{
			StatusText =
				importedCount == 1
					? $"Imported 1 setting from '{Path.GetFileName(filePath)}'."
					: $"Imported {importedCount} settings from '{Path.GetFileName(filePath)}'.";
		}
	}

	/// <summary>
	///	Exports every saved column setting to a user-chosen file.
	/// </summary>
	private void Export()
	{
		string? filePath = _fileDialogService.SelectSettingsExportFile();

		if (filePath is null)
		{
			return;
		}

		try
		{
			_library.Export(filePath, _library.Settings);
			StatusText = $"Exported {_library.Settings.Count} setting(s) to '{filePath}'.";
		}
		catch (Exception exception) when (IsFileProblem(exception))
		{
			_dialogService.ShowError(DIALOG_TITLE, $"The settings could not be exported.{Environment.NewLine}{Environment.NewLine}{exception.Message}");
		}
	}

	/// <summary>
	///	Applies automatic settings to the row sets that are already open after confirmation.
	/// </summary>
	private void ApplyAutomaticSettingsToExistingRowSets()
	{
		bool confirmed = _dialogService.Confirm(
			DIALOG_TITLE,
			"Apply the automatic settings to the row sets that already exist?"
				+ $"{Environment.NewLine}{Environment.NewLine}The current settings of every matching column are replaced."
		);

		if (!confirmed)
		{
			return;
		}

		int updatedCount = _library.ApplyAutomaticSettingsToExistingRowSets();

		StatusText =
			updatedCount == 1
				? "Updated 1 column."
				: $"Updated {updatedCount} columns.";
	}

	/// <summary>
	///	Runs a saved-settings library change and reports problems to the user.
	/// </summary>
	/// <param name="change">
	///	The library operation to run.
	/// </param>
	/// <returns>
	///	<see langword="true"/> when the operation succeeded; otherwise <see langword="false"/>.
	/// </returns>
	private bool TryChange(Action change)
	{
		try
		{
			change();
			return true;
		}
		catch (Exception exception) when (IsFileProblem(exception) || exception is InvalidOperationException or ArgumentException)
		{
			_dialogService.ShowError(DIALOG_TITLE, $"The saved settings could not be changed.{Environment.NewLine}{Environment.NewLine}{exception.Message}");
			return false;
		}
	}

	/// <summary>
	///	Whether an exception means the saved-settings file could not be read or written.
	/// </summary>
	/// <param name="exception">
	///	The exception to classify.
	/// </param>
	/// <returns>
	///	<see langword="true"/> when the exception is a file or data problem; otherwise <see langword="false"/>.
	/// </returns>
	private static bool IsFileProblem(Exception exception)
		=> exception is IOException or UnauthorizedAccessException or InvalidDataException;
}