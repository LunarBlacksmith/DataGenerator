using System.IO;
using DataGenerator.Infrastructure;
using DataGenerator.Models;
using DataGenerator.Services;

namespace DataGenerator.ViewModels;

/// <summary>
/// The "Save column settings" dialog: names the settings of a column rule and chooses whether new row sets use them
/// automatically.
/// </summary>
public sealed class SaveColumnSettingViewModel : ValidatableObservableObject
{
	private readonly SavedSettingsLibrary _library;
	private readonly SavedColumnSetting   _values;

	private string                   _name;
	private AutomaticApplicationKind _automaticApplication;
	private string?                  _errorMessage;
	private bool?                    _dialogResult;

	public SaveColumnSettingViewModel(SavedColumnSetting values, SavedSettingsLibrary library, string? suggestedName)
	{
		_values  = values?.Clone() ?? throw new ArgumentNullException(nameof(values));
		_library = library ?? throw new ArgumentNullException(nameof(library));

		SavedColumnSetting? automatic = _values.TableName is null || _values.ColumnName is null
			? null
			: _library.FindAutomatic(_values.TableName, _values.ColumnName);

		_name                 = suggestedName ?? automatic?.Name ?? _values.ColumnName ?? string.Empty;
		_automaticApplication = automatic is null
			? AutomaticApplicationKind.Never
			: automatic.AppliesToAnyTable ? AutomaticApplicationKind.EveryColumnWithName : AutomaticApplicationKind.ThisColumn;

		SaveCommand = new RelayCommand(_ => Save(), _ => !HasErrors);
		ValidateName();
	}

	public RelayCommand SaveCommand { get; }

	public string ValuesSummary => SavedSettingDescriber.DescribeValues(_values);

	public string ColumnDescription => $"{_values.ColumnName} in {_values.TableName}";

	public string ThisColumnText => $"Only for {_values.ColumnName} in {_values.TableName}";

	public string EveryColumnWithNameText => $"For every column named {_values.ColumnName}, in any table";

	public string Name
	{
		get => _name;
		set
		{
			if (SetProperty(ref _name, value ?? string.Empty))
			{
				ValidateName();
				OnPropertyChanged(nameof(ReplaceWarning));
				OnPropertyChanged(nameof(HasReplaceWarning));
				OnPropertyChanged(nameof(ConflictWarning));
				OnPropertyChanged(nameof(HasConflictWarning));
			}
		}
	}

	public bool ApplyNever
	{
		get => _automaticApplication == AutomaticApplicationKind.Never;
		set => SetAutomaticApplication(value, AutomaticApplicationKind.Never);
	}

	public bool ApplyToThisColumn
	{
		get => _automaticApplication == AutomaticApplicationKind.ThisColumn;
		set => SetAutomaticApplication(value, AutomaticApplicationKind.ThisColumn);
	}

	public bool ApplyToEveryColumnWithName
	{
		get => _automaticApplication == AutomaticApplicationKind.EveryColumnWithName;
		set => SetAutomaticApplication(value, AutomaticApplicationKind.EveryColumnWithName);
	}

	public string ReplaceWarning
	{
		get
		{
			SavedColumnSetting? existing = _library.Find(_name);

			return existing is null
				? string.Empty
				: $"Replaces the saved setting '{existing.Name}' ({SavedSettingDescriber.DescribeValues(existing)}).";
		}
	}

	public bool HasReplaceWarning => ReplaceWarning.Length > 0;

	public string ConflictWarning
	{
		get
		{
			SavedColumnSetting? conflict = SavedSettingsLibrary.ValidateName(_name) is null
				? _library.FindAutomaticConflict(CreateSetting())
				: null;

			return conflict is null
				? string.Empty
				: $"'{conflict.Name}' is applied automatically to the same columns now. This setting takes its place.";
		}
	}

	public bool HasConflictWarning => ConflictWarning.Length > 0;

	/// <summary>
	/// Why the setting could not be saved, e.g. because the file is read-only.
	/// </summary>
	public string? ErrorMessage
	{
		get => _errorMessage;
		private set => SetProperty(ref _errorMessage, value);
	}

	/// <summary>
	/// Set when the setting was saved, which closes the dialog.
	/// </summary>
	public bool? DialogResult
	{
		get => _dialogResult;
		private set => SetProperty(ref _dialogResult, value);
	}

	/// <summary>
	/// The name the setting was saved under.
	/// </summary>
	public string SavedName => _name.Trim();

	protected override void OnErrorsChanged() => SaveCommand.NotifyCanExecuteChanged();

	private void SetAutomaticApplication(bool isChecked, AutomaticApplicationKind kind)
	{
		if (!isChecked || _automaticApplication == kind)
		{
			return;
		}

		_automaticApplication = kind;
		OnPropertyChanged(nameof(ApplyNever));
		OnPropertyChanged(nameof(ApplyToThisColumn));
		OnPropertyChanged(nameof(ApplyToEveryColumnWithName));
		OnPropertyChanged(nameof(ConflictWarning));
		OnPropertyChanged(nameof(HasConflictWarning));
	}

	private void ValidateName() => SetError(nameof(Name), SavedSettingsLibrary.ValidateName(_name));

	private SavedColumnSetting CreateSetting()
	{
		SavedColumnSetting setting = _values.Clone();

		setting.Name               = _name.Trim();
		setting.ApplyAutomatically = _automaticApplication != AutomaticApplicationKind.Never;
		setting.TableName          = _automaticApplication == AutomaticApplicationKind.EveryColumnWithName ? null : _values.TableName;
		return setting;
	}

	private void Save()
	{
		if (HasErrors)
		{
			return;
		}

		try
		{
			_library.Save(CreateSetting());
			ErrorMessage = null;
			DialogResult = true;
		}
		catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
		{
			ErrorMessage = $"The setting could not be saved to '{_library.FilePath}': {exception.Message}";
		}
	}

	private enum AutomaticApplicationKind
	{
		Never               = 0,
		ThisColumn          = 1,
		EveryColumnWithName = 2
	}
}