using System.ComponentModel;
using DataGenerator.Infrastructure;
using DataGenerator.Models;

namespace DataGenerator.ViewModels;

/// <summary>
///	A named batch of rows for one table with its own column rules, e.g. "10 shirts in size S, then 10 in size XL".
/// </summary>
public sealed class RowSetViewModel : ValidatableObservableObject
{
	#region FIELDS
	#region PUBLIC
	public const int MAXIMUM_ROW_COUNT = RowSetPlan.MAXIMUM_ROW_COUNT;
	public const int MAXIMUM_STEP      = 99;
	#endregion PUBLIC

	#region PRIVATE
	private static readonly IReadOnlyList<ChoiceOption<RowScope>> UPDATE_SCOPE_OPTIONS;

	private string                _name;
	private int                   _rowCount;
	private int                   _step;
	private RowScope              _updateScope;
	private string                _updateCondition;
	private bool                  _requireAllRows;
	private int                   _invalidRuleCount;
	private OperationResultText?  _configurationResult;
	#endregion PRIVATE
	#endregion FIELDS

	#region PROPERTIES
	public IReadOnlyList<ColumnRuleViewModel> ColumnRules { get; }

	/// <summary>
	///	Changes several selected columns at once.
	/// </summary>
	public BulkColumnEditViewModel            BulkEdit    { get; }

	public RelayCommand DismissConfigurationResultCommand { get; }

	/// <summary>
	///	Insert sets add new rows; update sets change rows that are already in the table. Fixed when the set is created.
	/// </summary>
	public RowSetAction Action   { get; }

	public bool         IsUpdate => Action == RowSetAction.Update;

	public IReadOnlyList<ChoiceOption<RowScope>> UpdateScopeOptions => UPDATE_SCOPE_OPTIONS;

	/// <summary>
	///	Row sets run step by step (1, 2, 3, …) inside one transaction, so a later step can use rows of an earlier one.
	/// </summary>
	public int Step
	{
		get => _step;
		set
		{
			if (SetProperty(ref _step, Math.Clamp(value, RowSetPlan.FIRST_STEP, MAXIMUM_STEP)))
			{
				OnPropertyChanged(nameof(StepText));
				OnPropertyChanged(nameof(IsLaterStep));
				SettingsChanged?.Invoke(this, EventArgs.Empty);
			}
		}
	}

	public string StepText    => $"Step {_step}";
	public bool   IsLaterStep => _step > RowSetPlan.FIRST_STEP;

	/// <summary>
	///	Update sets only: which rows of the table may be changed.
	/// </summary>
	public RowScope UpdateScope
	{
		get => _updateScope;
		set => SetProperty(ref _updateScope, value);
	}

	/// <summary>
	///	Update sets only: an optional SQL condition on the alias t that the changed rows must meet, e.g. t.[Size] = 'XL'.
	/// </summary>
	public string UpdateCondition
	{
		get => _updateCondition;
		set => SetProperty(ref _updateCondition, value ?? string.Empty);
	}

	/// <summary>
	///	Update sets only: whether the run fails (and changes nothing) when fewer rows than requested can be changed.
	/// </summary>
	public bool RequireAllRows
	{
		get => _requireAllRows;
		set => SetProperty(ref _requireAllRows, value);
	}

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
				RefreshPatternPreviews();
				SettingsChanged?.Invoke(this, EventArgs.Empty);
			}
		}
	}

	/// <summary>
	///	What happened when a set configuration was last saved from or loaded into this row set, if anything.
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
	public string HeaderText       => $"{(string.IsNullOrWhiteSpace(_name) ? "(unnamed)" : _name.Trim())} ({(IsUpdate ? "change " : string.Empty)}{_rowCount:N0})";

	public string RowCountLabel    => IsUpdate ? "_Rows to change:" : "_Rows:";

	public string ValidationSummary => _invalidRuleCount switch
	{
		0 => string.Empty,
		1 => "1 column needs attention",
		_ => $"{_invalidRuleCount} columns need attention"
	};

	/// <summary>
	///	Why an update set cannot run as it is (no column gets a new value), or empty.
	/// </summary>
	public string UpdateProblem    => GetError(nameof(ColumnRules)) ?? string.Empty;
	public bool   HasUpdateProblem => UpdateProblem.Length > 0;

	/// <summary>
	///	Columns whose values SQL Server always chooses (e.g. identity columns) are not part of set configurations.
	/// </summary>
	public IEnumerable<ColumnRuleViewModel> ConfigurableRules => ColumnRules.Where(rule => rule.CanChangeMode);
	#endregion PROPERTIES

	#region EVENTS
	/// <summary>
	///	Raised when the number of rows or the validity of the row set changes.
	/// </summary>
	public event EventHandler? SettingsChanged;
	#endregion EVENTS

	#region CONSTRUCTOR
	/// <summary>
	///	Sets the default values of the static fields and properties of <see cref="RowSetViewModel"/>.
	/// </summary>
	static RowSetViewModel()
	{
		UPDATE_SCOPE_OPTIONS =
		[
			new ChoiceOption<RowScope>(RowScope.Any,       "Any rows",            "Any rows of the table may be changed."),
			new ChoiceOption<RowScope>(RowScope.Generated, "Rows generated now",  "Only rows inserted by this run (by earlier steps) may be changed."),
			new ChoiceOption<RowScope>(RowScope.Existing,  "Rows already there",  "Only rows that existed before this run may be changed.")
		];
	}

	/// <summary>
	///	Creates a row set with its column rules and attaches rule validation to the row-set state.
	/// </summary>
	/// <param name="name">
	///	The initial row-set name; <see langword="null"/> becomes empty.
	/// </param>
	/// <param name="rowCount">
	///	The requested number of rows, clamped to the supported range.
	/// </param>
	/// <param name="columnRules">
	///	The column rules that belong to the row set.
	/// </param>
	/// <param name="action">
	///	Whether the row set inserts new rows or updates existing ones.
	/// </param>
	/// <exception cref="ArgumentNullException">
	///	Thrown when <paramref name="columnRules"/> is <see langword="null"/>.
	/// </exception>
	public RowSetViewModel(string name, int rowCount, IReadOnlyList<ColumnRuleViewModel> columnRules, RowSetAction action = RowSetAction.Insert)
	{
		ColumnRules          = columnRules ?? throw new ArgumentNullException(nameof(columnRules));
		Action               = action;
		_name                = name ?? string.Empty;
		_rowCount            = ClampRowCount(rowCount);
		_step                = action == RowSetAction.Update ? RowSetPlan.FIRST_STEP + 1 : RowSetPlan.FIRST_STEP;
		_updateScope         = RowScope.Any;
		_updateCondition     = string.Empty;
		_requireAllRows      = true;
		_configurationResult = null;

		DismissConfigurationResultCommand = new RelayCommand(_ => ConfigurationResult = null);

		foreach (ColumnRuleViewModel rule in ColumnRules)
		{
			rule.ErrorsChanged   += OnRuleErrorsChanged;
			rule.SettingsChanged += OnRuleSettingsChanged;
			rule.AttachToRowSet(ColumnRules, () => _rowCount);
		}

		_invalidRuleCount = CountInvalidRules();
		BulkEdit          = new BulkColumnEditViewModel(ColumnRules);
		ValidateName();
		ValidateUpdateRules();
	}
	#endregion CONSTRUCTOR

	#region METHODS
	#region PUBLIC
	/// <summary>
	///	Limits a requested row count to the range supported by the UI and generator.
	/// </summary>
	/// <param name="rowCount">
	///	The requested row count.
	/// </param>
	/// <returns>
	///	The row count clamped between one and <see cref="MAXIMUM_ROW_COUNT"/>.
	/// </returns>
	public static int ClampRowCount(int rowCount) => Math.Clamp(rowCount, 1, MAXIMUM_ROW_COUNT);

	/// <summary>
	///	Creates the generation plan for this row set from the current settings.
	/// </summary>
	/// <returns>
	///	The plan used by the generation engine.
	/// </returns>
	public RowSetPlan CreatePlan() => new RowSetPlan
	{
		Name            = _name.Trim(),
		RowCount        = _rowCount,
		Rules           = [..
			ColumnRules
				.Select(rule => rule.CreateRule())
		],
		Action          = Action,
		Step            = _step,
		UpdateScope     = _updateScope,
		UpdateCondition = _updateCondition.Trim(),
		RequireAllRows  = _requireAllRows
	};

	/// <summary>
	///	Copies the step and update settings of another row set of the same kind.
	/// </summary>
	/// <param name="source">
	///	The row set to copy settings from.
	/// </param>
	/// <exception cref="ArgumentNullException">
	///	Thrown when <paramref name="source"/> is <see langword="null"/>.
	/// </exception>
	public void CopySettingsFrom(RowSetViewModel source)
	{
		ArgumentNullException.ThrowIfNull(source);

		Step            = source.Step;
		UpdateScope     = source.UpdateScope;
		UpdateCondition = source.UpdateCondition;
		RequireAllRows  = source.RequireAllRows;
	}

	/// <summary>
	///	The row count, generation modes and settings of every configurable column, ready to be saved as a set configuration.
	/// </summary>
	/// <param name="name">
	///	The name to save the configuration under.
	/// </param>
	/// <param name="tableName">
	///	The display name of the table the configuration is saved from.
	/// </param>
	/// <returns>
	///	The captured row-set configuration.
	/// </returns>
	public SavedRowSetConfiguration CaptureConfiguration(string name, string tableName) => new SavedRowSetConfiguration
	{
		Name      = name.Trim(),
		TableName = tableName,
		SavedAt   = DateTime.Now,
		RowCount  = RowCount,
		Columns   = [..
			ConfigurableRules
				.Select(CaptureColumn)
		]
	};

	/// <summary>
	///	Gives every configurable column that the configuration mentions its saved mode and settings. Columns that cannot
	///	use them keep their settings. Columns that use the values of other columns (Copy of column, Pattern) are set last,
	///	and any that fail are tried again once more columns are set, so the order of the columns does not matter.
	///	<para>
	///		Restores the saved row count when present; older configurations leave the count unchanged.
	///	</para>
	/// </summary>
	/// <param name="configuration">
	///	The saved configuration to load into this row set.
	/// </param>
	/// <returns>
	///	How many columns were updated and which columns were skipped, missing or unknown.
	/// </returns>
	/// <exception cref="ArgumentNullException">
	///	Thrown when <paramref name="configuration"/> is <see langword="null"/>.
	/// </exception>
	/// <exception cref="ArgumentOutOfRangeException">
	///	Thrown when the saved row count is outside the supported range.
	/// </exception>
	public RowSetConfigurationLoadResult ApplyConfiguration(SavedRowSetConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(configuration);

		if (configuration.RowCount is < 1 or > MAXIMUM_ROW_COUNT)
		{
			throw new ArgumentOutOfRangeException(nameof(configuration), $"The saved row count must be between 1 and {MAXIMUM_ROW_COUNT:N0}.");
		}

		if (configuration.RowCount is int rowCount)
		{
			RowCount = rowCount;
		}

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

		List<string> unknownColumns = [..
			configuration
				.Columns
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
	///	How many configurable columns of the row set the configuration mentions.
	/// </summary>
	/// <param name="configuration">
	///	The configuration whose column names are compared with this row set.
	/// </param>
	/// <returns>
	///	The number of configurable row-set columns found in the configuration.
	/// </returns>
	/// <exception cref="ArgumentNullException">
	///	Thrown when <paramref name="configuration"/> is <see langword="null"/>.
	/// </exception>
	public int CountMatchingColumns(SavedRowSetConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(configuration);
		return
			ConfigurableRules
				.Count(rule => configuration.FindColumn(rule.Name) is not null);
	}
	#endregion PUBLIC

	#region PROTECTED
	/// <summary>
	///	Refreshes validity and update-problem state after the row-set errors change.
	/// </summary>
	protected override void OnErrorsChanged()
	{
		OnPropertyChanged(nameof(IsValid));
		OnPropertyChanged(nameof(UpdateProblem));
		OnPropertyChanged(nameof(HasUpdateProblem));
		SettingsChanged?.Invoke(this, EventArgs.Empty);
	}
	#endregion PROTECTED

	#region PRIVATE
	/// <summary>
	///	Captures a column rule as a saved setting and stamps it with the column name.
	/// </summary>
	/// <param name="rule">
	///	The rule to capture.
	/// </param>
	/// <returns>
	///	The saved column setting for the rule.
	/// </returns>
	private static SavedColumnSetting CaptureColumn(ColumnRuleViewModel rule)
	{
		SavedColumnSetting setting = rule.CaptureSetting();

		setting.Name = rule.Name;
		return setting;
	}

	/// <summary>
	///	Whether saved column settings depend on other columns in the same row set.
	/// </summary>
	/// <param name="setting">
	///	The saved setting to inspect.
	/// </param>
	/// <returns>
	///	<see langword="true"/> when the setting copies or refers to other columns; otherwise <see langword="false"/>.
	/// </returns>
	private static bool UsesOtherColumns(SavedColumnSetting setting)
		=> setting.GenerationMode is ValueGenerationMode.CopyColumn or ValueGenerationMode.Pattern;

	/// <summary>
	///	Checks that the row set has a non-empty name.
	/// </summary>
	private void ValidateName()
		=> SetError(nameof(Name), string.IsNullOrWhiteSpace(_name) ? "Give the row set a name, e.g. Small shirts." : null);

	/// <summary>
	///	An update set must change at least one column.
	/// </summary>
	private void ValidateUpdateRules()
	{
		bool changesNothing =
			IsUpdate
			&& ColumnRules
				.All(
					rule => !rule.CanChangeMode || rule.GenerationMode == ValueGenerationMode.KeepCurrent
				);

		SetError(
			nameof(ColumnRules),
			changesNothing
				? "Choose a new value for at least one column. Columns set to Keep current value are not changed."
				: null
		);
	}

	/// <summary>
	///	Refreshes the row-set validation summary after a column rule gains or loses errors.
	/// </summary>
	/// <param name="sender">
	///	The column rule whose errors changed.
	/// </param>
	/// <param name="e">
	///	The event data that names the changed error property.
	/// </param>
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
	///	Columns that use the value of the changed column (Copy of column, COL(...)) are validated and previewed again.
	/// </summary>
	/// <param name="sender">
	///	The rule whose settings changed.
	/// </param>
	/// <param name="e">
	///	The event data for the settings change.
	/// </param>
	private void OnRuleSettingsChanged(object? sender, EventArgs e)
	{
		ValidateUpdateRules();

		foreach (ColumnRuleViewModel rule in ColumnRules)
		{
			if (!ReferenceEquals(rule, sender))
			{
				rule.RefreshAfterOtherColumnChanged();
			}
		}
	}

	/// <summary>
	///	Patterns such as LAST(...) depend on the number of rows, so their previews are built again when it changes.
	/// </summary>
	private void RefreshPatternPreviews()
	{
		foreach (ColumnRuleViewModel rule in
			ColumnRules
				.Where(rule => rule.GenerationMode == ValueGenerationMode.Pattern)
		)
		{
			rule.RefreshPreview();
		}
	}

	/// <summary>
	///	Counts column rules that currently have validation errors.
	/// </summary>
	/// <returns>
	///	The number of invalid column rules.
	/// </returns>
	private int CountInvalidRules() => ColumnRules.Count(rule => rule.HasErrors);

	/// <summary>
	///	Checks whether this row set has a column with the supplied name.
	/// </summary>
	/// <param name="columnName">
	///	The column name to find.
	/// </param>
	/// <returns>
	///	<see langword="true"/> when a rule has the column name, ignoring case; otherwise <see langword="false"/>.
	/// </returns>
	private bool HasColumn(string columnName)
		=>
			ColumnRules
				.Any(rule => string.Equals(rule.Name, columnName, StringComparison.OrdinalIgnoreCase));
	#endregion PRIVATE
	#endregion METHODS
}