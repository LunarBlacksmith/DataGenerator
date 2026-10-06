using System.IO;
using DataGenerator.Infrastructure;
using DataGenerator.Models;
using DataGenerator.Services;

namespace DataGenerator.ViewModels;

/// <summary>
///	The "Save column settings" dialog: names the settings of a column rule and chooses whether new row sets use them
///	automatically.
/// </summary>
public sealed class SaveColumnSettingViewModel : ValidatableObservableObject
{
	private readonly SavedSettingsLibrary _library;
	private readonly SavedColumnSetting   _values;

	private string                   _name;
	private AutomaticApplicationKind _automaticApplication;
	private string?                  _errorMessage;
	private bool?                    _dialogResult;

	/// <summary>
	///	Creates the save dialog for a column's current settings and suggests how it should be applied automatically.
	/// </summary>
	/// <param name="values">
	///	The column settings to save.
	/// </param>
	/// <param name="library">
	///	The saved settings library.
	/// </param>
	/// <param name="suggestedName">
	///	The initial name to show, or <see langword="null"/> to use an automatic or column name.
	/// </param>
	/// <exception cref="ArgumentNullException">
	///	Thrown when <paramref name="values"/> or <paramref name="library"/> is <see langword="null"/>.
	/// </exception>
	public SaveColumnSettingViewModel(SavedColumnSetting values, SavedSettingsLibrary library, string? suggestedName)
	{
		_values  = values?.Clone() ?? throw new ArgumentNullException(nameof(values));
		_library = library ?? throw new ArgumentNullException(nameof(library));

		SavedColumnSetting? automatic =
			_values.TableName is null || _values.ColumnName is null
				? null
				: _library.FindAutomatic(_values.TableName, _values.ColumnName);

		_name                 = suggestedName ?? automatic?.Name ?? _values.ColumnName ?? string.Empty;
		_automaticApplication =
			automatic is null
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

			return
				existing is null
					? string.Empty
					: $"Replaces the saved setting '{existing.Name}' ({SavedSettingDescriber.DescribeValues(existing)}).";
		}
	}

	public bool HasReplaceWarning => ReplaceWarning.Length > 0;

	public string ConflictWarning
	{
		get
		{
			SavedColumnSetting? conflict =
				SavedSettingsLibrary.ValidateName(_name) is null
					? _library.FindAutomaticConflict(CreateSetting())
					: null;

			return
				conflict is null
					? string.Empty
					: $"'{conflict.Name}' is applied automatically to the same columns now. This setting takes its place.";
		}
	}

	public bool HasConflictWarning => ConflictWarning.Length > 0;

	/// <summary>
	///	Why the setting could not be saved, e.g. because the file is read-only.
	/// </summary>
	public string? ErrorMessage
	{
		get => _errorMessage;
		private set => SetProperty(ref _errorMessage, value);
	}

	/// <summary>
	///	Set when the setting was saved, which closes the dialog.
	/// </summary>
	public bool? DialogResult
	{
		get => _dialogResult;
		private set => SetProperty(ref _dialogResult, value);
	}

	/// <summary>
	///	The name the setting was saved under.
	/// </summary>
	public string SavedName => _name.Trim();

	/// <summary>
	///	Refreshes whether the Save command can run after validation errors change.
	/// </summary>
	protected override void OnErrorsChanged() => SaveCommand.NotifyCanExecuteChanged();

	/// <summary>
	///	Chooses how the saved setting should be applied automatically.
	/// </summary>
	/// <param name="isChecked">
	///	Whether the radio button for <paramref name="kind"/> is checked.
	/// </param>
	/// <param name="kind">
	///	The automatic-application kind represented by the changed radio button.
	/// </param>
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

	/// <summary>
	///	Validates the entered setting name.
	/// </summary>
	private void ValidateName() => SetError(nameof(Name), SavedSettingsLibrary.ValidateName(_name));

	/// <summary>
	///	Builds the setting that will be saved from the dialog values.
	/// </summary>
	/// <returns>
	///	The saved column setting, including its name and automatic-application scope.
	/// </returns>
	private SavedColumnSetting CreateSetting()
	{
		SavedColumnSetting setting = _values.Clone();

		setting.Name               = _name.Trim();
		setting.ApplyAutomatically = _automaticApplication != AutomaticApplicationKind.Never;
		setting.TableName          = _automaticApplication == AutomaticApplicationKind.EveryColumnWithName ? null : _values.TableName;
		return setting;
	}

	/// <summary>
	///	Saves the setting and closes the dialog, or shows a file error.
	/// </summary>
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