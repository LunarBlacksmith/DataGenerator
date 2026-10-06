using System.Globalization;
using System.Text;
using DataGenerator.Infrastructure;
using DataGenerator.Interfaces;
using DataGenerator.Models;
using DataGenerator.Services;

namespace DataGenerator.ViewModels;

/// <summary>
///	How one column is generated within one row set. Only the settings of the selected mode are shown and validated.
/// </summary>
public sealed class ColumnRuleViewModel : ValidatableObservableObject
{
	private const int    PREVIEW_SAMPLE_COUNT = 3;
	private const string PREVIEW_SEPARATOR    = "  ·  ";
	private const string DEFAULT_SEQUENCE     = "1";
	private const char   SEQUENCE_SEPARATOR   = ';';

	private static readonly CultureInfo INVARIANT = CultureInfo.InvariantCulture;

	private readonly ColumnRuleServices _services;
	private readonly bool               _isUpdate;

	private ValueGenerationMode               _generationMode;
	private string                            _fixedValue        = string.Empty;
	private string                            _sequenceStartText = DEFAULT_SEQUENCE;
	private string                            _sequenceStepText  = DEFAULT_SEQUENCE;
	private string                            _regexPattern      = string.Empty;
	private string                            _patternExpression = string.Empty;
	private string                            _sourceColumnName  = string.Empty;
	private string                            _lookupExpression  = string.Empty;
	private ColumnLookup?                     _lookup;
	private LookupBuilderViewModel?           _lookupBuilder;
	private bool                              _isLookupBuilderOpen;
	private IReadOnlyList<ColumnRuleViewModel> _siblingRules       = [];
	private Func<int>?                        _rowCountProvider;
	private bool                              _isGeneratingSample;
	private string?                           _previewText;
	private bool                              _previewIsError;
	private string?                           _appliedSettingName;
	private bool                              _isApplyingSetting;
	private bool                              _isSavedSettingsMenuOpen;
	private IReadOnlyList<SavedSettingOption> _savedSettingOptions = [];

	/// <summary>
	///	Creates the generation rule view model for one table column and initialises its available modes, defaults and commands.
	/// </summary>
	/// <param name="table">
	///	The table that owns the column.
	/// </param>
	/// <param name="column">
	///	The column whose values this rule generates or updates.
	/// </param>
	/// <param name="reference">
	///	The foreign-key relationship for the column, or <see langword="null"/> when it is not a linked key.
	/// </param>
	/// <param name="isSelfReference">
	///	Whether <paramref name="reference"/> points back to the same table.
	/// </param>
	/// <param name="isUpdate">
	///	Whether the rule belongs to an update set, which changes rows that are already in the table.
	/// </param>
	/// <param name="services">
	///	The services used to convert values, parse lookups, generate samples and access saved settings.
	/// </param>
	/// <exception cref="ArgumentNullException">
	///	Thrown when <paramref name="table"/>, <paramref name="column"/> or <paramref name="services"/> is
	///	<see langword="null"/>.
	/// </exception>
	public ColumnRuleViewModel(
		TableModel         table,
		ColumnModel        column,
		ForeignKeyModel?   reference,
		bool               isSelfReference,
		bool               isUpdate,
		ColumnRuleServices services
	)
	{
		Table           = table ?? throw new ArgumentNullException(nameof(table));
		TableName       = table.DisplayName;
		Column          = column ?? throw new ArgumentNullException(nameof(column));
		_services       = services ?? throw new ArgumentNullException(nameof(services));
		_isUpdate       = isUpdate;
		Reference       = reference;
		IsSelfReference = isSelfReference;
		Category        = _services.Converter.GetCategory(column);
		AvailableModes  = CreateAvailableModes();
		_generationMode = GetDefaultMode();

		FixedValueInputKind = GetFixedValueInputKind(Category);
		SequenceInputKind   = Category == SqlTypeCategory.Integer ? TextInputKind.Integer : TextInputKind.Decimal;

		SaveSettingsCommand        = new RelayCommand(_ => SaveSettings(), _ => CanChangeMode && !HasErrors);
		ApplySavedSettingCommand   = new RelayCommand(ApplySavedSetting);
		ManageSavedSettingsCommand = new RelayCommand(_ => ManageSavedSettings());
		ApplyLookupBuilderCommand  = new RelayCommand(_ => ApplyLookupBuilder(), _ => _lookupBuilder?.CanApply == true);

		Validate();
	}

	/// <summary>
	///	Raised when the mode or a setting of the mode changes, so that columns which use this column's value can update.
	/// </summary>
	public event EventHandler? SettingsChanged;

	public TableModel Table { get; }

	/// <summary>
	///	The schema.table the column belongs to.
	/// </summary>
	public string TableName { get; }

	public bool IsUpdate => _isUpdate;

	public ColumnModel                         Column              { get; }
	public ForeignKeyModel?                    Reference           { get; }
	public bool                                IsSelfReference     { get; }
	public SqlTypeCategory                     Category            { get; }
	public IReadOnlyList<GenerationModeOption> AvailableModes      { get; }
	public TextInputKind                       FixedValueInputKind { get; }
	public TextInputKind                       SequenceInputKind   { get; }

	public RelayCommand SaveSettingsCommand        { get; }
	public RelayCommand ApplySavedSettingCommand   { get; }
	public RelayCommand ManageSavedSettingsCommand { get; }
	public RelayCommand ApplyLookupBuilderCommand  { get; }

	public string Name              => Column.Name;
	public string SqlTypeDisplay    => _services.Converter.GetDisplayType(Column);
	public string NullableText      => Column.IsNullable ? "Yes" : "No";
	public bool   IsPrimaryKey      => Column.IsPrimaryKey;
	public bool   IsForeignKey      => Column.IsForeignKey;
	public bool   IsForeignTableKey => Column.IsForeignTableKey;
	public bool   IsForeignKeyLike  => Column.IsForeignKey || Column.IsForeignTableKey;
	public bool   IsIdentity        => Column.IsIdentity;
	public bool   CanChangeMode     => AvailableModes.Count > 1;

	/// <summary>
	///	"FTK" badge for naming-convention keys that are not also declared foreign keys.
	/// </summary>
	public bool ShowForeignTableKeyBadge => Column.IsForeignTableKey && !Column.IsForeignKey;

	public string AcceptedInputDescription => _services.Converter.DescribeAcceptedInput(Column);

	public string FixedValueHint => $"Value used for every row. Enter {AcceptedInputDescription}.";

	public string ReferenceDisplay => Reference?.ReferencedDisplayName ?? string.Empty;

	public string ColumnDescription => BuildColumnDescription();

	public GenerationModeOption SelectedModeOption => GenerationModeOption.Get(_generationMode);

	public ValueGenerationMode GenerationMode
	{
		get => _generationMode;
		set
		{
			if (!IsModeAvailable(value) || !SetProperty(ref _generationMode, value))
			{
				return;
			}

			OnPropertyChanged(nameof(SelectedModeOption));
			OnPropertyChanged(nameof(ModeSummary));
			OnSettingsChanged();
		}
	}

	public string FixedValue
	{
		get => _fixedValue;
		set
		{
			if (SetProperty(ref _fixedValue, value ?? string.Empty))
			{
				OnSettingsChanged();
			}
		}
	}

	public string SequenceStartText
	{
		get => _sequenceStartText;
		set
		{
			if (SetProperty(ref _sequenceStartText, value ?? string.Empty))
			{
				OnSettingsChanged();
			}
		}
	}

	public string SequenceStepText
	{
		get => _sequenceStepText;
		set
		{
			if (SetProperty(ref _sequenceStepText, value ?? string.Empty))
			{
				OnSettingsChanged();
			}
		}
	}

	public string RegexPattern
	{
		get => _regexPattern;
		set
		{
			if (SetProperty(ref _regexPattern, value ?? string.Empty))
			{
				OnPropertyChanged(nameof(SelectedRegexProfile));
				OnSettingsChanged();
			}
		}
	}

	public IReadOnlyList<RegexProfile> RegexProfiles => _services.RegexProfiles;

	/// <summary>
	///	The preset whose pattern is the current <see cref="RegexPattern"/>, if any. Choosing a preset copies its pattern.
	/// </summary>
	public RegexProfile? SelectedRegexProfile
	{
		get => _services.RegexProfiles.FirstOrDefault(profile => string.Equals(profile.Pattern, _regexPattern, StringComparison.Ordinal));
		set
		{
			if (value is not null)
			{
				RegexPattern = value.Pattern;
			}
		}
	}

	public string PatternExpression
	{
		get => _patternExpression;
		set
		{
			if (SetProperty(ref _patternExpression, value ?? string.Empty))
			{
				OnSettingsChanged();
			}
		}
	}

	/// <summary>
	///	The column of the same row whose value the Copy of column mode copies.
	/// </summary>
	public string SourceColumnName
	{
		get => _sourceColumnName;
		set
		{
			if (SetProperty(ref _sourceColumnName, value ?? string.Empty))
			{
				OnSettingsChanged();
			}
		}
	}

	/// <summary>
	///	The column whose values the Value from table mode uses, e.g. dbo.Shirt.ShirtID UNIQUE FROM GENERATED.
	/// </summary>
	public string LookupExpression
	{
		get => _lookupExpression;
		set
		{
			if (SetProperty(ref _lookupExpression, value ?? string.Empty))
			{
				OnSettingsChanged();
			}
		}
	}

	/// <summary>
	///	The pop-up that builds <see cref="LookupExpression"/> from lists of the loaded tables and columns.
	/// </summary>
	public LookupBuilderViewModel? LookupBuilder
	{
		get => _lookupBuilder;
		private set => SetProperty(ref _lookupBuilder, value);
	}

	public bool IsLookupBuilderOpen
	{
		get => _isLookupBuilderOpen;
		set
		{
			if (value && !_isLookupBuilderOpen)
			{
				LookupBuilder = new LookupBuilderViewModel(
					_services.TableCatalog.Tables,
					Table,
					Column,
					_lookup,
					_services.LookupParser,
					ApplyLookupBuilderCommand.NotifyCanExecuteChanged
				);
				ApplyLookupBuilderCommand.NotifyCanExecuteChanged();
			}

			_ = SetProperty(ref _isLookupBuilderOpen, value);
		}
	}

	/// <summary>
	///	The other columns of the row set, which the Copy of column mode can copy.
	/// </summary>
	public IReadOnlyList<string> CopySourceOptions
		=> [.. _siblingRules.Where(rule => !ReferenceEquals(rule, this)).Select(rule => rule.Name)];

	/// <summary>
	///	Whether the column uses the value of another column (Copy of column, or COL(...) in a pattern).
	/// </summary>
	public bool UsesOtherColumns => _services.ValueGenerator.GetReferencedColumns(CreateRule()).Count > 0;

	/// <summary>
	///	Explanation shown instead of an editor for modes that have no settings.
	/// </summary>
	public string ModeSummary => _generationMode switch
	{
		ValueGenerationMode.Random              => _services.ValueGenerator.DescribeRandomValues(Column),
		ValueGenerationMode.DatabaseGenerated   => DescribeDatabaseGeneratedValue(),
		ValueGenerationMode.Null                => "NULL in every row",
		ValueGenerationMode.GeneratedForeignKey => $"Random key of the {DescribeReferencedTable()} rows generated in this run",
		ValueGenerationMode.ExistingForeignKey  => $"Random key that already exists in {DescribeReferencedTable()}",
		ValueGenerationMode.KeepCurrent         => DescribeKeptValue(),
		_                                       => string.Empty
	};

	/// <summary>
	///	A few sample values (or the problem that prevents generating them). Built on first use because previews of
	///	random values are only needed for rows that are on screen.
	/// </summary>
	public string PreviewText => _previewText ??= BuildPreview();

	public bool PreviewIsError
	{
		get
		{
			_ = PreviewText;
			return _previewIsError;
		}
	}

	/// <summary>
	///	The validation message of the selected mode, or <see langword="null"/> when its settings are valid.
	/// </summary>
	public string? ValidationError => _generationMode switch
	{
		ValueGenerationMode.Fixed       => GetError(nameof(FixedValue)),
		ValueGenerationMode.Sequence    => GetError(nameof(SequenceStartText)) ?? GetError(nameof(SequenceStepText)),
		ValueGenerationMode.Regex       => GetError(nameof(RegexPattern)),
		ValueGenerationMode.Pattern     => GetError(nameof(PatternExpression)),
		ValueGenerationMode.CopyColumn  => GetError(nameof(SourceColumnName)),
		ValueGenerationMode.TableLookup => GetError(nameof(LookupExpression)),
		_                               => null
	};

	/// <summary>
	///	The saved setting the column uses, or <see langword="null"/> once its settings were changed by hand.
	/// </summary>
	public string? AppliedSettingName
	{
		get => _appliedSettingName;
		private set
		{
			if (SetProperty(ref _appliedSettingName, value))
			{
				OnPropertyChanged(nameof(HasAppliedSetting));
				OnPropertyChanged(nameof(SavedSettingsButtonHint));
			}
		}
	}

	public bool HasAppliedSetting => _appliedSettingName is not null;

	public string SavedSettingsButtonHint
		=>
			_appliedSettingName is null
				? "Saved settings: save this column's mode and settings for reuse, or apply settings you or a colleague saved earlier."
				: $"Uses the saved setting '{_appliedSettingName}'. Click to save, apply or manage saved settings.";

	/// <summary>
	///	Whether the saved-settings menu is open. Opening it lists the saved settings that suit the column.
	/// </summary>
	public bool IsSavedSettingsMenuOpen
	{
		get => _isSavedSettingsMenuOpen;
		set
		{
			if (value && !_isSavedSettingsMenuOpen)
			{
				LoadSavedSettingOptions();
			}

			_ = SetProperty(ref _isSavedSettingsMenuOpen, value);
		}
	}

	/// <summary>
	///	The saved settings whose mode the column supports; the ones saved for a column with the same name come first.
	/// </summary>
	public IReadOnlyList<SavedSettingOption> SavedSettingOptions
	{
		get => _savedSettingOptions;
		private set
		{
			if (SetProperty(ref _savedSettingOptions, value))
			{
				OnPropertyChanged(nameof(HasSavedSettingOptions));
			}
		}
	}

	public bool HasSavedSettingOptions => _savedSettingOptions.Count > 0;

	/// <summary>
	///	Checks whether the column can use the requested generation mode.
	/// </summary>
	/// <param name="mode">
	///	The mode to look for in <see cref="AvailableModes"/>.
	/// </param>
	/// <returns>
	///	<see langword="true"/> when the mode is available for this column; otherwise <see langword="false"/>.
	/// </returns>
	public bool IsModeAvailable(ValueGenerationMode mode) => AvailableModes.Any(option => option.Mode == mode);

	/// <summary>
	///	Whether the current mode has a value in the Settings cell that can be typed (fixed value, sequence, pattern, …).
	/// </summary>
	public bool HasEditableSetting => _generationMode is ValueGenerationMode.Fixed
		or ValueGenerationMode.Sequence
		or ValueGenerationMode.Regex
		or ValueGenerationMode.Pattern
		or ValueGenerationMode.CopyColumn
		or ValueGenerationMode.TableLookup;

	/// <summary>
	///	Why the column cannot use a mode, or <see langword="null"/> when it can.
	/// </summary>
	/// <param name="mode">
	///	The mode the user tried to choose.
	/// </param>
	/// <returns>
	///	The problem text, or <see langword="null"/> when the mode is available.
	/// </returns>
	public string? DescribeModeProblem(ValueGenerationMode mode)
	{
		if (mode == _generationMode || IsModeAvailable(mode))
		{
			return null;
		}

		if (!CanChangeMode)
		{
			return
				_isUpdate && !IsGeneratedOnlyBySqlServer()
					? "it is part of the primary key, which update sets do not change"
					: $"{DescribeDatabaseGeneratedValue()}, so its mode cannot be changed";
		}

		return mode switch
		{
			ValueGenerationMode.KeepCurrent         => "only update sets can keep the current value",
			ValueGenerationMode.DatabaseGenerated when _isUpdate
			                                        => "update sets cannot ask SQL Server for a value",
			ValueGenerationMode.Null                => "it does not allow NULL",
			ValueGenerationMode.ExistingForeignKey  => "it does not refer to another table",
			ValueGenerationMode.GeneratedForeignKey =>
				Reference is null
					? "it does not refer to another table"
					: "it refers to its own table, whose keys are not known while its rows are inserted",
			ValueGenerationMode.DatabaseGenerated   => "it has no default value for SQL Server to use",
			_                                       => $"{GenerationModeOption.Get(mode).DisplayName} does not suit {SqlTypeDisplay}"
		};
	}

	/// <summary>
	///	Tries to put a typed value into the setting field for the current mode.
	///	Invalid values restore the previous settings and return the validation problem.
	/// </summary>
	/// <param name="value">
	///	The text to apply to the current mode's editable setting.
	/// </param>
	/// <param name="problem">
	///	The validation problem when the value cannot be used, or <see langword="null"/> when it is accepted.
	/// </param>
	/// <returns>
	///	<see langword="true"/> when the value was applied; otherwise <see langword="false"/>.
	/// </returns>
	/// <exception cref="ArgumentNullException">
	///	Thrown when <paramref name="value"/> is <see langword="null"/>.
	/// </exception>
	public bool TrySetSettingValue(string value, out string? problem)
	{
		ArgumentNullException.ThrowIfNull(value);

		if (!HasEditableSetting)
		{
			problem = $"{SelectedModeOption.DisplayName} has no settings to change";
			return false;
		}

		SavedColumnSetting previous           = CaptureSetting();
		string?            appliedSettingName = _appliedSettingName;

		switch (_generationMode)
		{
			case ValueGenerationMode.Fixed:
			{
				FixedValue = value;
				break;
			}

			case ValueGenerationMode.Sequence:
			{
				string[] parts = value.Split(SEQUENCE_SEPARATOR, 2);

				SequenceStartText = parts[0].Trim();

				if (parts.Length == 2)
				{
					SequenceStepText = parts[1].Trim();
				}

				break;
			}

			case ValueGenerationMode.Regex:
			{
				RegexPattern = value;
				break;
			}

			case ValueGenerationMode.Pattern:
			{
				PatternExpression = value;
				break;
			}

			case ValueGenerationMode.CopyColumn:
			{
				SourceColumnName = FindSiblingRule(value)?.Name ?? value.Trim();
				break;
			}

			case ValueGenerationMode.TableLookup:
			{
				LookupExpression = value.Trim();
				break;
			}
		}

		problem = ValidationError;

		if (problem is null)
		{
			return true;
		}

		_ = ApplySetting(previous);
		AppliedSettingName = appliedSettingName;
		return false;
	}

	/// <summary>
	///	Uses the mode and settings saved for this column in a set configuration. When the column cannot use the mode, or
	///	the saved value is not valid for it, its settings are put back as they were and the problem is returned.
	/// </summary>
	/// <param name="setting">
	///	The configured mode and values to apply.
	/// </param>
	/// <param name="problem">
	///	The mode or validation problem, or <see langword="null"/> when the setting was applied.
	/// </param>
	/// <returns>
	///	<see langword="true"/> when the setting was applied; otherwise <see langword="false"/>.
	/// </returns>
	/// <exception cref="ArgumentNullException">
	///	Thrown when <paramref name="setting"/> is <see langword="null"/>.
	/// </exception>
	public bool TryApplyConfiguredSetting(SavedColumnSetting setting, out string? problem)
	{
		ArgumentNullException.ThrowIfNull(setting);

		problem = DescribeModeProblem(setting.GenerationMode);

		if (problem is not null)
		{
			return false;
		}

		SavedColumnSetting previous           = CaptureAllValues();
		string?            appliedSettingName = _appliedSettingName;

		_       = ApplySetting(setting);
		problem = ValidationError;

		if (problem is null)
		{
			// The values come from a set configuration, not from a saved column setting.
			AppliedSettingName = null;
			return true;
		}

		RestoreAllValues(previous);
		AppliedSettingName = appliedSettingName;
		return false;
	}

	/// <summary>
	///	Uses the mode and settings of a saved setting. Settings of other modes keep their values.
	/// </summary>
	/// <param name="setting">
	///	The saved setting to apply.
	/// </param>
	/// <returns>
	///	<see langword="true"/> when the mode is available and the setting was applied; otherwise <see langword="false"/>.
	/// </returns>
	/// <exception cref="ArgumentNullException">
	///	Thrown when <paramref name="setting"/> is <see langword="null"/>.
	/// </exception>
	public bool ApplySetting(SavedColumnSetting setting)
	{
		ArgumentNullException.ThrowIfNull(setting);

		if (!IsModeAvailable(setting.GenerationMode))
		{
			return false;
		}

		_isApplyingSetting = true;

		try
		{
			switch (setting.GenerationMode)
			{
				case ValueGenerationMode.Fixed:
				{
					FixedValue = setting.FixedValue;
					break;
				}

				case ValueGenerationMode.Sequence:
				{
					SequenceStartText = setting.SequenceStart;
					SequenceStepText  = setting.SequenceStep;
					break;
				}

				case ValueGenerationMode.Regex:
				{
					RegexPattern = setting.RegexPattern;
					break;
				}

				case ValueGenerationMode.Pattern:
				{
					PatternExpression = setting.PatternExpression;
					break;
				}

				case ValueGenerationMode.CopyColumn:
				{
					SourceColumnName = setting.SourceColumnName;
					break;
				}

				case ValueGenerationMode.TableLookup:
				{
					LookupExpression = setting.LookupExpression;
					break;
				}
			}

			GenerationMode = setting.GenerationMode;
		}
		finally
		{
			_isApplyingSetting = false;
		}

		AppliedSettingName = setting.Name;
		return true;
	}

	/// <summary>
	///	Uses the saved setting that applies automatically to this column, if there is one.
	/// </summary>
	/// <returns>
	///	<see langword="true"/> when the column changed; otherwise <see langword="false"/>.
	/// </returns>
	public bool ApplyAutomaticSetting()
	{
		SavedColumnSetting? setting = _services.SavedSettings.FindAutomatic(TableName, Column.Name);

		return
			setting is null || (setting.Name == _appliedSettingName && setting.HasSameValues(CaptureSetting()))
				? false
				: ApplySetting(setting);
	}

	/// <summary>
	///	Updates <see cref="AppliedSettingName"/> after saved settings were renamed, changed or deleted.
	/// </summary>
	public void RefreshAppliedSetting()
	{
		if (_appliedSettingName is not null)
		{
			AppliedSettingName = _services.SavedSettings.FindEquivalent(CaptureSetting(), _appliedSettingName)?.Name;
		}
	}

	/// <summary>
	///	The mode and the settings of that mode, ready to be saved.
	/// </summary>
	/// <returns>
	///	A saved-setting snapshot for the current mode.
	/// </returns>
	public SavedColumnSetting CaptureSetting() => new SavedColumnSetting
	{
		GenerationMode    = _generationMode,
		FixedValue        = _generationMode == ValueGenerationMode.Fixed ? _fixedValue : string.Empty,
		SequenceStart     = _generationMode == ValueGenerationMode.Sequence ? _sequenceStartText.Trim() : DEFAULT_SEQUENCE,
		SequenceStep      = _generationMode == ValueGenerationMode.Sequence ? _sequenceStepText.Trim() : DEFAULT_SEQUENCE,
		RegexPattern      = _generationMode == ValueGenerationMode.Regex ? _regexPattern : string.Empty,
		PatternExpression = _generationMode == ValueGenerationMode.Pattern ? _patternExpression : string.Empty,
		SourceColumnName  = _generationMode == ValueGenerationMode.CopyColumn ? _sourceColumnName.Trim() : string.Empty,
		LookupExpression  = _generationMode == ValueGenerationMode.TableLookup ? _lookupExpression.Trim() : string.Empty,
		TableName         = TableName,
		ColumnName        = Column.Name
	};

	/// <summary>
	///	Copies all mode values and the selected mode from another column rule.
	/// </summary>
	/// <param name="source">
	///	The rule whose values should be copied.
	/// </param>
	/// <exception cref="ArgumentNullException">
	///	Thrown when <paramref name="source"/> is <see langword="null"/>.
	/// </exception>
	public void CopyFrom(ColumnRuleViewModel source)
	{
		ArgumentNullException.ThrowIfNull(source);

		FixedValue        = source.FixedValue;
		SequenceStartText = source.SequenceStartText;
		SequenceStepText  = source.SequenceStepText;
		RegexPattern      = source.RegexPattern;
		PatternExpression = source.PatternExpression;
		SourceColumnName  = source.SourceColumnName;
		LookupExpression  = source.LookupExpression;
		GenerationMode    = source.GenerationMode;

		AppliedSettingName = source.AppliedSettingName;
	}

	/// <summary>
	///	Clears the cached preview and notifies the preview properties so the UI rebuilds it on demand.
	/// </summary>
	public void RefreshPreview()
	{
		_previewText = null;
		OnPropertyChanged(nameof(PreviewText));
		OnPropertyChanged(nameof(PreviewIsError));
	}

	/// <summary>
	///	Builds the model used by validation, previews and generation from the current view-model values.
	/// </summary>
	/// <returns>
	///	A column rule containing the selected mode, converted sequence numbers and lookup information.
	/// </returns>
	public ColumnRule CreateRule() => new ColumnRule
	{
		Column            = Column,
		GenerationMode    = _generationMode,
		FixedValue        = _fixedValue,
		SequenceStart     = TryParseNumber(_sequenceStartText, out decimal start) ? start : 1,
		SequenceStep      = TryParseNumber(_sequenceStepText, out decimal step) ? step : 1,
		RegexPattern      = _regexPattern,
		PatternExpression = _patternExpression,
		SourceColumnName  = _sourceColumnName.Trim(),
		LookupExpression  = _lookupExpression.Trim(),
		Lookup            = _generationMode == ValueGenerationMode.TableLookup ? _lookup : null,
		Reference         = Reference
	};

	/// <summary>
	///	Gives the rule access to the other columns of its row set, for the Copy of column mode and COL(...) in patterns,
	///	and to its row count, for LAST(...) in patterns.
	/// </summary>
	/// <param name="rowSetRules">
	///	All column rules in the same row set.
	/// </param>
	/// <param name="rowCountProvider">
	///	A function that returns the current row count for the row set.
	/// </param>
	/// <exception cref="ArgumentNullException">
	///	Thrown when <paramref name="rowSetRules"/> or <paramref name="rowCountProvider"/> is <see langword="null"/>.
	/// </exception>
	public void AttachToRowSet(IReadOnlyList<ColumnRuleViewModel> rowSetRules, Func<int> rowCountProvider)
	{
		_siblingRules     = rowSetRules ?? throw new ArgumentNullException(nameof(rowSetRules));
		_rowCountProvider = rowCountProvider ?? throw new ArgumentNullException(nameof(rowCountProvider));
		OnPropertyChanged(nameof(CopySourceOptions));
		Validate();
		RefreshPreview();
	}

	/// <summary>
	///	Validates the rule again after another column of the row set changed, when the rule uses other columns.
	/// </summary>
	public void RefreshAfterOtherColumnChanged()
	{
		if (UsesOtherColumns)
		{
			Validate();
			RefreshPreview();
		}
	}

	/// <summary>
	///	Refreshes validation-dependent UI state after the error collection changes.
	/// </summary>
	protected override void OnErrorsChanged()
	{
		OnPropertyChanged(nameof(ValidationError));
		SaveSettingsCommand.NotifyCanExecuteChanged();
	}

	/// <summary>
	///	Revalidates the rule, clears manual saved-setting attribution and raises <see cref="SettingsChanged"/>.
	/// </summary>
	private void OnSettingsChanged()
	{
		if (!_isApplyingSetting)
		{
			AppliedSettingName = null;
		}

		Validate();
		RefreshPreview();
		SettingsChanged?.Invoke(this, EventArgs.Empty);
	}

	/// <summary>
	///	Loads saved settings whose modes are valid for the column, placing settings saved for the same column first.
	/// </summary>
	private void LoadSavedSettingOptions()
		=> SavedSettingOptions = [..
			_services
				.SavedSettings
				.Settings
				.Where(setting => IsModeAvailable(setting.GenerationMode))
				.Select(setting => new SavedSettingOption(setting, string.Equals(setting.ColumnName, Column.Name, StringComparison.OrdinalIgnoreCase)))
				.OrderByDescending(option => option.IsSavedForColumn)
		];

	/// <summary>
	///	Opens the save dialog for the current setting and records the saved setting name when the dialog succeeds.
	/// </summary>
	private void SaveSettings()
	{
		IsSavedSettingsMenuOpen = false;

		SaveColumnSettingViewModel dialog = new(CaptureSetting(), _services.SavedSettings, _appliedSettingName);

		if (_services.SavedSettingsWindows.ShowSaveDialog(dialog))
		{
			AppliedSettingName = dialog.SavedName;
		}
	}

	/// <summary>
	///	Applies the saved setting represented by a saved-setting menu item.
	/// </summary>
	/// <param name="parameter">
	///	The command parameter, expected to be a <see cref="SavedSettingOption"/>.
	/// </param>
	private void ApplySavedSetting(object? parameter)
	{
		IsSavedSettingsMenuOpen = false;

		if (parameter is SavedSettingOption option)
		{
			_ = ApplySetting(option.Setting);
		}
	}

	/// <summary>
	///	Closes the menu and opens the saved-settings manager window.
	/// </summary>
	private void ManageSavedSettings()
	{
		IsSavedSettingsMenuOpen = false;
		_services.SavedSettingsWindows.ShowManager();
	}

	/// <summary>
	///	The mode and the values of every mode, so they can all be put back after a change that is undone.
	/// </summary>
	/// <returns>
	///	A saved-setting snapshot containing every editable value.
	/// </returns>
	private SavedColumnSetting CaptureAllValues() => new SavedColumnSetting
	{
		GenerationMode    = _generationMode,
		FixedValue        = _fixedValue,
		SequenceStart     = _sequenceStartText,
		SequenceStep      = _sequenceStepText,
		RegexPattern      = _regexPattern,
		PatternExpression = _patternExpression,
		SourceColumnName  = _sourceColumnName,
		LookupExpression  = _lookupExpression,
		TableName         = TableName,
		ColumnName        = Column.Name
	};

	/// <summary>
	///	Restores all mode values from a snapshot without clearing the applied saved-setting name during the restore.
	/// </summary>
	/// <param name="values">
	///	The snapshot previously created by <see cref="CaptureAllValues"/>.
	/// </param>
	private void RestoreAllValues(SavedColumnSetting values)
	{
		_isApplyingSetting = true;

		try
		{
			FixedValue        = values.FixedValue;
			SequenceStartText = values.SequenceStart;
			SequenceStepText  = values.SequenceStep;
			RegexPattern      = values.RegexPattern;
			PatternExpression = values.PatternExpression;
			SourceColumnName  = values.SourceColumnName;
			LookupExpression  = values.LookupExpression;
			GenerationMode    = values.GenerationMode;
		}
		finally
		{
			_isApplyingSetting = false;
		}
	}

	/// <summary>
	///	Runs the validator for the selected mode and updates the visible validation message.
	/// </summary>
	private void Validate()
	{
		SetError(nameof(FixedValue), _generationMode == ValueGenerationMode.Fixed ? ValidateFixedValue() : null);
		SetError(nameof(SequenceStartText), _generationMode == ValueGenerationMode.Sequence ? ValidateSequenceStart() : null);
		SetError(nameof(SequenceStepText), _generationMode == ValueGenerationMode.Sequence ? ValidateSequenceStep() : null);
		SetError(nameof(RegexPattern), _generationMode == ValueGenerationMode.Regex ? ValidateRegexPattern() : null);
		SetError(nameof(PatternExpression), _generationMode == ValueGenerationMode.Pattern ? ValidatePatternExpression() : null);
		SetError(nameof(SourceColumnName), _generationMode == ValueGenerationMode.CopyColumn ? ValidateSourceColumn() : null);
		SetError(nameof(LookupExpression), _generationMode == ValueGenerationMode.TableLookup ? ValidateLookupExpression() : null);
		OnPropertyChanged(nameof(ValidationError));
	}

	/// <summary>
	///	Parses and validates the Value from table expression, updating the cached lookup when it is valid.
	/// </summary>
	/// <returns>
	///	The validation problem, or <see langword="null"/> when the lookup can be used.
	/// </returns>
	private string? ValidateLookupExpression()
	{
		_lookup = null;

		if (!_services.LookupParser.TryParse(_lookupExpression, Table, out ColumnLookup? lookup, out string errorMessage))
		{
			return errorMessage;
		}

		if (_services.Converter.GetCategory(lookup!.SourceColumn) is SqlTypeCategory.Unsupported or SqlTypeCategory.RowVersion)
		{
			return $"The values of {lookup.SourceDisplayName} ({_services.Converter.GetDisplayType(lookup.SourceColumn)}) cannot be copied. Choose another column.";
		}

		if (	lookup.IsUnique
				&& ReferenceEquals(lookup.SourceTable, Table)
				&& string.Equals(lookup.SourceColumn.Name, Column.Name, StringComparison.OrdinalIgnoreCase)
		)
		{
			return "UNIQUE leaves out the values the column already has, so a column cannot take unique values from itself. "
				+ "Remove UNIQUE or choose another column.";
		}

		_lookup = lookup;
		return null;
	}

	/// <summary>
	///	Copies the lookup-builder expression into the rule and closes the builder when the builder can be applied.
	/// </summary>
	private void ApplyLookupBuilder()
	{
		if (_lookupBuilder?.CanApply != true)
		{
			return;
		}

		LookupExpression    = _lookupBuilder.Expression;
		IsLookupBuilderOpen = false;
	}

	/// <summary>
	///	Validates the Copy of column source and checks that a sample can be generated from it.
	/// </summary>
	/// <returns>
	///	The validation problem, or <see langword="null"/> when the source column is usable.
	/// </returns>
	private string? ValidateSourceColumn()
		=>
			string.IsNullOrWhiteSpace(_sourceColumnName)
				? "Choose the column whose value is copied."
				: ValidateReferencedColumns() ?? TryGenerateSample(0, out _);

	/// <summary>
	///	Checks that the columns used by Copy of column or COL(...) exist in the row set and have a value before the row is inserted.
	/// </summary>
	/// <returns>
	///	The validation problem, or <see langword="null"/> when all referenced columns are usable.
	/// </returns>
	private string? ValidateReferencedColumns()
	{
		foreach (string columnName in _services.ValueGenerator.GetReferencedColumns(CreateRule()))
		{
			ColumnRuleViewModel? referencedRule = FindSiblingRule(columnName);

			if (referencedRule is null)
			{
				return
					_siblingRules.Count == 0
						? null
						: $"The table has no column named [{columnName}].";
			}

			if (ReferenceEquals(referencedRule, this))
			{
				return "A column cannot use its own value. Choose another column.";
			}

			if (referencedRule.GenerationMode == ValueGenerationMode.KeepCurrent)
			{
				return $"[{referencedRule.Name}] keeps its current value, which is not known when the new values are generated. "
					+ $"Choose another column, or give [{referencedRule.Name}] a new value.";
			}

			if (referencedRule.GenerationMode == ValueGenerationMode.DatabaseGenerated)
			{
				return $"SQL Server generates [{referencedRule.Name}] while inserting the row, so its value is not known in advance. "
					+ $"Choose another column, or another mode for [{referencedRule.Name}].";
			}
		}

		return null;
	}

	/// <summary>
	///	Finds another rule in the row set by column name.
	/// </summary>
	/// <param name="columnName">
	///	The column name to match, ignoring surrounding white space and case.
	/// </param>
	/// <returns>
	///	The matching sibling rule, or <see langword="null"/> when no rule matches.
	/// </returns>
	private ColumnRuleViewModel? FindSiblingRule(string columnName)
		=> _siblingRules.FirstOrDefault(rule => string.Equals(rule.Name, columnName.Trim(), StringComparison.OrdinalIgnoreCase));

	/// <summary>
	///	Validates that the fixed value can be converted to the column's SQL type.
	/// </summary>
	/// <returns>
	///	The validation problem, or <see langword="null"/> when the fixed value is valid.
	/// </returns>
	private string? ValidateFixedValue()
		=>
			_fixedValue.Length == 0 && Category != SqlTypeCategory.Text
				? $"Enter {AcceptedInputDescription}."
				: _services.Converter.TryConvertText(Column, _fixedValue, out _, out string errorMessage)
					? null
					: errorMessage;

	/// <summary>
	///	Validates the first value of a sequence and checks that it fits the column type.
	/// </summary>
	/// <returns>
	///	The validation problem, or <see langword="null"/> when the sequence start is valid.
	/// </returns>
	private string? ValidateSequenceStart()
	{
		if (!TryParseNumber(_sequenceStartText, out decimal start))
		{
			return "Enter the first value of the sequence, e.g. 1.";
		}

		try
		{
			_ = _services.Converter.ConvertSequenceValue(Column, start);
			return null;
		}
		catch (Exception exception) when (IsValueProblem(exception))
		{
			return exception.Message;
		}
	}

	/// <summary>
	///	Validates the amount added to each generated sequence value.
	/// </summary>
	/// <returns>
	///	The validation problem, or <see langword="null"/> when the sequence step is valid.
	/// </returns>
	private string? ValidateSequenceStep()
		=>
			!TryParseNumber(_sequenceStepText, out decimal step)
				? "Enter the amount added for each row, e.g. 1. Use a negative number to count down."
				: Category == SqlTypeCategory.Integer && step != decimal.Truncate(step)
					? $"{SqlTypeDisplay} only stores whole numbers, so the step must be a whole number."
					: null;

	/// <summary>
	///	Validates that a regular expression is present and can produce a sample value.
	/// </summary>
	/// <returns>
	///	The validation problem, or <see langword="null"/> when the regular expression is valid.
	/// </returns>
	private string? ValidateRegexPattern()
		=>
			string.IsNullOrWhiteSpace(_regexPattern)
				? "Enter a regular expression, e.g. [A-Z]{3}-[0-9]{4}, or pick a preset."
				: TryGenerateSample(0, out _);

	/// <summary>
	///	Validates the pattern expression, its referenced columns and a sample value.
	/// </summary>
	/// <returns>
	///	The validation problem, or <see langword="null"/> when the pattern is valid.
	/// </returns>
	private string? ValidatePatternExpression()
		=>
			string.IsNullOrWhiteSpace(_patternExpression)
				? "Enter a pattern, e.g. P FOLLOWED BY SEQ(1-1000) FOLLOWED BY (X OR Y). Click ? for the pattern language reference."
				: _services.PatternGenerator.TryValidate(_patternExpression, out string errorMessage)
					? ValidateReferencedColumns() ?? TryGenerateSample(0, out _)
					: errorMessage;

	/// <summary>
	///	Builds the preview text for the selected mode, or records that the preview text is a validation problem.
	/// </summary>
	/// <returns>
	///	The preview text shown for the column rule.
	/// </returns>
	private string BuildPreview()
	{
		_previewIsError = false;

		switch (_generationMode)
		{
			case ValueGenerationMode.DatabaseGenerated:
			{
				return "(set by SQL Server)";
			}

			case ValueGenerationMode.Null:
			{
				return "NULL";
			}

			case ValueGenerationMode.GeneratedForeignKey:
			case ValueGenerationMode.ExistingForeignKey:
			{
				return $"(keys of {DescribeReferencedTable()})";
			}

			case ValueGenerationMode.KeepCurrent:
			{
				return "(current value)";
			}
		}

		string? validationError = ValidationError;

		if (validationError is not null)
		{
			_previewIsError = true;
			return validationError;
		}

		if (_generationMode == ValueGenerationMode.TableLookup && _lookup is not null)
		{
			return $"({(_lookup.IsUnique ? "unique values" : "values")} of {_lookup.SourceDisplayName}, chosen while generating)";
		}

		int          sampleCount = _generationMode == ValueGenerationMode.Fixed ? 1 : PREVIEW_SAMPLE_COUNT;
		List<string> samples     = new(sampleCount);

		for (int rowIndex = 0; rowIndex < sampleCount; ++rowIndex)
		{
			string? sampleError = TryGenerateSample(rowIndex, out object? value);

			if (sampleError is not null)
			{
				_previewIsError = true;
				return sampleError;
			}

			if (value is SampleUnavailableException unavailable)
			{
				return unavailable.Message;
			}

			samples.Add(_services.Converter.FormatForDisplay(Column, value));
		}

		return string.Join(PREVIEW_SEPARATOR, samples);
	}

	/// <summary>
	///	Tries to generate one preview sample value for the requested row.
	/// </summary>
	/// <param name="rowIndex">
	///	The zero-based preview row index to generate.
	/// </param>
	/// <param name="value">
	///	The generated value, <see langword="null"/> when conversion failed, or a sample-unavailable marker.
	/// </param>
	/// <returns>
	///	The conversion problem, or <see langword="null"/> when generation succeeded or is only unavailable for preview.
	/// </returns>
	/// <remarks>
	///	When the value depends on a column whose value is only known while generating, there is no error and
	///	<paramref name="value"/> is the <see cref="SampleUnavailableException"/> that explains why.
	/// </remarks>
	private string? TryGenerateSample(long rowIndex, out object? value)
	{
		try
		{
			value = GenerateSample(rowIndex);
			return null;
		}
		catch (SampleUnavailableException exception)
		{
			value = exception;
			return null;
		}
		catch (Exception exception) when (IsValueProblem(exception))
		{
			value = null;
			return exception.Message;
		}
	}

	/// <summary>
	///	A sample value for the preview; the values of other columns are generated with their own rules for the same row.
	/// </summary>
	/// <param name="rowIndex">
	///	The zero-based preview row index to generate.
	/// </param>
	/// <returns>
	///	The generated sample value, or <see langword="null"/> when the rule generates a database null.
	/// </returns>
	/// <exception cref="InvalidOperationException">
	///	Thrown when sample generation detects a loop through this column.
	/// </exception>
	private object? GenerateSample(long rowIndex)
	{
		if (_isGeneratingSample)
		{
			throw new InvalidOperationException(
				$"The columns use each other's values in a loop through [{Name}], so none of them can be generated first."
			);
		}

		_isGeneratingSample = true;

		try
		{
			return _services.ValueGenerator.Generate(
				CreateRule(),
				rowIndex,
				_rowCountProvider?.Invoke() ?? PREVIEW_SAMPLE_COUNT,
				new SampleRowValues(this, rowIndex)
			);
		}
		finally
		{
			_isGeneratingSample = false;
		}
	}

	/// <summary>
	///	Generates this column's preview value when another column needs it while generating its own sample.
	/// </summary>
	/// <param name="rowIndex">
	///	The zero-based preview row index to generate.
	/// </param>
	/// <returns>
	///	The generated sample value, or <see langword="null"/> when the rule generates a database null.
	/// </returns>
	/// <exception cref="SampleUnavailableException">
	///	Thrown when this value is only known during real generation.
	/// </exception>
	/// <exception cref="InvalidOperationException">
	///	Thrown when this column currently has no valid value to supply.
	/// </exception>
	private object? GenerateSampleForOtherColumn(long rowIndex)
	{
		switch (_generationMode)
		{
			case ValueGenerationMode.GeneratedForeignKey:
			case ValueGenerationMode.ExistingForeignKey:
			{
				throw new SampleUnavailableException($"(uses the key in [{Name}], chosen while generating)");
			}

			case ValueGenerationMode.DatabaseGenerated:
			{
				throw new SampleUnavailableException($"(uses [{Name}], set by SQL Server)");
			}

			case ValueGenerationMode.TableLookup:
			{
				throw new SampleUnavailableException($"(uses the value in [{Name}], chosen while generating)");
			}

			case ValueGenerationMode.KeepCurrent:
			{
				throw new SampleUnavailableException($"(uses the current value of [{Name}])");
			}
		}

		try
		{
			return GenerateSample(rowIndex);
		}
		catch (Exception exception) when (IsValueProblem(exception) && exception is not SampleUnavailableException)
		{
			throw new InvalidOperationException($"[{Name}] has no valid value yet: {exception.Message}", exception);
		}
	}

	/// <summary>
	///	Builds the list of generation modes that suit the column metadata and whether the row set is an update set.
	/// </summary>
	/// <returns>
	///	The available generation modes in display order.
	/// </returns>
	private List<GenerationModeOption> CreateAvailableModes()
	{
		if (_isUpdate && (IsGeneratedOnlyBySqlServer() || Column.IsPrimaryKey))
		{
			return [GenerationModeOption.Get(ValueGenerationMode.KeepCurrent)];
		}

		if (IsGeneratedOnlyBySqlServer())
		{
			return [GenerationModeOption.Get(ValueGenerationMode.DatabaseGenerated)];
		}

		HashSet<ValueGenerationMode> modes = [ValueGenerationMode.Fixed];

		if (_isUpdate)
		{
			_ = modes.Add(ValueGenerationMode.KeepCurrent);
		}

		if (Category != SqlTypeCategory.Unsupported)
		{
			_ = modes.Add(ValueGenerationMode.Random);
		}

		if (Category is SqlTypeCategory.Integer or SqlTypeCategory.Decimal or SqlTypeCategory.Text)
		{
			_ = modes.Add(ValueGenerationMode.Sequence);
		}

		if (Category != SqlTypeCategory.Binary)
		{
			_ = modes.Add(ValueGenerationMode.Pattern);
			_ = modes.Add(ValueGenerationMode.Regex);
		}

		if (Category != SqlTypeCategory.Unsupported)
		{
			_ = modes.Add(ValueGenerationMode.CopyColumn);
			_ = modes.Add(ValueGenerationMode.TableLookup);
		}

		if (Reference is not null)
		{
			_ = modes.Add(ValueGenerationMode.ExistingForeignKey);

			// A table cannot take keys from its own rows while they are being inserted, but it can once they are in.
			if (!IsSelfReference || _isUpdate)
			{
				_ = modes.Add(ValueGenerationMode.GeneratedForeignKey);
			}
		}

		// UPDATE has no column to leave out, so SQL Server cannot supply the value.
		if (Column.HasDefault && !_isUpdate)
		{
			_ = modes.Add(ValueGenerationMode.DatabaseGenerated);
		}

		if (Column.IsNullable)
		{
			_ = modes.Add(ValueGenerationMode.Null);
		}

		return [.. GenerationModeOption.ALL_OPTIONS.Where(option => modes.Contains(option.Mode))];
	}

	/// <summary>
	///	Chooses the safest initial generation mode for the column metadata.
	/// </summary>
	/// <returns>
	///	The default generation mode for the column.
	/// </returns>
	private ValueGenerationMode GetDefaultMode()
	{
		if (_isUpdate)
		{
			return ValueGenerationMode.KeepCurrent;
		}

		if (IsGeneratedOnlyBySqlServer())
		{
			return ValueGenerationMode.DatabaseGenerated;
		}

		if (Reference is not null)
		{
			return
				IsSelfReference
					? Column.IsNullable
						? ValueGenerationMode.Null
						: ValueGenerationMode.ExistingForeignKey
					: ValueGenerationMode.GeneratedForeignKey;
		}

		if (Column.IsPrimaryKey && Category is SqlTypeCategory.Integer or SqlTypeCategory.Decimal or SqlTypeCategory.Text)
		{
			return ValueGenerationMode.Sequence;
		}

		if (Category != SqlTypeCategory.Unsupported)
		{
			return ValueGenerationMode.Random;
		}

		return
			Column.IsNullable
				? ValueGenerationMode.Null
				: Column.HasDefault
					? ValueGenerationMode.DatabaseGenerated
					: ValueGenerationMode.Fixed;
	}

	/// <summary>
	///	Checks whether SQL Server must provide the column value because the application cannot generate it directly.
	/// </summary>
	/// <returns>
	///	<see langword="true"/> for identity, computed and rowversion columns; otherwise <see langword="false"/>.
	/// </returns>
	private bool IsGeneratedOnlyBySqlServer()
		=> Column.IsIdentity || Column.IsComputed || Category == SqlTypeCategory.RowVersion;

	/// <summary>
	///	Describes which SQL Server mechanism supplies the column value.
	/// </summary>
	/// <returns>
	///	The user-facing explanation for database-generated values.
	/// </returns>
	private string DescribeDatabaseGeneratedValue()
		=>
			Column.IsIdentity
				? "Identity column: SQL Server numbers the rows"
				: Column.IsComputed
					? "Computed column: SQL Server calculates the value"
					: Category == SqlTypeCategory.RowVersion
						? "rowversion column: SQL Server stamps every row"
						: "SQL Server uses the column's default value";

	/// <summary>
	///	Describes why an update set leaves the column unchanged.
	/// </summary>
	/// <returns>
	///	The user-facing explanation for keeping the current value.
	/// </returns>
	private string DescribeKeptValue()
		=>
			!_isUpdate || CanChangeMode
				? "Not changed: the row keeps its current value"
				: IsGeneratedOnlyBySqlServer()
					? $"Not changed ({DescribeDatabaseGeneratedValue()})"
					: "Not changed: update sets find their rows by the primary key, so it keeps its value";

	/// <summary>
	///	Formats the table used by the foreign-key generation modes.
	/// </summary>
	/// <returns>
	///	The referenced schema and table, or a generic referenced-table label when there is no reference.
	/// </returns>
	private string DescribeReferencedTable()
		=>
			Reference is null
				? "the referenced table"
				: $"{Reference.ReferencedSchema}.{Reference.ReferencedTable}";

	/// <summary>
	///	Builds the multi-line tooltip description for the column metadata and key relationships.
	/// </summary>
	/// <returns>
	///	The column description shown in the UI.
	/// </returns>
	private string BuildColumnDescription()
	{
		StringBuilder builder = new();

		_ = builder.Append($"[{Column.Name}]  {SqlTypeDisplay}, {(Column.IsNullable ? "allows NULL" : "NOT NULL")}");

		if (Column.IsPrimaryKey)
		{
			_ = builder.AppendLine().Append("Primary key");
		}

		if (Column.IsIdentity)
		{
			_ = builder.AppendLine().Append("Identity: SQL Server generates the value");
		}

		if (Column.IsComputed)
		{
			_ = builder.AppendLine().Append("Computed: SQL Server calculates the value");
		}

		if (Column.HasDefault)
		{
			_ = builder.AppendLine().Append("Has a default value");
		}

		if (Reference is not null)
		{
			_ = builder.AppendLine().Append(
				Reference.IsInferred
					? $"Foreign table key (naming convention) → {Reference.ReferencedDisplayName}"
					: $"Foreign key → {Reference.ReferencedDisplayName}"
			);
		}
		else if (Column.IsForeignTableKey)
		{
			_ = builder.AppendLine().Append("Name ends with FTK, but no matching key column (…PK, …TK or …_tk) was found in another table of this database");
		}

		return builder.ToString();
	}

	/// <summary>
	///	Maps a SQL type category to the text editor kind used for fixed values.
	/// </summary>
	/// <param name="category">
	///	The SQL type category of the column.
	/// </param>
	/// <returns>
	///	The text input kind that best matches the category.
	/// </returns>
	private static TextInputKind GetFixedValueInputKind(SqlTypeCategory category) => category switch
	{
		SqlTypeCategory.Integer  => TextInputKind.Integer,
		SqlTypeCategory.Decimal  => TextInputKind.Decimal,
		SqlTypeCategory.Boolean  => TextInputKind.Boolean,
		SqlTypeCategory.DateTime => TextInputKind.DateTime,
		SqlTypeCategory.Time     => TextInputKind.Time,
		SqlTypeCategory.Guid     => TextInputKind.Guid,
		SqlTypeCategory.Binary   => TextInputKind.Hexadecimal,
		_                        => TextInputKind.Any
	};

	/// <summary>
	///	Tries to parse a decimal number using invariant-culture number and exponent formats.
	/// </summary>
	/// <param name="text">
	///	The text to parse after trimming.
	/// </param>
	/// <param name="value">
	///	The parsed decimal value, or zero when parsing fails.
	/// </param>
	/// <returns>
	///	<see langword="true"/> when the text is a valid number; otherwise <see langword="false"/>.
	/// </returns>
	private static bool TryParseNumber(string text, out decimal value)
		=> decimal.TryParse(text.Trim(), NumberStyles.Number | NumberStyles.AllowExponent, INVARIANT, out value);

	/// <summary>
	///	Checks whether an exception represents invalid user-entered data rather than an unexpected failure.
	/// </summary>
	/// <param name="exception">
	///	The exception raised while converting or generating a value.
	/// </param>
	/// <returns>
	///	<see langword="true"/> when the exception can be shown as a validation problem; otherwise <see langword="false"/>.
	/// </returns>
	private static bool IsValueProblem(Exception exception)
		=> exception is FormatException
			or OverflowException
			or InvalidOperationException
			or ArgumentException
			or NotSupportedException;

	/// <summary>
	///	The values of the other columns of one preview row.
	/// </summary>
	private sealed class SampleRowValues : IRowValueLookup
	{
		private readonly ColumnRuleViewModel _owner;
		private readonly long                _rowIndex;

		/// <summary>
		///	Creates a preview lookup for the other column values in one row.
		/// </summary>
		/// <param name="owner">
		///	The rule whose preview is being generated.
		/// </param>
		/// <param name="rowIndex">
		///	The zero-based preview row index being generated.
		/// </param>
		public SampleRowValues(ColumnRuleViewModel owner, long rowIndex)
		{
			_owner    = owner;
			_rowIndex = rowIndex;
		}

		/// <summary>
		///	Gets the preview value of another column in the same row.
		/// </summary>
		/// <param name="columnName">
		///	The name of the column whose value is needed.
		/// </param>
		/// <returns>
		///	The other column's preview value, or <see langword="null"/> when that rule generates a database null.
		/// </returns>
		/// <exception cref="SampleUnavailableException">
		///	Thrown when the preview cannot know the requested value yet.
		/// </exception>
		/// <exception cref="InvalidOperationException">
		///	Thrown when the requested column does not exist in the row set.
		/// </exception>
		public object? GetValue(string columnName)
		{
			ColumnRuleViewModel? rule = _owner.FindSiblingRule(columnName);

			if (rule is null)
			{
				// Rules that are not in a row set (yet) cannot see other columns, so only the shape of the value is shown.
				return
					_owner._siblingRules.Count == 0
						? throw new SampleUnavailableException($"(uses [{columnName}])")
						: throw new InvalidOperationException($"The table has no column named [{columnName}].");
			}

			return rule.GenerateSampleForOtherColumn(_rowIndex);
		}
	}

	/// <summary>
	///	The preview cannot show a value because it depends on a value that is only known while generating.
	/// </summary>
	private sealed class SampleUnavailableException : InvalidOperationException
	{
		/// <summary>
		///	Creates an exception that explains why a preview sample is unavailable but generation can continue.
		/// </summary>
		/// <param name="message">
		///	The explanation shown in the preview.
		/// </param>
		public SampleUnavailableException(string message)
			: base(message)
		{
		}
	}
}