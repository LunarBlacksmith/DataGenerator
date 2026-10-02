using System.ComponentModel;
using DataGenerator.Infrastructure;
using DataGenerator.Models;

namespace DataGenerator.ViewModels;

/// <summary>
/// A named batch of rows for one table with its own column rules, e.g. "10 shirts in size S, then 10 in size XL".
/// </summary>
public sealed class RowSetViewModel : ValidatableObservableObject
{
	public const int MAXIMUM_ROW_COUNT = 1_000_000;

	private string                _name;
	private int                   _rowCount;
	private int                   _invalidRuleCount;
	private OperationResultText?  _configurationResult;

	public RowSetViewModel(string name, int rowCount, IReadOnlyList<ColumnRuleViewModel> columnRules)
	{
		ColumnRules          = columnRules ?? throw new ArgumentNullException(nameof(columnRules));
		_name                = name ?? string.Empty;
		_rowCount            = ClampRowCount(rowCount);
		_configurationResult = null;

		DismissConfigurationResultCommand = new RelayCommand(_ => ConfigurationResult = null);

		foreach (ColumnRuleViewModel rule in ColumnRules)
		{
			rule.ErrorsChanged   += OnRuleErrorsChanged;
			rule.SettingsChanged += OnRuleSettingsChanged;
			rule.AttachToRowSet(ColumnRules);
		}

		_invalidRuleCount = CountInvalidRules();
		BulkEdit          = new BulkColumnEditViewModel(ColumnRules);
		ValidateName();
	}

	/// <summary>
	/// Raised when the number of rows or the validity of the row set changes.
	/// </summary>
	public event EventHandler? SettingsChanged;

	public IReadOnlyList<ColumnRuleViewModel> ColumnRules { get; }

	/// <summary>
	/// Changes several selected columns at once.
	/// </summary>
	public BulkColumnEditViewModel            BulkEdit    { get; }

	public RelayCommand DismissConfigurationResultCommand { get; }

	public string Name
	{
		get => _name;
		set
		{
			if (SetProperty(ref _name, value ?? string.Empty))
			{
				ValidateName();
				OnPropertyChanged(nameof(HeaderText));
			}
		}
	}

	public int RowCount
	{
		get => _rowCount;
		set
		{
			if (SetProperty(ref _rowCount, ClampRowCount(value)))
			{
				OnPropertyChanged(nameof(HeaderText));
				SettingsChanged?.Invoke(this, EventArgs.Empty);
			}
		}
	}

	/// <summary>
	/// What happened when a set configuration was last saved from or loaded into this row set, if anything.
	/// </summary>
	public OperationResultText? ConfigurationResult
	{
		get => _configurationResult;
		set
		{
			if (SetProperty(ref _configurationResult, value))
			{
				OnPropertyChanged(nameof(HasConfigurationResult));
			}
		}
	}

	public bool HasConfigurationResult => _configurationResult is not null;

	public int    InvalidRuleCount => _invalidRuleCount;
	public bool   HasInvalidRules  => _invalidRuleCount > 0;
	public bool   IsValid          => !HasErrors && !HasInvalidRules;
	public string HeaderText       => $"{(string.IsNullOrWhiteSpace(_name) ? "(unnamed)" : _name.Trim())} ({_rowCount:N0})";

	public string ValidationSummary => _invalidRuleCount switch
	{
		0 => string.Empty,
		1 => "1 column needs attention",
		_ => $"{_invalidRuleCount} columns need attention"
	};

	public RowSetPlan CreatePlan() => new RowSetPlan
	{
		Name     = _name.Trim(),
		RowCount = _rowCount,
		Rules    = [.. ColumnRules.Select(rule => rule.CreateRule())]
	};

	/// <summary>
	/// Columns whose values SQL Server always chooses (e.g. identity columns) are not part of set configurations.
	/// </summary>
	public IEnumerable<ColumnRuleViewModel> ConfigurableRules => ColumnRules.Where(rule => rule.CanChangeMode);

	/// <summary>
	/// The generation modes and settings of every configurable column, ready to be saved as a set configuration.
	/// </summary>
	public SavedRowSetConfiguration CaptureConfiguration(string name, string tableName) => new SavedRowSetConfiguration
	{
		Name      = name.Trim(),
		TableName = tableName,
		SavedAt   = DateTime.Now,
		Columns   = [.. ConfigurableRules.Select(CaptureColumn)]
	};

	/// <summary>
	/// Gives every configurable column that the configuration mentions its saved mode and settings. Columns that cannot
	/// use them keep their settings. Columns that use the values of other columns (Copy of column, Pattern) are set last,
	/// and any that fail are tried again once more columns are set, so the order of the columns does not matter.
	/// </summary>
	public RowSetConfigurationLoadResult ApplyConfiguration(SavedRowSetConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(configuration);

		List<(ColumnRuleViewModel Rule, SavedColumnSetting Setting)> matches            = [];
		List<(string ColumnName, string Problem)>                    skipped            = [];
		List<string>                                                 notInConfiguration = [];

		foreach (ColumnRuleViewModel rule in ConfigurableRules)
		{
			SavedColumnSetting? setting = configuration.FindColumn(rule.Name);

			if (setting is null)
			{
				notInConfiguration.Add(rule.Name);
			}
			else
			{
				matches.Add((rule, setting));
			}
		}

		int                                                          updatedCount = 0;
		List<(ColumnRuleViewModel Rule, SavedColumnSetting Setting)> pending      = [.. matches.OrderBy(match => UsesOtherColumns(match.Setting) ? 1 : 0)];
		Dictionary<ColumnRuleViewModel, string>                      problems     = [];
		bool                                                         madeProgress = true;

		// Columns that refer to other columns may only be valid once those columns are set, so failed columns are
		// tried again for as long as each pass sets at least one more column.
		while (pending.Count > 0 && madeProgress)
		{
			List<(ColumnRuleViewModel Rule, SavedColumnSetting Setting)> failed = [];

			foreach ((ColumnRuleViewModel rule, SavedColumnSetting setting) in pending)
			{
				if (rule.TryApplyConfiguredSetting(setting, out string? problem))
				{
					++updatedCount;
					problems.Remove(rule);
				}
				else
				{
					problems[rule] = problem ?? "the saved settings do not suit it";
					failed.Add((rule, setting));
				}
			}

			madeProgress = failed.Count < pending.Count;
			pending      = failed;
		}

		foreach ((ColumnRuleViewModel rule, SavedColumnSetting _) in pending)
		{
			skipped.Add((rule.Name, problems[rule]));
		}

		List<string> unknownColumns =
		[
			.. configuration.Columns
				.Select(column => column.ColumnName ?? string.Empty)
				.Where(columnName => columnName.Length > 0 && !HasColumn(columnName))
		];

		return new RowSetConfigurationLoadResult
		{
			UpdatedCount       = updatedCount,
			Skipped            = skipped,
			NotInConfiguration = notInConfiguration,
			UnknownColumns     = unknownColumns
		};
	}

	/// <summary>
	/// How many configurable columns of the row set the configuration mentions.
	/// </summary>
	public int CountMatchingColumns(SavedRowSetConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(configuration);
		return ConfigurableRules.Count(rule => configuration.FindColumn(rule.Name) is not null);
	}

	public static int ClampRowCount(int rowCount) => Math.Clamp(rowCount, 1, MAXIMUM_ROW_COUNT);

	protected override void OnErrorsChanged()
	{
		OnPropertyChanged(nameof(IsValid));
		SettingsChanged?.Invoke(this, EventArgs.Empty);
	}

	private void ValidateName()
		=> SetError(nameof(Name), string.IsNullOrWhiteSpace(_name) ? "Give the row set a name, e.g. Small shirts." : null);

	private void OnRuleErrorsChanged(object? sender, DataErrorsChangedEventArgs e)
	{
		int invalidRuleCount = CountInvalidRules();

		if (invalidRuleCount == _invalidRuleCount)
		{
			return;
		}

		_invalidRuleCount = invalidRuleCount;
		OnPropertyChanged(nameof(InvalidRuleCount));
		OnPropertyChanged(nameof(HasInvalidRules));
		OnPropertyChanged(nameof(IsValid));
		OnPropertyChanged(nameof(ValidationSummary));
		SettingsChanged?.Invoke(this, EventArgs.Empty);
	}

	/// <summary>
	/// Columns that use the value of the changed column (Copy of column, COL(...)) are validated and previewed again.
	/// </summary>
	private void OnRuleSettingsChanged(object? sender, EventArgs e)
	{
		foreach (ColumnRuleViewModel rule in ColumnRules)
		{
			if (!ReferenceEquals(rule, sender))
			{
				rule.RefreshAfterOtherColumnChanged();
			}
		}
	}

	private int CountInvalidRules() => ColumnRules.Count(rule => rule.HasErrors);

	private bool HasColumn(string columnName)
		=> ColumnRules.Any(rule => string.Equals(rule.Name, columnName, StringComparison.OrdinalIgnoreCase));

	private static SavedColumnSetting CaptureColumn(ColumnRuleViewModel rule)
	{
		SavedColumnSetting setting = rule.CaptureSetting();

		setting.Name = rule.Name;
		return setting;
	}

	private static bool UsesOtherColumns(SavedColumnSetting setting)
		=> setting.GenerationMode is ValueGenerationMode.CopyColumn or ValueGenerationMode.Pattern;
}