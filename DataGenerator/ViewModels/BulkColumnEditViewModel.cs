using System.Collections.ObjectModel;
using System.Collections.Specialized;
using DataGenerator.Infrastructure;
using DataGenerator.Models;

namespace DataGenerator.ViewModels;

/// <summary>
///	Changes the generation mode or the Settings value of several selected columns of a row set at once. Columns that
///	cannot take the change keep what they had, and the result says which were skipped and why.
/// </summary>
public sealed class BulkColumnEditViewModel : ObservableObject
{
	#region FIELDS
	#region PUBLIC
	public const int MINIMUM_SELECTION_COUNT = 2;
	#endregion PUBLIC

	#region PRIVATE
	private const int MAXIMUM_NAMED_SKIPS = 3;

	private readonly IReadOnlyList<ColumnRuleViewModel> _allRules;

	private IReadOnlyList<BulkModeOption> _modeOptions;
	private BulkModeOption?               _selectedModeOption;
	private string                        _settingValue;
	private string?                       _resultText;
	private string?                       _resultDetails;
	private bool                          _resultHasSkips;
	#endregion PRIVATE
	#endregion FIELDS

	#region PROPERTIES
	#region PUBLIC
	/// <summary>
	///	The columns selected in the grid, kept in step with the grid's selection.
	/// </summary>
	public ObservableCollection<ColumnRuleViewModel> SelectedRules { get; }

	public RelayCommand ApplyModeCommand         { get; }
	public RelayCommand ApplySettingValueCommand { get; }
	public RelayCommand SelectAllCommand         { get; }
	public RelayCommand ClearSelectionCommand    { get; }

	/// <summary>
	///	Whether enough columns are selected for the bulk tools to be shown.
	/// </summary>
	public bool IsActive => SelectedRules.Count >= MINIMUM_SELECTION_COUNT;

	public string SelectionText => $"{SelectedRules.Count:N0} columns selected";

	/// <summary>
	///	The modes at least one selected column can use, in the usual order.
	/// </summary>
	public IReadOnlyList<BulkModeOption> ModeOptions
	{
		get         => _modeOptions;
		private set => SetProperty(ref _modeOptions, value);
	}

	public BulkModeOption? SelectedModeOption
	{
		get => _selectedModeOption;
		set
		{
			if (SetProperty(ref _selectedModeOption, value))
			{
				ApplyModeCommand.NotifyCanExecuteChanged();
			}
		}
	}

	/// <summary>
	///	The value put into the Settings cell of each selected column, for the mode that column uses.
	/// </summary>
	public string SettingValue
	{
		get => _settingValue;
		set => SetProperty(ref _settingValue, value ?? string.Empty);
	}

	/// <summary>
	///	What the last bulk change did, or <see langword="null"/> before the first change of the selection.
	/// </summary>
	public string? ResultText
	{
		get => _resultText;
		private set
		{
			if (SetProperty(ref _resultText, value))
			{
				OnPropertyChanged(nameof(HasResult));
			}
		}
	}

	/// <summary>
	///	Every skipped column with the reason it was skipped, one per line.
	/// </summary>
	public string? ResultDetails
	{
		get         => _resultDetails;
		private set => SetProperty(ref _resultDetails, value);
	}

	public bool HasResult => _resultText is not null;

	public bool ResultHasSkips
	{
		get         => _resultHasSkips;
		private set => SetProperty(ref _resultHasSkips, value);
	}
	#endregion PUBLIC
	#endregion PROPERTIES

	#region CONSTRUCTORS
	#region PUBLIC
	/// <summary>
	///	Creates the bulk editor for all rules in the active row set and tracks the grid selection.
	/// </summary>
	/// <param name="allRules">
	///	All column rules that can be selected for bulk changes.
	/// </param>
	/// <exception cref="ArgumentNullException">
	///	Thrown when <paramref name="allRules"/> is <see langword="null"/>.
	/// </exception>
	public BulkColumnEditViewModel(IReadOnlyList<ColumnRuleViewModel> allRules)
	{
		SelectedRules = [];

		_allRules           = allRules ?? throw new ArgumentNullException(nameof(allRules));
		_modeOptions        = [];
		_selectedModeOption = null;
		_settingValue       = string.Empty;
		_resultText         = null;
		_resultDetails      = null;
		_resultHasSkips     = false;

		ApplyModeCommand         = new RelayCommand(_ => ApplyMode(), _ => IsActive && _selectedModeOption is not null);
		ApplySettingValueCommand = new RelayCommand(_ => ApplySettingValue(), _ => IsActive);
		SelectAllCommand         = new RelayCommand(_ => SelectAll(), _ => SelectedRules.Count < _allRules.Count);
		ClearSelectionCommand    = new RelayCommand(_ => SelectedRules.Clear(), _ => SelectedRules.Count > 0);

		SelectedRules.CollectionChanged += OnSelectedRulesChanged;
	}
	#endregion PUBLIC
	#endregion CONSTRUCTORS

	#region METHODS
	#region PRIVATE
	/// <summary>
	///	Refreshes the bulk action state after the grid selection changes.
	/// </summary>
	/// <param name="sender">
	///	The selected rules collection that raised the event.
	/// </param>
	/// <param name="e">
	///	The change made to the selected rules collection.
	/// </param>
	private void OnSelectedRulesChanged(object? sender, NotifyCollectionChangedEventArgs e)
	{
		OnPropertyChanged(nameof(IsActive));
		OnPropertyChanged(nameof(SelectionText));
		ClearResult();
		RefreshModeOptions();
		ApplyModeCommand.NotifyCanExecuteChanged();
		ApplySettingValueCommand.NotifyCanExecuteChanged();
		SelectAllCommand.NotifyCanExecuteChanged();
		ClearSelectionCommand.NotifyCanExecuteChanged();
	}

	/// <summary>
	///	Builds the generation-mode choices supported by at least one selected column and keeps the previous choice when
	///	it is still available.
	/// </summary>
	private void RefreshModeOptions()
	{
		List<BulkModeOption> options = [];

		foreach (GenerationModeOption option in GenerationModeOption.ALL_OPTIONS)
		{
			int supportedCount =
				SelectedRules
					.Count(rule => rule.IsModeAvailable(option.Mode));

			if (supportedCount > 0)
			{
				options.Add(new BulkModeOption(option, supportedCount, SelectedRules.Count));
			}
		}

		ValueGenerationMode? selectedMode = _selectedModeOption?.Mode;

		ModeOptions        = options;
		SelectedModeOption = options.FirstOrDefault(option => option.Mode == selectedMode);
	}

	/// <summary>
	///	Applies the selected generation mode to every selected column that can use it and reports any columns left
	///	unchanged.
	/// </summary>
	private void ApplyMode()
	{
		if (_selectedModeOption is null)
		{
			return;
		}

		ValueGenerationMode                       mode    = _selectedModeOption.Mode;
		List<(string ColumnName, string Problem)> skipped = [];
		int                                       changed = 0;

		foreach (ColumnRuleViewModel rule in SelectedRules)
		{
			string? problem = rule.DescribeModeProblem(mode);

			if (problem is null)
			{
				rule.GenerationMode = mode;
				++changed;
			}
			else
			{
				skipped.Add((rule.Name, problem));
			}
		}

		ShowResult($"{changed:N0} of {SelectedRules.Count:N0} columns now use {_selectedModeOption.Option.DisplayName}.", skipped);
		ApplySettingValueCommand.NotifyCanExecuteChanged();
	}

	/// <summary>
	///	Applies the typed Settings value to every selected column and reports any values that were rejected.
	/// </summary>
	private void ApplySettingValue()
	{
		List<(string ColumnName, string Problem)> skipped = [];
		int                                       changed = 0;

		foreach (ColumnRuleViewModel rule in SelectedRules)
		{
			if (rule.TrySetSettingValue(_settingValue, out string? problem))
			{
				++changed;
			}
			else
			{
				skipped.Add((rule.Name, problem ?? "the value does not suit it"));
			}
		}

		ShowResult($"Set the value of {changed:N0} of {SelectedRules.Count:N0} columns.", skipped);
	}

	/// <summary>
	///	Adds every rule in the row set to the current selection.
	/// </summary>
	private void SelectAll()
	{
		foreach (ColumnRuleViewModel rule in _allRules)
		{
			if (!SelectedRules.Contains(rule))
			{
				SelectedRules.Add(rule);
			}
		}
	}

	/// <summary>
	///	Shows the outcome of a bulk action and stores the detailed skip reasons for the tooltip.
	/// </summary>
	/// <param name="summary">
	///	The main result text to show when no selected columns were skipped.
	/// </param>
	/// <param name="skipped">
	///	The selected columns left unchanged, with the reason for each one.
	/// </param>
	private void ShowResult(string summary, IReadOnlyList<(string ColumnName, string Problem)> skipped)
	{
		ResultHasSkips = skipped.Count > 0;
		ResultDetails  =
			skipped.Count > 0
				? string.Join(
					Environment.NewLine,
					skipped
						.Select(skip => $"{skip.ColumnName}: {skip.Problem}")
				)
				: null;

		if (skipped.Count == 0)
		{
			ResultText = summary;
			return;
		}

		string nameList   = string.Join(
			", ",
			skipped
				.Take(MAXIMUM_NAMED_SKIPS)
				.Select(skip => skip.ColumnName)
		);
		int    otherCount = skipped.Count - MAXIMUM_NAMED_SKIPS;

		ResultText = $"{summary} Kept as they were: {nameList}"
			+ (otherCount > 0 ? $" and {otherCount:N0} more" : string.Empty)
			+ " (hover for why).";
	}

	/// <summary>
	///	Removes the result text and skip details for the previous bulk action.
	/// </summary>
	private void ClearResult()
	{
		ResultText     = null;
		ResultDetails  = null;
		ResultHasSkips = false;
	}
	#endregion PRIVATE
	#endregion METHODS
}