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

	private string _name;
	private int    _rowCount;
	private int    _invalidRuleCount;

	public RowSetViewModel(string name, int rowCount, IReadOnlyList<ColumnRuleViewModel> columnRules)
	{
		ColumnRules = columnRules ?? throw new ArgumentNullException(nameof(columnRules));
		_name       = name ?? string.Empty;
		_rowCount   = ClampRowCount(rowCount);

		foreach (ColumnRuleViewModel rule in ColumnRules)
		{
			rule.ErrorsChanged += OnRuleErrorsChanged;
		}

		_invalidRuleCount = CountInvalidRules();
		ValidateName();
	}

	/// <summary>
	/// Raised when the number of rows or the validity of the row set changes.
	/// </summary>
	public event EventHandler? SettingsChanged;

	public IReadOnlyList<ColumnRuleViewModel> ColumnRules { get; }

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

	private int CountInvalidRules() => ColumnRules.Count(rule => rule.HasErrors);
}