using System.Collections.ObjectModel;
using System.IO;
using DataGenerator.Infrastructure;
using DataGenerator.Interfaces;
using DataGenerator.Models;
using DataGenerator.Services;

namespace DataGenerator.ViewModels;

/// <summary>
/// The "Set configuration" menu of the row set being edited: saves the modes and settings of all its columns under a
/// name, loads a saved configuration into it, and imports, exports or deletes configurations. Saved column settings,
/// which each hold one column, are handled by the menu of each column and are never mixed with these.
/// </summary>
public sealed class RowSetConfigurationsViewModel : ObservableObject
{
	private const string DIALOG_TITLE        = "Set configurations";
	private const string NAME_SEPARATOR      = " – ";
	private const int    MAXIMUM_NAMED_SKIPS = 3;

	private readonly RowSetConfigurationLibrary _library;
	private readonly IDialogService             _dialogService;
	private readonly IFileDialogService         _fileDialogService;

	private TableNodeViewModel? _table;
	private bool                _isMenuOpen;
	private string              _newName;
	private string?             _nameError;

	public RowSetConfigurationsViewModel(
		RowSetConfigurationLibrary library,
		IDialogService             dialogService,
		IFileDialogService         fileDialogService
	)
	{
		_library           = library ?? throw new ArgumentNullException(nameof(library));
		_dialogService     = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
		_fileDialogService = fileDialogService ?? throw new ArgumentNullException(nameof(fileDialogService));
		_table             = null;
		_isMenuOpen        = false;
		_newName           = string.Empty;
		_nameError         = null;

		SaveCommand   = new RelayCommand(_ => Save(), _ => RowSet is not null && _nameError is null);
		LoadCommand   = new RelayCommand(parameter => Load(parameter as RowSetConfigurationOption), parameter => RowSet is not null && parameter is RowSetConfigurationOption);
		DeleteCommand = new RelayCommand(parameter => Delete(parameter as RowSetConfigurationOption), parameter => parameter is RowSetConfigurationOption);
		ImportCommand = new RelayCommand(_ => Import());
		ExportCommand = new RelayCommand(_ => Export(), _ => _library.Configurations.Count > 0);

		_library.Changed += OnLibraryChanged;
	}

	public RelayCommand SaveCommand   { get; }
	public RelayCommand LoadCommand   { get; }
	public RelayCommand DeleteCommand { get; }
	public RelayCommand ImportCommand { get; }
	public RelayCommand ExportCommand { get; }

	/// <summary>
	/// The saved configurations that share at least one column with the row set; the ones saved from its table come first.
	/// </summary>
	public ObservableCollection<RowSetConfigurationOption> Options { get; } = [];

	public bool HasOptions => Options.Count > 0;

	/// <summary>
	/// How many saved configurations are not offered because none of their columns are in this table.
	/// </summary>
	public int OtherTableCount => _library.Configurations.Count - Options.Count;

	public bool HasOtherTables => OtherTableCount > 0;

	public string OtherTablesText
		=> OtherTableCount == 1
			? "1 more configuration has no columns of this table."
			: $"{OtherTableCount} more configurations have no columns of this table.";

	public string MenuTitle => RowSet is null ? DIALOG_TITLE : $"Set configuration · {RowSet.Name.Trim()}";

	/// <summary>
	/// Whether the menu is open. Opening it lists the configurations that suit the row set and suggests a name.
	/// </summary>
	public bool IsMenuOpen
	{
		get => _isMenuOpen;
		set
		{
			if (value && !_isMenuOpen)
			{
				PrepareMenu();
			}

			_ = SetProperty(ref _isMenuOpen, value);
		}
	}

	/// <summary>
	/// The name to save the configuration of the row set under.
	/// </summary>
	public string NewName
	{
		get => _newName;
		set
		{
			if (SetProperty(ref _newName, value ?? string.Empty))
			{
				NameError = RowSetConfigurationLibrary.ValidateName(_newName);
			}
		}
	}

	public string? NameError
	{
		get => _nameError;
		private set
		{
			if (SetProperty(ref _nameError, value))
			{
				OnPropertyChanged(nameof(HasNameError));
				SaveCommand.NotifyCanExecuteChanged();
			}
		}
	}

	public bool HasNameError => _nameError is not null;

	private RowSetViewModel? RowSet => _table?.SelectedRowSet;

	private string TableName => _table?.Model.DisplayName ?? string.Empty;

	/// <summary>
	/// Makes the menu work on the row set shown for a table (the one selected in its tabs).
	/// </summary>
	public void SetTable(TableNodeViewModel? table)
	{
		_table     = table;
		IsMenuOpen = false;
		RefreshCommands();
	}

	/// <summary>
	/// Called when the row set of the table changes, e.g. when another tab is selected.
	/// </summary>
	public void RefreshCommands()
	{
		SaveCommand.NotifyCanExecuteChanged();
		LoadCommand.NotifyCanExecuteChanged();
		OnPropertyChanged(nameof(MenuTitle));
	}

	private void PrepareMenu()
	{
		RefreshOptions();
		NewName = RowSet is null ? string.Empty : $"{TableName}{NAME_SEPARATOR}{RowSet.Name.Trim()}";
		NameError = RowSetConfigurationLibrary.ValidateName(_newName);
		OnPropertyChanged(nameof(MenuTitle));
	}

	private void RefreshOptions()
	{
		Options.Clear();

		RowSetViewModel? rowSet = RowSet;

		if (rowSet is not null)
		{
			IEnumerable<SavedRowSetConfiguration> configurations = _library.Configurations
				.OrderBy(configuration => configuration.IsFromTable(TableName) ? 0 : 1)
				.ThenBy(configuration => configuration.Name, StringComparer.CurrentCultureIgnoreCase);

			foreach (SavedRowSetConfiguration configuration in configurations)
			{
				int matchingColumnCount = rowSet.CountMatchingColumns(configuration);

				if (matchingColumnCount > 0)
				{
					Options.Add(new RowSetConfigurationOption(configuration, matchingColumnCount, configuration.IsFromTable(TableName)));
				}
			}
		}

		OnPropertyChanged(nameof(HasOptions));
		OnPropertyChanged(nameof(OtherTableCount));
		OnPropertyChanged(nameof(HasOtherTables));
		OnPropertyChanged(nameof(OtherTablesText));
	}

	private void Save()
	{
		RowSetViewModel? rowSet = RowSet;

		if (rowSet is null || _nameError is not null)
		{
			return;
		}

		string                    name     = _newName.Trim();
		SavedRowSetConfiguration? existing = _library.Find(name);

		IsMenuOpen = false;

		if (existing is not null
			&& !_dialogService.Confirm(
				DIALOG_TITLE,
				$"A set configuration called '{existing.Name}' already exists (saved from {existing.TableName}).{Environment.NewLine}{Environment.NewLine}"
					+ $"Replace it with the column settings of '{rowSet.Name.Trim()}'?"
			))
		{
			return;
		}

		if (rowSet.HasInvalidRules
			&& !_dialogService.Confirm(
				DIALOG_TITLE,
				$"{rowSet.ValidationSummary} in '{rowSet.Name.Trim()}', and the configuration would keep those settings as they are.{Environment.NewLine}{Environment.NewLine}"
					+ "Save it anyway?"
			))
		{
			return;
		}

		SavedRowSetConfiguration configuration = rowSet.CaptureConfiguration(name, TableName);

		if (TryChange(() => _library.Save(configuration), "saved"))
		{
			rowSet.ConfigurationResult = new OperationResultText(
				$"Saved the settings of all {configuration.Columns.Count} columns as the set configuration '{name}'.",
				null,
				false
			);
		}
	}

	private void Load(RowSetConfigurationOption? option)
	{
		RowSetViewModel? rowSet = RowSet;

		if (rowSet is null || option is null)
		{
			return;
		}

		SavedRowSetConfiguration configuration = option.Configuration;
		int                      columnCount   = rowSet.ConfigurableRules.Count();
		int                      keptCount     = columnCount - option.MatchingColumnCount;
		string                   scope         = keptCount == 0
			? $"all {columnCount} of its columns"
			: $"{option.MatchingColumnCount} of its {columnCount} columns";

		IsMenuOpen = false;

		string message = $"Load the set configuration '{configuration.Name}' into the row set '{rowSet.Name.Trim()}' of {TableName}?"
			+ $"{Environment.NewLine}{Environment.NewLine}This replaces the generation mode and settings of {scope}."
			+ (keptCount > 0 ? $" The other {keptCount} keep their settings." : string.Empty)
			+ (option.IsFromActiveTable
				? string.Empty
				: $"{Environment.NewLine}{Environment.NewLine}It was saved from {configuration.TableName}; only columns with the same names are changed.");

		if (!_dialogService.Confirm(DIALOG_TITLE, message))
		{
			return;
		}

		rowSet.ConfigurationResult = Describe(configuration.Name, rowSet.ApplyConfiguration(configuration));
	}

	private void Delete(RowSetConfigurationOption? option)
	{
		if (option is null)
		{
			return;
		}

		bool confirmed = _dialogService.Confirm(
			DIALOG_TITLE,
			$"Delete the set configuration '{option.Name}'?{Environment.NewLine}{Environment.NewLine}Row sets that loaded it keep their settings."
		);

		if (confirmed && TryChange(() => _library.Remove(option.Name), "deleted"))
		{
			ReportResult(new OperationResultText($"Deleted the set configuration '{option.Name}'.", null, false));
		}
	}

	private void Import()
	{
		IsMenuOpen = false;

		string? filePath = _fileDialogService.SelectSetConfigurationsFileToImport();

		if (filePath is null)
		{
			return;
		}

		IReadOnlyList<SavedRowSetConfiguration> configurations;

		try
		{
			configurations = _library.ReadImportFile(filePath);
		}
		catch (Exception exception) when (IsFileProblem(exception))
		{
			_dialogService.ShowError(DIALOG_TITLE, $"The set configurations could not be imported.{Environment.NewLine}{Environment.NewLine}{exception.Message}");
			return;
		}

		if (configurations.Count == 0)
		{
			ReportResult(new OperationResultText($"'{Path.GetFileName(filePath)}' contains no set configurations.", null, true));
			return;
		}

		List<string> existingNames = [.. configurations.Where(configuration => _library.Find(configuration.Name) is not null).Select(configuration => configuration.Name)];
		bool         replace       = false;

		if (existingNames.Count > 0)
		{
			DialogChoice choice = _dialogService.AskYesNoCancel(
				DIALOG_TITLE,
				$"These set configurations already exist:{Environment.NewLine}{Environment.NewLine}{string.Join(Environment.NewLine, existingNames)}"
					+ $"{Environment.NewLine}{Environment.NewLine}Replace them with the imported ones?"
					+ $"{Environment.NewLine}{Environment.NewLine}Yes: replace them.  No: keep yours and import only the new ones."
			);

			if (choice == DialogChoice.Cancel)
			{
				return;
			}

			replace = choice == DialogChoice.Yes;
		}

		int importedCount = 0;

		if (TryChange(() => importedCount = _library.Import(configurations, replace), "imported"))
		{
			ReportResult(
				new OperationResultText(
					$"Imported {importedCount} set configuration(s) from '{Path.GetFileName(filePath)}'. Open Set configuration to load one.",
					null,
					false
				)
			);
		}
	}

	private void Export()
	{
		IsMenuOpen = false;

		string? filePath = _fileDialogService.SelectSetConfigurationsExportFile();

		if (filePath is null)
		{
			return;
		}

		try
		{
			_library.Export(filePath, _library.Configurations);
			ReportResult(new OperationResultText($"Exported {_library.Configurations.Count} set configuration(s) to '{filePath}'.", null, false));
		}
		catch (Exception exception) when (IsFileProblem(exception))
		{
			_dialogService.ShowError(DIALOG_TITLE, $"The set configurations could not be exported.{Environment.NewLine}{Environment.NewLine}{exception.Message}");
		}
	}

	private static OperationResultText Describe(string name, RowSetConfigurationLoadResult result)
	{
		List<string> details = [.. result.Skipped.Select(skip => $"{skip.ColumnName}: {skip.Problem}")];

		if (result.NotInConfiguration.Count > 0)
		{
			details.Add($"Not in the configuration, so unchanged: {string.Join(", ", result.NotInConfiguration)}");
		}

		if (result.UnknownColumns.Count > 0)
		{
			details.Add($"In the configuration but not in this table: {string.Join(", ", result.UnknownColumns)}");
		}

		string text = result.UpdatedCount == 1
			? $"Loaded '{name}': 1 column updated."
			: $"Loaded '{name}': {result.UpdatedCount} columns updated.";

		if (result.Skipped.Count > 0)
		{
			string names      = string.Join(", ", result.Skipped.Take(MAXIMUM_NAMED_SKIPS).Select(skip => skip.ColumnName));
			int    otherCount = result.Skipped.Count - MAXIMUM_NAMED_SKIPS;

			text += otherCount > 0
				? $" Kept as they were: {names} and {otherCount} more (hover for why)."
				: $" Kept as they were: {names} (hover for why).";
		}
		else if (details.Count > 0)
		{
			text += " Hover for details.";
		}

		return new OperationResultText(text, details.Count > 0 ? string.Join(Environment.NewLine, details) : null, result.Skipped.Count > 0);
	}

	private void ReportResult(OperationResultText result)
	{
		if (RowSet is not null)
		{
			RowSet.ConfigurationResult = result;
		}
	}

	private bool TryChange(Action change, string verb)
	{
		try
		{
			change();
			return true;
		}
		catch (Exception exception) when (IsFileProblem(exception))
		{
			_dialogService.ShowError(
				DIALOG_TITLE,
				$"The set configurations could not be {verb}.{Environment.NewLine}{Environment.NewLine}{exception.Message}"
			);
			return false;
		}
	}

	private void OnLibraryChanged(object? sender, EventArgs e)
	{
		if (_isMenuOpen)
		{
			RefreshOptions();
		}

		ExportCommand.NotifyCanExecuteChanged();
	}

	private static bool IsFileProblem(Exception exception)
		=> exception is IOException or UnauthorizedAccessException or InvalidDataException or InvalidOperationException or ArgumentException;
}