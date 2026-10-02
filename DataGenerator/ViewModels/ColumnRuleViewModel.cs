using System.Globalization;
using System.Text;
using DataGenerator.Infrastructure;
using DataGenerator.Models;
using DataGenerator.Services;

namespace DataGenerator.ViewModels;

/// <summary>
/// How one column is generated within one row set. Only the settings of the selected mode are shown and validated.
/// </summary>
public sealed class ColumnRuleViewModel : ValidatableObservableObject
{
	private const int    PREVIEW_SAMPLE_COUNT = 3;
	private const string PREVIEW_SEPARATOR    = "  ·  ";
	private const string DEFAULT_SEQUENCE     = "1";
	private const char   SEQUENCE_SEPARATOR   = ';';

	private static readonly CultureInfo INVARIANT = CultureInfo.InvariantCulture;

	private readonly ColumnRuleServices _services;

	private ValueGenerationMode               _generationMode;
	private string                            _fixedValue        = string.Empty;
	private string                            _sequenceStartText = DEFAULT_SEQUENCE;
	private string                            _sequenceStepText  = DEFAULT_SEQUENCE;
	private string                            _regexPattern      = string.Empty;
	private string                            _patternExpression = string.Empty;
	private string                            _sourceColumnName  = string.Empty;
	private IReadOnlyList<ColumnRuleViewModel> _siblingRules       = [];
	private bool                              _isGeneratingSample;
	private string?                           _previewText;
	private bool                              _previewIsError;
	private string?                           _appliedSettingName;
	private bool                              _isApplyingSetting;
	private bool                              _isSavedSettingsMenuOpen;
	private IReadOnlyList<SavedSettingOption> _savedSettingOptions = [];

	public ColumnRuleViewModel(
		string             tableName,
		ColumnModel        column,
		ForeignKeyModel?   reference,
		bool               isSelfReference,
		ColumnRuleServices services
	)
	{
		TableName       = tableName ?? throw new ArgumentNullException(nameof(tableName));
		Column          = column ?? throw new ArgumentNullException(nameof(column));
		_services       = services ?? throw new ArgumentNullException(nameof(services));
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

		Validate();
	}

	/// <summary>
	/// Raised when the mode or a setting of the mode changes, so that columns which use this column's value can update.
	/// </summary>
	public event EventHandler? SettingsChanged;

	/// <summary>
	/// The schema.table the column belongs to.
	/// </summary>
	public string TableName { get; }

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
	/// "FTK" badge for naming-convention keys that are not also declared foreign keys.
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
	/// The preset whose pattern is the current <see cref="RegexPattern"/>, if any. Choosing a preset copies its pattern.
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
	/// The column of the same row whose value the Copy of column mode copies.
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
	/// The other columns of the row set, which the Copy of column mode can copy.
	/// </summary>
	public IReadOnlyList<string> CopySourceOptions
		=> [.. _siblingRules.Where(rule => !ReferenceEquals(rule, this)).Select(rule => rule.Name)];

	/// <summary>
	/// Whether the column uses the value of another column (Copy of column, or COL(...) in a pattern).
	/// </summary>
	public bool UsesOtherColumns => _services.ValueGenerator.GetReferencedColumns(CreateRule()).Count > 0;

	/// <summary>
	/// Explanation shown instead of an editor for modes that have no settings.
	/// </summary>
	public string ModeSummary => _generationMode switch
	{
		ValueGenerationMode.Random              => _services.ValueGenerator.DescribeRandomValues(Column),
		ValueGenerationMode.DatabaseGenerated   => DescribeDatabaseGeneratedValue(),
		ValueGenerationMode.Null                => "NULL in every row",
		ValueGenerationMode.GeneratedForeignKey => $"Random key of the {DescribeReferencedTable()} rows generated in this run",
		ValueGenerationMode.ExistingForeignKey  => $"Random key that already exists in {DescribeReferencedTable()}",
		_                                       => string.Empty
	};

	/// <summary>
	/// A few sample values (or the problem that prevents generating them). Built on first use because previews of
	/// random values are only needed for rows that are on screen.
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
	/// The validation message of the selected mode, or <see langword="null"/> when its settings are valid.
	/// </summary>
	public string? ValidationError => _generationMode switch
	{
		ValueGenerationMode.Fixed      => GetError(nameof(FixedValue)),
		ValueGenerationMode.Sequence   => GetError(nameof(SequenceStartText)) ?? GetError(nameof(SequenceStepText)),
		ValueGenerationMode.Regex      => GetError(nameof(RegexPattern)),
		ValueGenerationMode.Pattern    => GetError(nameof(PatternExpression)),
		ValueGenerationMode.CopyColumn => GetError(nameof(SourceColumnName)),
		_                              => null
	};

	/// <summary>
	/// The saved setting the column uses, or <see langword="null"/> once its settings were changed by hand.
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
		=> _appliedSettingName is null
			? "Saved settings: save this column's mode and settings for reuse, or apply settings you or a colleague saved earlier."
			: $"Uses the saved setting '{_appliedSettingName}'. Click to save, apply or manage saved settings.";

	/// <summary>
	/// Whether the saved-settings menu is open. Opening it lists the saved settings that suit the column.
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
	/// The saved settings whose mode the column supports; the ones saved for a column with the same name come first.
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

	public bool IsModeAvailable(ValueGenerationMode mode) => AvailableModes.Any(option => option.Mode == mode);

	/// <summary>
	/// Whether the current mode has a value in the Settings cell that can be typed (fixed value, sequence, pattern, …).
	/// </summary>
	public bool HasEditableSetting => _generationMode is ValueGenerationMode.Fixed
		or ValueGenerationMode.Sequence
		or ValueGenerationMode.Regex
		or ValueGenerationMode.Pattern
		or ValueGenerationMode.CopyColumn;

	/// <summary>
	/// Why the column cannot use a mode, or <see langword="null"/> when it can.
	/// </summary>
	public string? DescribeModeProblem(ValueGenerationMode mode)
	{
		if (mode == _generationMode || IsModeAvailable(mode))
		{
			return null;
		}

		if (!CanChangeMode)
		{
			return $"{DescribeDatabaseGeneratedValue()}, so its mode cannot be changed";
		}

		return mode switch
		{
			ValueGenerationMode.Null                => "it does not allow NULL",
			ValueGenerationMode.ExistingForeignKey  => "it does not refer to another table",
			ValueGenerationMode.GeneratedForeignKey => Reference is null
				? "it does not refer to another table"
				: "it refers to its own table, whose keys are not known while its rows are inserted",
			ValueGenerationMode.DatabaseGenerated   => "it has no default value for SQL Server to use",
			_                                       => $"{GenerationModeOption.Get(mode).DisplayName} does not suit {SqlTypeDisplay}"
		};
	}

	/// <summary>
	/// Puts a value into the Settings cell of the current mode: the fixed value, the regular expression, the pattern, the
	/// copied column, or the sequence start (optionally followed by a semicolon and the step, e.g. 100; 5). When the
	/// value is not valid for the column, its settings are put back as they were and the problem is returned.
	/// </summary>
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
				FixedValue = value;
				break;

			case ValueGenerationMode.Sequence:
				string[] parts = value.Split(SEQUENCE_SEPARATOR, 2);

				SequenceStartText = parts[0].Trim();

				if (parts.Length == 2)
				{
					SequenceStepText = parts[1].Trim();
				}

				break;

			case ValueGenerationMode.Regex:
				RegexPattern = value;
				break;

			case ValueGenerationMode.Pattern:
				PatternExpression = value;
				break;

			case ValueGenerationMode.CopyColumn:
				SourceColumnName = FindSiblingRule(value)?.Name ?? value.Trim();
				break;
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
	/// Uses the mode and settings saved for this column in a set configuration. When the column cannot use the mode, or
	/// the saved value is not valid for it, its settings are put back as they were and the problem is returned.
	/// </summary>
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
	/// Uses the mode and settings of a saved setting. Settings of other modes keep their values.
	/// </summary>
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
					FixedValue = setting.FixedValue;
					break;

				case ValueGenerationMode.Sequence:
					SequenceStartText = setting.SequenceStart;
					SequenceStepText  = setting.SequenceStep;
					break;

				case ValueGenerationMode.Regex:
					RegexPattern = setting.RegexPattern;
					break;

				case ValueGenerationMode.Pattern:
					PatternExpression = setting.PatternExpression;
					break;

				case ValueGenerationMode.CopyColumn:
					SourceColumnName = setting.SourceColumnName;
					break;
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
	/// Uses the saved setting that applies automatically to this column, if there is one.
	/// Returns whether the column changed.
	/// </summary>
	public bool ApplyAutomaticSetting()
	{
		SavedColumnSetting? setting = _services.SavedSettings.FindAutomatic(TableName, Column.Name);

		if (setting is null || (setting.Name == _appliedSettingName && setting.HasSameValues(CaptureSetting())))
		{
			return false;
		}

		return ApplySetting(setting);
	}

	/// <summary>
	/// Updates <see cref="AppliedSettingName"/> after saved settings were renamed, changed or deleted.
	/// </summary>
	public void RefreshAppliedSetting()
	{
		if (_appliedSettingName is not null)
		{
			AppliedSettingName = _services.SavedSettings.FindEquivalent(CaptureSetting(), _appliedSettingName)?.Name;
		}
	}

	/// <summary>
	/// The mode and the settings of that mode, ready to be saved.
	/// </summary>
	public SavedColumnSetting CaptureSetting() => new SavedColumnSetting
	{
		GenerationMode    = _generationMode,
		FixedValue        = _generationMode == ValueGenerationMode.Fixed ? _fixedValue : string.Empty,
		SequenceStart     = _generationMode == ValueGenerationMode.Sequence ? _sequenceStartText.Trim() : DEFAULT_SEQUENCE,
		SequenceStep      = _generationMode == ValueGenerationMode.Sequence ? _sequenceStepText.Trim() : DEFAULT_SEQUENCE,
		RegexPattern      = _generationMode == ValueGenerationMode.Regex ? _regexPattern : string.Empty,
		PatternExpression = _generationMode == ValueGenerationMode.Pattern ? _patternExpression : string.Empty,
		SourceColumnName  = _generationMode == ValueGenerationMode.CopyColumn ? _sourceColumnName.Trim() : string.Empty,
		TableName         = TableName,
		ColumnName        = Column.Name
	};

	public void CopyFrom(ColumnRuleViewModel source)
	{
		ArgumentNullException.ThrowIfNull(source);

		FixedValue        = source.FixedValue;
		SequenceStartText = source.SequenceStartText;
		SequenceStepText  = source.SequenceStepText;
		RegexPattern      = source.RegexPattern;
		PatternExpression = source.PatternExpression;
		SourceColumnName  = source.SourceColumnName;
		GenerationMode    = source.GenerationMode;

		AppliedSettingName = source.AppliedSettingName;
	}

	public void RefreshPreview()
	{
		_previewText = null;
		OnPropertyChanged(nameof(PreviewText));
		OnPropertyChanged(nameof(PreviewIsError));
	}

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
		Reference         = Reference
	};

	/// <summary>
	/// Gives the rule access to the other columns of its row set, for the Copy of column mode and COL(...) in patterns.
	/// </summary>
	public void AttachToRowSet(IReadOnlyList<ColumnRuleViewModel> rowSetRules)
	{
		_siblingRules = rowSetRules ?? throw new ArgumentNullException(nameof(rowSetRules));
		OnPropertyChanged(nameof(CopySourceOptions));
		Validate();
		RefreshPreview();
	}

	/// <summary>
	/// Validates the rule again after another column of the row set changed, when the rule uses other columns.
	/// </summary>
	public void RefreshAfterOtherColumnChanged()
	{
		if (UsesOtherColumns)
		{
			Validate();
			RefreshPreview();
		}
	}

	protected override void OnErrorsChanged()
	{
		OnPropertyChanged(nameof(ValidationError));
		SaveSettingsCommand.NotifyCanExecuteChanged();
	}

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

	private void LoadSavedSettingOptions()
		=> SavedSettingOptions =
		[
			.. _services.SavedSettings.Settings
				.Where(setting => IsModeAvailable(setting.GenerationMode))
				.Select(setting => new SavedSettingOption(setting, string.Equals(setting.ColumnName, Column.Name, StringComparison.OrdinalIgnoreCase)))
				.OrderByDescending(option => option.IsSavedForColumn)
		];

	private void SaveSettings()
	{
		IsSavedSettingsMenuOpen = false;

		SaveColumnSettingViewModel dialog = new SaveColumnSettingViewModel(CaptureSetting(), _services.SavedSettings, _appliedSettingName);

		if (_services.SavedSettingsWindows.ShowSaveDialog(dialog))
		{
			AppliedSettingName = dialog.SavedName;
		}
	}

	private void ApplySavedSetting(object? parameter)
	{
		IsSavedSettingsMenuOpen = false;

		if (parameter is SavedSettingOption option)
		{
			_ = ApplySetting(option.Setting);
		}
	}

	private void ManageSavedSettings()
	{
		IsSavedSettingsMenuOpen = false;
		_services.SavedSettingsWindows.ShowManager();
	}

	/// <summary>
	/// The mode and the values of every mode, so they can all be put back after a change that is undone.
	/// </summary>
	private SavedColumnSetting CaptureAllValues() => new SavedColumnSetting
	{
		GenerationMode    = _generationMode,
		FixedValue        = _fixedValue,
		SequenceStart     = _sequenceStartText,
		SequenceStep      = _sequenceStepText,
		RegexPattern      = _regexPattern,
		PatternExpression = _patternExpression,
		SourceColumnName  = _sourceColumnName,
		TableName         = TableName,
		ColumnName        = Column.Name
	};

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
			GenerationMode    = values.GenerationMode;
		}
		finally
		{
			_isApplyingSetting = false;
		}
	}

	private void Validate()
	{
		SetError(nameof(FixedValue), _generationMode == ValueGenerationMode.Fixed ? ValidateFixedValue() : null);
		SetError(nameof(SequenceStartText), _generationMode == ValueGenerationMode.Sequence ? ValidateSequenceStart() : null);
		SetError(nameof(SequenceStepText), _generationMode == ValueGenerationMode.Sequence ? ValidateSequenceStep() : null);
		SetError(nameof(RegexPattern), _generationMode == ValueGenerationMode.Regex ? ValidateRegexPattern() : null);
		SetError(nameof(PatternExpression), _generationMode == ValueGenerationMode.Pattern ? ValidatePatternExpression() : null);
		SetError(nameof(SourceColumnName), _generationMode == ValueGenerationMode.CopyColumn ? ValidateSourceColumn() : null);
		OnPropertyChanged(nameof(ValidationError));
	}

	private string? ValidateSourceColumn()
		=> string.IsNullOrWhiteSpace(_sourceColumnName)
			? "Choose the column whose value is copied."
			: ValidateReferencedColumns() ?? TryGenerateSample(0, out _);

	/// <summary>
	/// Checks that the columns used by Copy of column or COL(...) exist in the row set and have a value before the row is inserted.
	/// </summary>
	private string? ValidateReferencedColumns()
	{
		foreach (string columnName in _services.ValueGenerator.GetReferencedColumns(CreateRule()))
		{
			ColumnRuleViewModel? referencedRule = FindSiblingRule(columnName);

			if (referencedRule is null)
			{
				return _siblingRules.Count == 0
					? null
					: $"The table has no column named [{columnName}].";
			}

			if (ReferenceEquals(referencedRule, this))
			{
				return "A column cannot use its own value. Choose another column.";
			}

			if (referencedRule.GenerationMode == ValueGenerationMode.DatabaseGenerated)
			{
				return $"SQL Server generates [{referencedRule.Name}] while inserting the row, so its value is not known in advance. "
					+ $"Choose another column, or another mode for [{referencedRule.Name}].";
			}
		}

		return null;
	}

	private ColumnRuleViewModel? FindSiblingRule(string columnName)
		=> _siblingRules.FirstOrDefault(rule => string.Equals(rule.Name, columnName.Trim(), StringComparison.OrdinalIgnoreCase));

	private string? ValidateFixedValue()
	{
		if (_fixedValue.Length == 0 && Category != SqlTypeCategory.Text)
		{
			return $"Enter {AcceptedInputDescription}.";
		}

		return _services.Converter.TryConvertText(Column, _fixedValue, out _, out string errorMessage) ? null : errorMessage;
	}

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

	private string? ValidateSequenceStep()
	{
		if (!TryParseNumber(_sequenceStepText, out decimal step))
		{
			return "Enter the amount added for each row, e.g. 1. Use a negative number to count down.";
		}

		return Category == SqlTypeCategory.Integer && step != decimal.Truncate(step)
			? $"{SqlTypeDisplay} only stores whole numbers, so the step must be a whole number."
			: null;
	}

	private string? ValidateRegexPattern()
		=> string.IsNullOrWhiteSpace(_regexPattern)
			? "Enter a regular expression, e.g. [A-Z]{3}-[0-9]{4}, or pick a preset."
			: TryGenerateSample(0, out _);

	private string? ValidatePatternExpression()
	{
		if (string.IsNullOrWhiteSpace(_patternExpression))
		{
			return "Enter a pattern, e.g. P FOLLOWED BY SEQ(1-1000) FOLLOWED BY (X OR Y). Click ? for the pattern language reference.";
		}

		return _services.PatternGenerator.TryValidate(_patternExpression, out string errorMessage)
			? ValidateReferencedColumns() ?? TryGenerateSample(0, out _)
			: errorMessage;
	}

	private string BuildPreview()
	{
		_previewIsError = false;

		switch (_generationMode)
		{
			case ValueGenerationMode.DatabaseGenerated:
				return "(set by SQL Server)";

			case ValueGenerationMode.Null:
				return "NULL";

			case ValueGenerationMode.GeneratedForeignKey:
			case ValueGenerationMode.ExistingForeignKey:
				return $"(keys of {DescribeReferencedTable()})";
		}

		string? validationError = ValidationError;

		if (validationError is not null)
		{
			_previewIsError = true;
			return validationError;
		}

		int          sampleCount = _generationMode == ValueGenerationMode.Fixed ? 1 : PREVIEW_SAMPLE_COUNT;
		List<string> samples     = new List<string>(sampleCount);

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

	/// <remarks>
	/// When the value depends on a column whose value is only known while generating (e.g. a key), there is no error and
	/// <paramref name="value"/> is the <see cref="SampleUnavailableException"/> that explains why.
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
	/// A sample value for the preview; the values of other columns are generated with their own rules for the same row.
	/// </summary>
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
			return _services.ValueGenerator.Generate(CreateRule(), rowIndex, new SampleRowValues(this, rowIndex));
		}
		finally
		{
			_isGeneratingSample = false;
		}
	}

	private object? GenerateSampleForOtherColumn(long rowIndex)
	{
		switch (_generationMode)
		{
			case ValueGenerationMode.GeneratedForeignKey:
			case ValueGenerationMode.ExistingForeignKey:
				throw new SampleUnavailableException($"(uses the key in [{Name}], chosen while generating)");

			case ValueGenerationMode.DatabaseGenerated:
				throw new SampleUnavailableException($"(uses [{Name}], set by SQL Server)");
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

	private List<GenerationModeOption> CreateAvailableModes()
	{
		if (IsGeneratedOnlyBySqlServer())
		{
			return [GenerationModeOption.Get(ValueGenerationMode.DatabaseGenerated)];
		}

		HashSet<ValueGenerationMode> modes = [ValueGenerationMode.Fixed];

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
		}

		if (Reference is not null)
		{
			_ = modes.Add(ValueGenerationMode.ExistingForeignKey);

			// A table cannot take keys from its own rows while they are being inserted.
			if (!IsSelfReference)
			{
				_ = modes.Add(ValueGenerationMode.GeneratedForeignKey);
			}
		}

		if (Column.HasDefault)
		{
			_ = modes.Add(ValueGenerationMode.DatabaseGenerated);
		}

		if (Column.IsNullable)
		{
			_ = modes.Add(ValueGenerationMode.Null);
		}

		return [.. GenerationModeOption.ALL_OPTIONS.Where(option => modes.Contains(option.Mode))];
	}

	private ValueGenerationMode GetDefaultMode()
	{
		if (IsGeneratedOnlyBySqlServer())
		{
			return ValueGenerationMode.DatabaseGenerated;
		}

		if (Reference is not null)
		{
			return IsSelfReference
				? Column.IsNullable ? ValueGenerationMode.Null : ValueGenerationMode.ExistingForeignKey
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

		return Column.IsNullable
			? ValueGenerationMode.Null
			: Column.HasDefault ? ValueGenerationMode.DatabaseGenerated : ValueGenerationMode.Fixed;
	}

	private bool IsGeneratedOnlyBySqlServer()
		=> Column.IsIdentity || Column.IsComputed || Category == SqlTypeCategory.RowVersion;

	private string DescribeDatabaseGeneratedValue()
	{
		if (Column.IsIdentity)
		{
			return "Identity column: SQL Server numbers the rows";
		}

		if (Column.IsComputed)
		{
			return "Computed column: SQL Server calculates the value";
		}

		return Category == SqlTypeCategory.RowVersion
			? "rowversion column: SQL Server stamps every row"
			: "SQL Server uses the column's default value";
	}

	private string DescribeReferencedTable()
		=> Reference is null
			? "the referenced table"
			: $"{Reference.ReferencedSchema}.{Reference.ReferencedTable}";

	private string BuildColumnDescription()
	{
		StringBuilder builder = new StringBuilder();

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
			_ = builder.AppendLine().Append("Name ends with FTK, but no matching …PK column was found in another table of this database");
		}

		return builder.ToString();
	}

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

	private static bool TryParseNumber(string text, out decimal value)
		=> decimal.TryParse(text.Trim(), NumberStyles.Number | NumberStyles.AllowExponent, INVARIANT, out value);

	private static bool IsValueProblem(Exception exception)
		=> exception is FormatException
			or OverflowException
			or InvalidOperationException
			or ArgumentException
			or NotSupportedException;

	/// <summary>
	/// The values of the other columns of one preview row.
	/// </summary>
	private sealed class SampleRowValues : IRowValueLookup
	{
		private readonly ColumnRuleViewModel _owner;
		private readonly long                _rowIndex;

		public SampleRowValues(ColumnRuleViewModel owner, long rowIndex)
		{
			_owner    = owner;
			_rowIndex = rowIndex;
		}

		public object? GetValue(string columnName)
		{
			ColumnRuleViewModel? rule = _owner.FindSiblingRule(columnName);

			if (rule is null)
			{
				// Rules that are not in a row set (yet) cannot see other columns, so only the shape of the value is shown.
				return _owner._siblingRules.Count == 0
					? throw new SampleUnavailableException($"(uses [{columnName}])")
					: throw new InvalidOperationException($"The table has no column named [{columnName}].");
			}

			return rule.GenerateSampleForOtherColumn(_rowIndex);
		}
	}

	/// <summary>
	/// The preview cannot show a value because it depends on a value that is only known while generating.
	/// </summary>
	private sealed class SampleUnavailableException : InvalidOperationException
	{
		public SampleUnavailableException(string message)
			: base(message)
		{
		}
	}
}