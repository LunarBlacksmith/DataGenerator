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

	private static readonly CultureInfo INVARIANT = CultureInfo.InvariantCulture;

	private readonly ColumnRuleServices _services;

	private ValueGenerationMode               _generationMode;
	private string                            _fixedValue        = string.Empty;
	private string                            _sequenceStartText = DEFAULT_SEQUENCE;
	private string                            _sequenceStepText  = DEFAULT_SEQUENCE;
	private string                            _regexPattern      = string.Empty;
	private string                            _patternExpression = string.Empty;
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
		ValueGenerationMode.Fixed    => GetError(nameof(FixedValue)),
		ValueGenerationMode.Sequence => GetError(nameof(SequenceStartText)) ?? GetError(nameof(SequenceStepText)),
		ValueGenerationMode.Regex    => GetError(nameof(RegexPattern)),
		ValueGenerationMode.Pattern  => GetError(nameof(PatternExpression)),
		_                            => null
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
		Reference         = Reference
	};

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

	private void Validate()
	{
		SetError(nameof(FixedValue), _generationMode == ValueGenerationMode.Fixed ? ValidateFixedValue() : null);
		SetError(nameof(SequenceStartText), _generationMode == ValueGenerationMode.Sequence ? ValidateSequenceStart() : null);
		SetError(nameof(SequenceStepText), _generationMode == ValueGenerationMode.Sequence ? ValidateSequenceStep() : null);
		SetError(nameof(RegexPattern), _generationMode == ValueGenerationMode.Regex ? ValidateRegexPattern() : null);
		SetError(nameof(PatternExpression), _generationMode == ValueGenerationMode.Pattern ? ValidatePatternExpression() : null);
		OnPropertyChanged(nameof(ValidationError));
	}

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
			? TryGenerateSample(0, out _)
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

			samples.Add(_services.Converter.FormatForDisplay(Column, value));
		}

		return string.Join(PREVIEW_SEPARATOR, samples);
	}

	private string? TryGenerateSample(long rowIndex, out object? value)
	{
		try
		{
			value = _services.ValueGenerator.Generate(CreateRule(), rowIndex);
			return null;
		}
		catch (Exception exception) when (IsValueProblem(exception))
		{
			value = null;
			return exception.Message;
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
}