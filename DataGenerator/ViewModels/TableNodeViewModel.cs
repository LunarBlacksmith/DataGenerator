using System.Collections.ObjectModel;
using System.Text;
using DataGenerator.Models;

namespace DataGenerator.ViewModels;

/// <summary>
/// A table in the explorer tree, with the row sets that describe the data to generate for it.
/// Row sets (and their column rules) are created on first use so large databases load quickly.
/// </summary>
public sealed class TableNodeViewModel : TreeNodeViewModel
{
	public const int DEFAULT_ROW_COUNT = 10;

	private const string ROW_SET_NAME_PREFIX    = "Set ";
	private const string UPDATE_SET_NAME_PREFIX = "Update ";
	private const string COPY_SUFFIX            = " (copy)";

	private readonly ColumnRuleFactory _ruleFactory;

	private RowSetViewModel? _selectedRowSet;
	private bool             _isIncluded;
	private int              _pendingRowCount = DEFAULT_ROW_COUNT;

	public TableNodeViewModel(TableModel model, DatabaseNodeViewModel database, ColumnRuleFactory ruleFactory)
	{
		Model        = model ?? throw new ArgumentNullException(nameof(model));
		Database     = database ?? throw new ArgumentNullException(nameof(database));
		_ruleFactory = ruleFactory ?? throw new ArgumentNullException(nameof(ruleFactory));
		ToolTipText  = BuildToolTipText();

		RowSets.CollectionChanged += (_, _) => OnRowSetsChanged();
	}

	/// <summary>
	/// Raised when inclusion, row counts or the validity of the rules change.
	/// </summary>
	public event EventHandler? GenerationSettingsChanged;

	public TableModel                            Model       { get; }
	public DatabaseNodeViewModel                 Database    { get; }
	public ObservableCollection<RowSetViewModel> RowSets     { get; } = [];
	public string                                ToolTipText { get; }

	public override string DisplayName => Model.DisplayName;

	public override bool IsDimmed => IsHidden || Database.IsHidden;

	public bool IsIncluded
	{
		get => _isIncluded;
		set
		{
			if (SetProperty(ref _isIncluded, value))
			{
				RaiseGenerationSettingsChanged();
			}
		}
	}

	public RowSetViewModel? SelectedRowSet
	{
		get => _selectedRowSet;
		set
		{
			// The row set tabs push null (or a row set of the next table) here while they switch to another table.
			if (value is null ? RowSets.Count > 0 : !RowSets.Contains(value))
			{
				return;
			}

			if (SetProperty(ref _selectedRowSet, value))
			{
				RefreshRowSetCommands?.Invoke();
			}
		}
	}

	/// <summary>
	/// Rows to generate. With several row sets (or an update set) this is the (read-only) total of the insert sets.
	/// Editing it also includes the table, because a row count only matters for included tables.
	/// </summary>
	public int RowCount
	{
		get => RowSets.Count switch
		{
			0 => _pendingRowCount,
			1 => CanEditRowCount ? RowSets[0].RowCount : TotalRowCount,
			_ => TotalRowCount
		};
		set
		{
			if (!CanEditRowCount)
			{
				return;
			}

			SetRowCountForAllSets(value);
			IsIncluded = true;
		}
	}

	/// <summary>
	/// New rows the insert sets generate. Update sets change existing rows, so they do not add to it.
	/// </summary>
	public int TotalRowCount
		=> RowSets.Count == 0
			? _pendingRowCount
			: RowSets.Where(rowSet => !rowSet.IsUpdate).Sum(rowSet => rowSet.RowCount);

	public int  UpdateSetCount     => RowSets.Count(rowSet => rowSet.IsUpdate);
	public bool HasUpdateSets      => RowSets.Any(rowSet => rowSet.IsUpdate);
	public bool CanEditRowCount    => RowSets.Count == 0 || (RowSets.Count == 1 && !RowSets[0].IsUpdate);
	public bool HasMultipleRowSets => RowSets.Count > 1;
	public bool HasRowSetSummary   => HasMultipleRowSets || HasUpdateSets;
	public bool CanRemoveRowSet    => RowSets.Count > 1 && _selectedRowSet is not null;
	public int  ColumnCount        => Model.Columns.Count;

	public string RowSetSummary
		=> !HasRowSetSummary
			? string.Empty
			: UpdateSetCount == 0
				? $"{RowSets.Count} sets"
				: $"{RowSets.Count} {(RowSets.Count == 1 ? "set" : "sets")}, {UpdateSetCount} update";

	public string RowCountToolTip
		=> CanEditRowCount
			? "Number of rows to generate for this table (1 to 1,000,000). Changing it also includes the table."
			: $"New rows of the {RowSets.Count - UpdateSetCount} insert set(s); update sets change existing rows instead. "
				+ "Edit each set's row count in the column rules panel.";

	public int InvalidRowSetCount => RowSets.Count(rowSet => !rowSet.IsValid);

	public bool HasInvalidRowSets => RowSets.Any(rowSet => !rowSet.IsValid);

	/// <summary>
	/// Lets the explorer refresh the add/duplicate/remove commands when the selected row set changes.
	/// </summary>
	internal Action? RefreshRowSetCommands { get; set; }

	public void EnsureRowSets()
	{
		if (RowSets.Count == 0)
		{
			AddRowSetCore(new RowSetViewModel(ROW_SET_NAME_PREFIX + "1", _pendingRowCount, _ruleFactory.CreateRules(Model)));
		}
	}

	public RowSetViewModel AddRowSet()
	{
		EnsureRowSets();

		RowSetViewModel rowSet = new(GetNextRowSetName(), DEFAULT_ROW_COUNT, _ruleFactory.CreateRules(Model));

		AddRowSetCore(rowSet);
		return rowSet;
	}

	/// <summary>
	/// Adds a set that changes rows already in the table (or inserted by an earlier step). It runs in step 2 by default,
	/// after the insert sets of step 1.
	/// </summary>
	public RowSetViewModel AddUpdateSet()
	{
		EnsureRowSets();

		RowSetViewModel rowSet = new(
			GetNextRowSetName(UPDATE_SET_NAME_PREFIX),
			DEFAULT_ROW_COUNT,
			_ruleFactory.CreateRules(Model, isUpdate: true),
			RowSetAction.Update
		);

		AddRowSetCore(rowSet);
		return rowSet;
	}

	public RowSetViewModel DuplicateRowSet(RowSetViewModel source)
	{
		ArgumentNullException.ThrowIfNull(source);

		IReadOnlyList<ColumnRuleViewModel> rules = _ruleFactory.CreateRules(Model, source.IsUpdate);

		for (int index = 0; index < rules.Count; ++index)
		{
			rules[index].CopyFrom(source.ColumnRules[index]);
		}

		RowSetViewModel rowSet = new(
			GetUniqueName(source.Name.Trim() + COPY_SUFFIX),
			source.RowCount,
			rules,
			source.Action
		);

		rowSet.CopySettingsFrom(source);
		AddRowSetCore(rowSet);
		return rowSet;
	}

	public bool RemoveRowSet(RowSetViewModel rowSet)
	{
		ArgumentNullException.ThrowIfNull(rowSet);

		int index = RowSets.IndexOf(rowSet);

		if (RowSets.Count <= 1 || index < 0)
		{
			return false;
		}

		rowSet.SettingsChanged -= OnRowSetSettingsChanged;
		RowSets.RemoveAt(index);
		SelectedRowSet = RowSets[Math.Min(index, RowSets.Count - 1)];
		return true;
	}

	public void SetRowCountForAllSets(int rowCount)
	{
		int clampedRowCount = RowSetViewModel.ClampRowCount(rowCount);

		if (RowSets.Count == 0)
		{
			if (_pendingRowCount != clampedRowCount)
			{
				_pendingRowCount = clampedRowCount;
				OnRowCountChanged();
			}

			return;
		}

		foreach (RowSetViewModel rowSet in RowSets.Where(rowSet => !rowSet.IsUpdate))
		{
			rowSet.RowCount = clampedRowCount;
		}
	}

	public TableGenerationPlan CreatePlan()
	{
		EnsureRowSets();

		return new TableGenerationPlan
		{
			Table   = Model,
			RowSets = [.. RowSets.Select(rowSet => rowSet.CreatePlan())]
		};
	}

	internal void RaiseDatabaseHiddenChanged() => RaiseDimmedChanged();

	private void AddRowSetCore(RowSetViewModel rowSet)
	{
		rowSet.SettingsChanged += OnRowSetSettingsChanged;
		RowSets.Add(rowSet);
		SelectedRowSet = rowSet;
	}

	private void OnRowSetsChanged()
	{
		OnPropertyChanged(nameof(CanEditRowCount));
		OnPropertyChanged(nameof(HasMultipleRowSets));
		OnPropertyChanged(nameof(HasUpdateSets));
		OnPropertyChanged(nameof(UpdateSetCount));
		OnPropertyChanged(nameof(HasRowSetSummary));
		OnPropertyChanged(nameof(CanRemoveRowSet));
		OnPropertyChanged(nameof(RowSetSummary));
		OnPropertyChanged(nameof(RowCountToolTip));
		OnRowCountChanged();
	}

	private void OnRowSetSettingsChanged(object? sender, EventArgs e) => OnRowCountChanged();

	private void OnRowCountChanged()
	{
		OnPropertyChanged(nameof(RowCount));
		OnPropertyChanged(nameof(TotalRowCount));
		OnPropertyChanged(nameof(InvalidRowSetCount));
		OnPropertyChanged(nameof(HasInvalidRowSets));
		RefreshRowSetCommands?.Invoke();
		RaiseGenerationSettingsChanged();
	}

	private void RaiseGenerationSettingsChanged() => GenerationSettingsChanged?.Invoke(this, EventArgs.Empty);

	private string GetNextRowSetName(string prefix = ROW_SET_NAME_PREFIX)
	{
		int number = RowSets.Count(rowSet => rowSet.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) + 1;

		while (RowSets.Any(rowSet => string.Equals(rowSet.Name.Trim(), prefix + number, StringComparison.OrdinalIgnoreCase)))
		{
			++number;
		}

		return prefix + number;
	}

	private string GetUniqueName(string baseName)
	{
		string name   = baseName;
		int    number = 2;

		while (RowSets.Any(rowSet => string.Equals(rowSet.Name.Trim(), name, StringComparison.OrdinalIgnoreCase)))
		{
			name = $"{baseName} {number}";
			++number;
		}

		return name;
	}

	private string BuildToolTipText()
	{
		StringBuilder builder          = new();
		int           primaryKeyCount  = Model.Columns.Count(column => column.IsPrimaryKey);
		int           declaredKeyCount = Model.ForeignKeys.Count(foreignKey => !foreignKey.IsInferred);
		int           inferredKeyCount = Model.ForeignKeys.Count(foreignKey => foreignKey.IsInferred);

		_ = builder.Append(Model.FullyQualifiedName)
			.AppendLine()
			.Append($"{Model.Columns.Count} column(s)");

		if (primaryKeyCount > 0)
		{
			_ = builder.Append($", {primaryKeyCount} primary key column(s)");
		}

		if (declaredKeyCount > 0)
		{
			_ = builder.AppendLine().Append($"References: {DescribeReferences(inferred: false)}");
		}

		if (inferredKeyCount > 0)
		{
			_ = builder.AppendLine().Append($"Inferred from FTK columns: {DescribeReferences(inferred: true)}");
		}

		_ = builder.AppendLine()
			.AppendLine()
			.Append("Click to edit its column rules. Ctrl+click or Shift+click to select several rows for the bulk actions.");

		return builder.ToString();
	}

	private string DescribeReferences(bool inferred)
		=> string.Join(
				", ",
				Model.ForeignKeys
					.Where(foreignKey => foreignKey.IsInferred == inferred)
					.Select(foreignKey => $"{foreignKey.ReferencedSchema}.{foreignKey.ReferencedTable}")
					.Distinct(StringComparer.OrdinalIgnoreCase)
			);
}