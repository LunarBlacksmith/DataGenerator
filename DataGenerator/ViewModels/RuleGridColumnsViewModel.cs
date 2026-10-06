using System.IO;
using DataGenerator.Infrastructure;
using DataGenerator.Interfaces;
using DataGenerator.Models;

namespace DataGenerator.ViewModels;

/// <summary>
/// Which optional columns the column rules grid shows. The choice applies to every row set and is remembered for the next
/// start of the application.
/// </summary>
public sealed class RuleGridColumnsViewModel : ObservableObject
{
	public const string KEYS_COLUMN          = "Keys";
	public const string SQL_TYPE_COLUMN      = "SqlType";
	public const string NULLABLE_COLUMN      = "Nullable";
	public const string SAMPLE_VALUES_COLUMN = "SampleValues";

	private const string PREFERENCES_ERROR_TITLE = "Grid columns";

	private readonly IUserPreferencesStore _preferencesStore;
	private readonly IDialogService        _dialogService;

	private bool _isMenuOpen;

	public RuleGridColumnsViewModel(IUserPreferencesStore preferencesStore, IDialogService dialogService)
	{
		_preferencesStore = preferencesStore ?? throw new ArgumentNullException(nameof(preferencesStore));
		_dialogService    = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
		_isMenuOpen       = false;

		HashSet<string> hiddenColumns = new(_preferencesStore.Load().HiddenRuleGridColumns, StringComparer.OrdinalIgnoreCase);

		Keys = CreateOption(
			KEYS_COLUMN,
			"Keys",
			"The PK, FK, FTK and ID badges of the columns.",
			hiddenColumns
		);
		SqlType = CreateOption(
			SQL_TYPE_COLUMN,
			"SQL type",
			"The SQL Server data type of the columns.",
			hiddenColumns
		);
		Nullable = CreateOption(
			NULLABLE_COLUMN,
			"Nullable",
			"Whether the columns accept NULL.",
			hiddenColumns
		);
		SampleValues = CreateOption(
			SAMPLE_VALUES_COLUMN,
			"Sample values",
			"A preview of the values generated with the current settings.",
			hiddenColumns
		);

		Options        = [Keys, SqlType, Nullable, SampleValues];
		ShowAllCommand = new RelayCommand(_ => ShowAll(), _ => HasHiddenColumns);
	}

	public RuleGridColumnOption                 Keys           { get; }
	public RuleGridColumnOption                 SqlType        { get; }
	public RuleGridColumnOption                 Nullable       { get; }
	public RuleGridColumnOption                 SampleValues   { get; }
	public IReadOnlyList<RuleGridColumnOption>  Options        { get; }
	public RelayCommand                         ShowAllCommand { get; }

	public bool HasHiddenColumns => Options.Any(option => !option.IsVisible);

	public string HiddenColumnsText
	{
		get
		{
			int hiddenCount = Options.Count(option => !option.IsVisible);

			return hiddenCount switch
			{
				0 => "Columns",
				_ => $"Columns ({hiddenCount} hidden)"
			};
		}
	}

	/// <summary>
	/// Whether the drop-down list of the columns is open.
	/// </summary>
	public bool IsMenuOpen
	{
		get => _isMenuOpen;
		set => SetProperty(ref _isMenuOpen, value);
	}

	private RuleGridColumnOption CreateOption(string key, string displayName, string description, HashSet<string> hiddenColumns)
		=> new RuleGridColumnOption(key, displayName, description, !hiddenColumns.Contains(key), OnVisibilityChanged);

	private void ShowAll()
	{
		foreach (RuleGridColumnOption option in Options)
		{
			option.IsVisible = true;
		}
	}

	private void OnVisibilityChanged(RuleGridColumnOption option)
	{
		OnPropertyChanged(nameof(HasHiddenColumns));
		OnPropertyChanged(nameof(HiddenColumnsText));
		ShowAllCommand.NotifyCanExecuteChanged();
		SaveHiddenColumns();
	}

	private void SaveHiddenColumns()
	{
		try
		{
			UserPreferences preferences = _preferencesStore.Load();

			preferences.HiddenRuleGridColumns = [.. Options.Where(option => !option.IsVisible).Select(option => option.Key)];
			_preferencesStore.Save(preferences);
		}
		catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
		{
			_dialogService.ShowError(
				PREFERENCES_ERROR_TITLE,
				$"The columns were shown or hidden, but the choice could not be remembered for next time.{Environment.NewLine}{Environment.NewLine}{exception.Message}"
			);
		}
	}
}