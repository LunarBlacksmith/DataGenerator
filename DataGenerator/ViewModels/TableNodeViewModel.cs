using System.Collections.ObjectModel;
using System.Text;
using DataGenerator.Infrastructure;
using DataGenerator.Models;

namespace DataGenerator.ViewModels;

/// <summary>
///	A table in the explorer tree, with the row sets that describe the data to generate for it.
///	Row sets (and their column rules) are created on first use so large databases load quickly.
/// </summary>
public sealed class TableNodeViewModel : TreeNodeViewModel
{
	#region FIELDS
	#region PUBLIC
	public const int DEFAULT_ROW_COUNT = 10;
	#endregion PUBLIC

	#region PRIVATE
	private const string ROW_SET_NAME_PREFIX    = "Set ";
	private const string UPDATE_SET_NAME_PREFIX = "Update ";
	private const string COPY_SUFFIX            = " (copy)";

	private readonly ColumnRuleFactory _ruleFactory;

	private RowSetViewModel? _selectedRowSet;
	private bool             _isIncluded;
	private int              _pendingRowCount;
	#endregion PRIVATE
	#endregion FIELDS

	#region PROPERTIES
	#region PUBLIC
	public TableModel                            Model       { get; }
	public DatabaseNodeViewModel                 Database    { get; }
	public ObservableCollection<RowSetViewModel> RowSets     { get; }

	/// <summary>
	///	Swaps two row sets supplied by the tab Shift+drag behaviour.
	/// </summary>
	public RelayCommand SwapRowSetsCommand { get; }

	/// <summary>
	///	Inserts a set dragged by its tab before or after another set, shifting the intervening sets.
	/// </summary>
	public RelayCommand MoveRowSetCommand { get; }
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
	///	Rows to generate. With several row sets (or an update set) this is the (read-only) total of the insert sets.
	///	Editing it also includes the table, because a row count only matters for included tables.
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
	///	New rows the insert sets generate. Update sets change existing rows, so they do not add to it.
	/// </summary>
	public int TotalRowCount
		=>
			RowSets.Count == 0
				? _pendingRowCount
				: RowSets
					.Where(rowSet => !rowSet.IsUpdate)
					.Sum(rowSet => rowSet.RowCount);

	public int  UpdateSetCount     => RowSets.Count(rowSet => rowSet.IsUpdate);
	public bool HasUpdateSets      => RowSets.Any(rowSet => rowSet.IsUpdate);
	public bool CanEditRowCount    => RowSets.Count == 0 || (RowSets.Count == 1 && !RowSets[0].IsUpdate);
	public bool HasMultipleRowSets => RowSets.Count > 1;
	public bool HasRowSetSummary   => HasMultipleRowSets || HasUpdateSets;
	public bool CanRemoveRowSet    => RowSets.Count > 1 && _selectedRowSet is not null;
	public int  ColumnCount        => Model.Columns.Count;

	public string RowSetSummary
		=>
			!HasRowSetSummary
				? string.Empty
				: UpdateSetCount == 0
					? $"{RowSets.Count} sets"
					: $"{RowSets.Count} {(RowSets.Count == 1 ? "set" : "sets")}, {UpdateSetCount} update";

	public string RowCountToolTip
		=>
			CanEditRowCount
				? "Number of rows to generate for this table (1 to 1,000,000). Changing it also includes the table."
				: $"New rows of the {RowSets.Count - UpdateSetCount} insert set(s); update sets change existing rows instead. "
					+ "Edit each set's row count in the column rules panel.";

	public int InvalidRowSetCount => RowSets.Count(rowSet => !rowSet.IsValid);

	public bool HasInvalidRowSets => RowSets.Any(rowSet => !rowSet.IsValid);
	#endregion PUBLIC

	#region INTERNAL
	/// <summary>
	///	Lets the explorer refresh the add/duplicate/remove commands when the selected row set changes.
	/// </summary>
	internal Action? RefreshRowSetCommands { get; set; }
	#endregion INTERNAL
	#endregion PROPERTIES

	#region EVENTS
	/// <summary>
	///	Raised when inclusion, row counts or the validity of the rules change.
	/// </summary>
	public event EventHandler? GenerationSettingsChanged;
	#endregion EVENTS

	#region CONSTRUCTOR
	/// <summary>
	///	Creates a table node and stores the factory used when row sets are first needed.
	/// </summary>
	/// <param name="model">
	///	The table metadata represented by the node.
	/// </param>
	/// <param name="database">
	///	The database node that owns this table.
	/// </param>
	/// <param name="ruleFactory">
	///	The factory used to create column rules for new row sets.
	/// </param>
	/// <exception cref="ArgumentNullException">
	///	Thrown when any argument is <see langword="null"/>.
	/// </exception>
	public TableNodeViewModel(TableModel model, DatabaseNodeViewModel database, ColumnRuleFactory ruleFactory)
	{
		_selectedRowSet       = null;
		_isIncluded           = false;
		_pendingRowCount      = DEFAULT_ROW_COUNT;
		RowSets               = [];
		SwapRowSetsCommand    = new RelayCommand(
			parameter =>
			{
				if (parameter is Tuple<object, object> pair
					&& pair.Item1 is RowSetViewModel source
					&& pair.Item2 is RowSetViewModel target)
				{
					SwapRowSets(source, target);
				}
			}
		);
		MoveRowSetCommand = new RelayCommand(
			parameter =>
			{
				if (parameter is Tuple<object, object, bool> move
					&& move.Item1 is RowSetViewModel source
					&& move.Item2 is RowSetViewModel target)
				{
					MoveRowSet(source, target, move.Item3);
				}
			}
		);
		RefreshRowSetCommands = null;

		Model        = model ?? throw new ArgumentNullException(nameof(model));
		Database     = database ?? throw new ArgumentNullException(nameof(database));
		_ruleFactory = ruleFactory ?? throw new ArgumentNullException(nameof(ruleFactory));
		ToolTipText  = BuildToolTipText();

		RowSets.CollectionChanged += (_, _) => OnRowSetsChanged();
	}
	#endregion CONSTRUCTOR

	#region METHODS
	#region PUBLIC
	/// <summary>
	///	Creates the first insert row set when the table has not yet been expanded or edited.
	/// </summary>
	public void EnsureRowSets()
	{
		if (RowSets.Count == 0)
		{
			AddRowSetCore(new RowSetViewModel(ROW_SET_NAME_PREFIX + "1", _pendingRowCount, _ruleFactory.CreateRules(Model)));
		}
	}

	/// <summary>
	///	Adds a new insert row set to the table and selects it.
	/// </summary>
	/// <returns>
	///	The added row set.
	/// </returns>
	public RowSetViewModel AddRowSet()
	{
		EnsureRowSets();

		RowSetViewModel rowSet = new(GetNextRowSetName(), DEFAULT_ROW_COUNT, _ruleFactory.CreateRules(Model));

		AddRowSetCore(rowSet);
		return rowSet;
	}

	/// <summary>
	///	Adds a set that changes rows already in the table (or inserted by an earlier step). It runs in step 2 by default,
	///	after the insert sets of step 1.
	/// </summary>
	/// <returns>
	///	The added update row set.
	/// </returns>
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

	/// <summary>
	///	Copies an existing row set, including its column rules, and selects the copy.
	/// </summary>
	/// <param name="source">
	///	The row set to duplicate.
	/// </param>
	/// <returns>
	///	The duplicated row set.
	/// </returns>
	/// <exception cref="ArgumentNullException">
	///	Thrown when <paramref name="source"/> is <see langword="null"/>.
	/// </exception>
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

	/// <summary>
	///	Removes a row set when at least one other row set remains.
	/// </summary>
	/// <param name="rowSet">
	///	The row set to remove.
	/// </param>
	/// <returns>
	///	<see langword="true"/> when the row set was removed; otherwise <see langword="false"/>.
	/// </returns>
	/// <exception cref="ArgumentNullException">
	///	Thrown when <paramref name="rowSet"/> is <see langword="null"/>.
	/// </exception>
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

	/// <summary>
	///	Exchanges two row-set positions without changing their rules, steps or the selected set.
	/// </summary>
	/// <param name="source">
	///	The dragged row set belonging to this table.
	/// </param>
	/// <param name="target">
	///	The row set onto which it was dropped.
	/// </param>
	/// <exception cref="ArgumentException">
	///	Thrown when either row set does not belong to this table.
	/// </exception>
	public void SwapRowSets(RowSetViewModel source, RowSetViewModel target)
	{
		int sourceIndex = RowSets.IndexOf(source);
		int targetIndex = RowSets.IndexOf(target);

		if (sourceIndex < 0 || targetIndex < 0)
		{
			throw new ArgumentException("Both row sets must belong to this table.");
		}

		if (sourceIndex == targetIndex)
		{
			return;
		}

		RowSetViewModel? selected = SelectedRowSet;

		RowSets.Move(sourceIndex, targetIndex);
		RowSets.Move(targetIndex > sourceIndex ? targetIndex - 1 : targetIndex + 1, sourceIndex);
		SelectedRowSet = selected;
	}

	/// <summary>
	///	Moves a set to either edge of another set, preserving selection, settings and execution step numbers.
	/// </summary>
	/// <param name="source">
	///	The set being moved.
	/// </param>
	/// <param name="target">
	///	The set marking the insertion position.
	/// </param>
	/// <param name="after">
	///	Whether to insert after the target instead of before it.
	/// </param>
	/// <exception cref="ArgumentException">
	///	Thrown when either set does not belong to this table.
	/// </exception>
	public void MoveRowSet(RowSetViewModel source, RowSetViewModel target, bool after)
	{
		int sourceIndex = RowSets.IndexOf(source);
		int targetIndex = RowSets.IndexOf(target);
		if (sourceIndex < 0 || targetIndex < 0)
		{
			throw new ArgumentException("Both row sets must belong to this table.");
		}

		if (sourceIndex == targetIndex)
		{
			return;
		}

		int destination          = targetIndex + (after ? 1 : 0) - (sourceIndex < targetIndex ? 1 : 0);
		RowSetViewModel? selected = SelectedRowSet;
		RowSets.Move(sourceIndex, destination);
		SelectedRowSet = selected;
	}

	/// <summary>
	///	Sets the row count used by all insert row sets, or stores it until the first row set is created.
	/// </summary>
	/// <param name="rowCount">
	///	The requested row count, clamped to the row-set limits.
	/// </param>
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

	/// <summary>
	///	Creates the generation plan for this table and all of its row sets.
	/// </summary>
	/// <returns>
	///	The table generation plan.
	/// </returns>
	public TableGenerationPlan CreatePlan()
	{
		EnsureRowSets();

		return new TableGenerationPlan
		{
			Table   = Model,
			RowSets = [.. RowSets.Select(rowSet => rowSet.CreatePlan())]
		};
	}
	#endregion PUBLIC

	#region INTERNAL
	/// <summary>
	///	Refreshes dimming after the parent database is hidden or shown.
	/// </summary>
	internal void RaiseDatabaseHiddenChanged() => RaiseDimmedChanged();
	#endregion INTERNAL

	#region PRIVATE
	/// <summary>
	///	Adds a row set to the collection, hooks its settings changes and selects it.
	/// </summary>
	/// <param name="rowSet">
	///	The row set to add.
	/// </param>
	private void AddRowSetCore(RowSetViewModel rowSet)
	{
		rowSet.SettingsChanged += OnRowSetSettingsChanged;
		RowSets.Add(rowSet);
		SelectedRowSet = rowSet;
	}

	/// <summary>
	///	Refreshes row-set summary and row-count state after the row-set collection changes.
	/// </summary>
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

	/// <summary>
	///	Refreshes table row counts after one row set changes.
	/// </summary>
	/// <param name="sender">
	///	The row set whose settings changed.
	/// </param>
	/// <param name="e">
	///	The event data for the row-set settings change.
	/// </param>
	private void OnRowSetSettingsChanged(object? sender, EventArgs e) => OnRowCountChanged();

	/// <summary>
	///	Refreshes row-count and validity properties and notifies the explorer.
	/// </summary>
	private void OnRowCountChanged()
	{
		OnPropertyChanged(nameof(RowCount));
		OnPropertyChanged(nameof(TotalRowCount));
		OnPropertyChanged(nameof(InvalidRowSetCount));
		OnPropertyChanged(nameof(HasInvalidRowSets));
		RefreshRowSetCommands?.Invoke();
		RaiseGenerationSettingsChanged();
	}

	/// <summary>
	///	Raises the generation settings changed event.
	/// </summary>
	private void RaiseGenerationSettingsChanged() => GenerationSettingsChanged?.Invoke(this, EventArgs.Empty);

	/// <summary>
	///	Chooses the next unused row-set name for the supplied prefix.
	/// </summary>
	/// <param name="prefix">
	///	The prefix for insert or update set names.
	/// </param>
	/// <returns>
	///	A row-set name that is not already used by this table.
	/// </returns>
	private string GetNextRowSetName(string prefix = ROW_SET_NAME_PREFIX)
	{
		int number =
			RowSets
				.Count(rowSet => rowSet.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
			+ 1;

		while (
			RowSets
				.Any(
					rowSet => string.Equals(
						rowSet.Name.Trim(),
						prefix + number,
						StringComparison.OrdinalIgnoreCase
					)
				)
		)
		{
			++number;
		}

		return prefix + number;
	}

	/// <summary>
	///	Adds a numeric suffix to a base name until it is unique among this table's row sets.
	/// </summary>
	/// <param name="baseName">
	///	The preferred row-set name.
	/// </param>
	/// <returns>
	///	The base name or a suffixed name that is unique.
	/// </returns>
	private string GetUniqueName(string baseName)
	{
		string name   = baseName;
		int    number = 2;

		while (
			RowSets
				.Any(
					rowSet => string.Equals(rowSet.Name.Trim(), name, StringComparison.OrdinalIgnoreCase)
				)
		)
		{
			name = $"{baseName} {number}";
			++number;
		}

		return name;
	}

	/// <summary>
	///	Builds the tooltip that describes the table, its columns and its references.
	/// </summary>
	/// <returns>
	///	The multi-line tooltip text.
	/// </returns>
	private string BuildToolTipText()
	{
		StringBuilder builder          = new();
		int           primaryKeyCount  =
			Model
				.Columns
				.Count(column => column.IsPrimaryKey);
		int           declaredKeyCount =
			Model
				.ForeignKeys
				.Count(foreignKey => !foreignKey.IsInferred);
		int           inferredKeyCount =
			Model
				.ForeignKeys
				.Count(foreignKey => foreignKey.IsInferred);

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

	/// <summary>
	///	Describes the referenced tables for declared or inferred foreign keys.
	/// </summary>
	/// <param name="inferred">
	///	Whether to describe inferred FTK references instead of declared references.
	/// </param>
	/// <returns>
	///	A comma-separated list of referenced schema and table names.
	/// </returns>
	private string DescribeReferences(bool inferred)
		=> string.Join(
			", ",
			Model
				.ForeignKeys
				.Where(foreignKey => foreignKey.IsInferred == inferred)
				.Select(foreignKey => $"{foreignKey.ReferencedSchema}.{foreignKey.ReferencedTable}")
				.Distinct(StringComparer.OrdinalIgnoreCase)
		);
	#endregion PRIVATE
	#endregion METHODS
}