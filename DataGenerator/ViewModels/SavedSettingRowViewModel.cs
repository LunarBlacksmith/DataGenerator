using DataGenerator.Infrastructure;
using DataGenerator.Models;
using DataGenerator.Services;

namespace DataGenerator.ViewModels;

/// <summary>
///	One saved setting in the saved-settings manager. Edits are passed to the manager, which saves them.
/// </summary>
public sealed class SavedSettingRowViewModel : ValidatableObservableObject
{
	#region FIELDS
	private readonly Func<SavedSettingRowViewModel, string, string?> _rename;
	private readonly Action<SavedSettingRowViewModel, bool>          _setApplyAutomatically;

	private string _name;
	#endregion FIELDS

	#region PROPERTIES
	public SavedColumnSetting Setting { get; }

	public string ModeName   => GenerationModeOption.Get(Setting.GenerationMode).DisplayName;
	public string Summary    => SavedSettingDescriber.DescribeValues(Setting);
	public string TargetText => SavedSettingDescriber.DescribeTarget(Setting);
	public bool   HasTarget  => Setting.ColumnName is not null;

	/// <summary>
	///	The name; a valid new name renames the saved setting, otherwise the validation message is shown.
	/// </summary>
	public string Name
	{
		get => _name;
		set
		{
			if (!SetProperty(ref _name, value ?? string.Empty))
			{
				return;
			}

			string? error = SavedSettingsLibrary.ValidateName(_name);

			if (error is null && !string.Equals(_name.Trim(), Setting.Name, StringComparison.Ordinal))
			{
				error = _rename(this, _name.Trim());
			}

			SetError(nameof(Name), error);
		}
	}

	public bool ApplyAutomatically
	{
		get => Setting.ApplyAutomatically;
		set
		{
			if (value != Setting.ApplyAutomatically)
			{
				_setApplyAutomatically(this, value);
				OnPropertyChanged();
			}
		}
	}
	#endregion PROPERTIES

	#region CONSTRUCTOR
	/// <summary>
	///	Creates a row for one saved setting and the callbacks that persist edits.
	/// </summary>
	/// <param name="setting">
	///	The saved setting represented by the row.
	/// </param>
	/// <param name="rename">
	///	The callback that saves a valid new name and returns an error message when it cannot be saved.
	/// </param>
	/// <param name="setApplyAutomatically">
	///	The callback that saves whether the setting is applied automatically.
	/// </param>
	/// <exception cref="ArgumentNullException">
	///	Thrown when any argument is <see langword="null"/>.
	/// </exception>
	public SavedSettingRowViewModel(
		SavedColumnSetting                              setting,
		Func<SavedSettingRowViewModel, string, string?> rename,
		Action<SavedSettingRowViewModel, bool>          setApplyAutomatically
	)
	{
		Setting                = setting ?? throw new ArgumentNullException(nameof(setting));
		_rename                = rename ?? throw new ArgumentNullException(nameof(rename));
		_setApplyAutomatically = setApplyAutomatically ?? throw new ArgumentNullException(nameof(setApplyAutomatically));
		_name                  = setting.Name;
	}
	#endregion CONSTRUCTOR

	#region METHODS
	/// <summary>
	///	Shows the saved values again after the library changed.
	/// </summary>
	public void Refresh()
	{
		_name = Setting.Name;
		ClearErrors(nameof(Name));
		OnPropertyChanged(string.Empty);
	}
	#endregion METHODS
}