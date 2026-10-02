using System.Collections.ObjectModel;
using System.Globalization;
using DataGenerator.Infrastructure;
using DataGenerator.Models;
using DataGenerator.Services;

namespace DataGenerator.ViewModels;

/// <summary>
/// The database/table tree: multi-selection, search, hiding, bulk actions and the table whose rules are being edited.
/// </summary>
public sealed class DatabaseExplorerViewModel : ValidatableObservableObject
{
	private static readonly CultureInfo INVARIANT = CultureInfo.InvariantCulture;

	private readonly ColumnRuleFactory                        _ruleFactory;
	private readonly SavedSettingsLibrary                     _savedSettings;
	private readonly ISavedSettingsWindowService              _savedSettingsWindows;
	private readonly IDialogService                           _dialogService;
	private readonly HashSet<TreeNodeViewModel>               _selectedNodes;
	private readonly Dictionary<string, TableNodeViewModel>   _tablesByKey;
	private readonly List<RelayCommand>                       _selectionCommands;

	private TreeNodeViewModel?  _selectionAnchor;
	private TableNodeViewModel? _activeTable;
	private string              _filterText;
	private bool                _showIncludedOnly;
	private string              _bulkRowCountText;
	private int                 _bulkUpdateDepth;
	private bool                _hasPendingSettingsChange;

	public DatabaseExplorerViewModel(
		ColumnRuleFactory           ruleFactory,
		SavedSettingsLibrary        savedSettings,
		ISavedSettingsWindowService savedSettingsWindows,
		IDialogService              dialogService
	)
	{
		_ruleFactory              = ruleFactory ?? throw new ArgumentNullException(nameof(ruleFactory));
		_savedSettings            = savedSettings ?? throw new ArgumentNullException(nameof(savedSettings));
		_savedSettingsWindows     = savedSettingsWindows ?? throw new ArgumentNullException(nameof(savedSettingsWindows));
		_dialogService            = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
		_selectedNodes            = [];
		_tablesByKey              = new Dictionary<string, TableNodeViewModel>(StringComparer.OrdinalIgnoreCase);
		_selectionAnchor          = null;
		_activeTable              = null;
		_filterText               = string.Empty;
		_showIncludedOnly         = false;
		_bulkRowCountText         = TableNodeViewModel.DEFAULT_ROW_COUNT.ToString(INVARIANT);
		_bulkUpdateDepth          = 0;
		_hasPendingSettingsChange = false;

		SelectNodeCommand       = new RelayCommand(SelectNode);
		SelectAllCommand        = new RelayCommand(_ => SelectAllVisible(), _ => HasDatabases);
		ClearSelectionCommand   = new RelayCommand(_ => ClearSelection(), _ => HasSelection);
		ExpandSelectedCommand   = new RelayCommand(_ => SetSelectedExpanded(true), _ => HasSelection);
		CollapseSelectedCommand = new RelayCommand(_ => SetSelectedExpanded(false), _ => HasSelection);
		ExpandAllCommand        = new RelayCommand(_ => SetAllExpanded(true), _ => HasDatabases);
		CollapseAllCommand      = new RelayCommand(_ => SetAllExpanded(false), _ => HasDatabases);
		HideSelectedCommand     = new RelayCommand(_ => SetHidden(_selectedNodes, true), _ => HasSelection);
		ShowSelectedCommand     = new RelayCommand(_ => SetHidden(_selectedNodes, false), _ => HasSelection);
		ShowAllCommand          = new RelayCommand(_ => SetHidden(GetAllNodes(), false), _ => HasDatabases);
		ToggleHiddenCommand     = new RelayCommand(ToggleHidden);
		IncludeSelectedCommand  = new RelayCommand(_ => SetSelectedIncluded(true), _ => HasSelection);
		ExcludeSelectedCommand  = new RelayCommand(_ => SetSelectedIncluded(false), _ => HasSelection);
		ExcludeAllCommand       = new RelayCommand(_ => ExcludeAll(), _ => IncludedTableCount > 0);
		ApplyRowCountCommand    = new RelayCommand(_ => ApplyRowCountToSelection(), _ => HasSelection && TryGetBulkRowCount(out int _));
		ClearFilterCommand      = new RelayCommand(_ => FilterText = string.Empty, _ => _filterText.Length > 0);
		AddRowSetCommand        = new RelayCommand(_ => AddRowSet(), _ => _activeTable is not null);
		DuplicateRowSetCommand  = new RelayCommand(_ => DuplicateRowSet(), _ => _activeTable?.SelectedRowSet is not null);
		RemoveRowSetCommand     = new RelayCommand(_ => RemoveRowSet(), _ => _activeTable?.CanRemoveRowSet == true);

		ManageSavedSettingsCommand = new RelayCommand(_ => _savedSettingsWindows.ShowManager());

		_selectionCommands =
		[
			ClearSelectionCommand,
			ExpandSelectedCommand,
			CollapseSelectedCommand,
			HideSelectedCommand,
			ShowSelectedCommand,
			IncludeSelectedCommand,
			ExcludeSelectedCommand,
			ApplyRowCountCommand
		];

		_savedSettings.Changed                         += OnSavedSettingsChanged;
		_savedSettings.ApplyAutomaticSettingsRequested += OnApplyAutomaticSettingsRequested;
	}

	/// <summary>
	/// Raised when tables are included or excluded, or when row counts or rule validity change.
	/// </summary>
	public event EventHandler? GenerationSettingsChanged;

	#region PROPERTIES
	#region PUBLIC
	public ObservableCollection<DatabaseNodeViewModel> Databases { get; } = [];

	public IEnumerable<TableNodeViewModel> AllTables => Databases.SelectMany(database => database.Tables);

	public RelayCommand SelectNodeCommand       { get; }
	public RelayCommand SelectAllCommand        { get; }
	public RelayCommand ClearSelectionCommand   { get; }
	public RelayCommand ExpandSelectedCommand   { get; }
	public RelayCommand CollapseSelectedCommand { get; }
	public RelayCommand ExpandAllCommand        { get; }
	public RelayCommand CollapseAllCommand      { get; }
	public RelayCommand HideSelectedCommand     { get; }
	public RelayCommand ShowSelectedCommand     { get; }
	public RelayCommand ShowAllCommand          { get; }
	public RelayCommand ToggleHiddenCommand     { get; }
	public RelayCommand IncludeSelectedCommand  { get; }
	public RelayCommand ExcludeSelectedCommand  { get; }
	public RelayCommand ExcludeAllCommand       { get; }
	public RelayCommand ApplyRowCountCommand    { get; }
	public RelayCommand ClearFilterCommand      { get; }
	public RelayCommand AddRowSetCommand        { get; }
	public RelayCommand DuplicateRowSetCommand  { get; }
	public RelayCommand RemoveRowSetCommand     { get; }

	public RelayCommand ManageSavedSettingsCommand { get; }

	/// <summary>
	/// The table whose row sets and column rules are shown for editing (the last table that was clicked).
	/// </summary>
	public TableNodeViewModel? ActiveTable
	{
		get => _activeTable;
		private set
		{
			TableNodeViewModel? previousTable = _activeTable;

			if (!SetProperty(ref _activeTable, value))
			{
				return;
			}

			if (previousTable is not null)
			{
				previousTable.RefreshRowSetCommands = null;
			}

			if (value is not null)
			{
				value.EnsureRowSets();
				value.RefreshRowSetCommands = RefreshRowSetCommands;
			}

			OnPropertyChanged(nameof(HasActiveTable));
			RefreshRowSetCommands();
		}
	}
	public string               FilterText
	{
		get => _filterText;
		set
		{
			if (SetProperty(ref _filterText, value ?? string.Empty))
			{
				ApplyFilter();
				ClearFilterCommand.NotifyCanExecuteChanged();
			}
		}
	}
	public bool                 ShowIncludedOnly
	{
		get => _showIncludedOnly;
		set
		{
			if (SetProperty(ref _showIncludedOnly, value))
			{
				ApplyFilter();
			}
		}
	}
	public string               BulkRowCountText
	{
		get => _bulkRowCountText;
		set
		{
			if (SetProperty(ref _bulkRowCountText, value ?? string.Empty))
			{
				SetError(
					nameof(BulkRowCountText),
					TryGetBulkRowCount(out _)
						? null
						: $"Enter a whole number of rows from 1 to {RowSetViewModel.MAXIMUM_ROW_COUNT:N0}."
				);
				ApplyRowCountCommand.NotifyCanExecuteChanged();
			}
		}
	}

	public bool   HasActiveTable     => _activeTable is not null;
	public bool   HasDatabases       => Databases.Count > 0;
	public bool   HasSelection       => _selectedNodes.Count > 0;
	public int    TotalTableCount    => _tablesByKey.Count;
	public int    IncludedTableCount => Databases.Sum(database => database.IncludedTableCount);
	public long   IncludedRowCount   => Databases.Sum(database => database.IncludedRowCount);
	public bool   HasNoVisibleNodes  => HasDatabases && !Databases.Any(database => database.IsVisibleInTree);

	public string SelectionSummary
	{
		get
		{
			if (_selectedNodes.Count == 0)
			{
				return "Nothing selected · Ctrl/Shift+click selects several";
			}

			int databaseCount = _selectedNodes.Count(node => node is DatabaseNodeViewModel);
			int tableCount    = _selectedNodes.Count - databaseCount;

			return (databaseCount, tableCount) switch
			{
				(0, _) => $"{tableCount:N0} table(s) selected",
				(_, 0) => $"{databaseCount:N0} database(s) selected",
				_      => $"{databaseCount:N0} database(s) and {tableCount:N0} table(s) selected"
			};
		}
	}

	public string IncludedSummary
		=> HasDatabases
			? $"{IncludedTableCount:N0} of {TotalTableCount:N0} tables included · {IncludedRowCount:N0} rows"
			: "No metadata loaded.";
	#endregion PUBLIC
	#endregion PROPERTIES

	public void Load(IReadOnlyList<DatabaseModel> databases)
	{
		ArgumentNullException.ThrowIfNull(databases);

		foreach (TableNodeViewModel table in AllTables)
		{
			table.GenerationSettingsChanged -= OnTableSettingsChanged;
		}

		ClearSelectionCore();
		ActiveTable = null;
		Databases.Clear();
		_tablesByKey.Clear();

		foreach (DatabaseModel model in databases.OrderBy(database => database.Name, StringComparer.OrdinalIgnoreCase))
		{
			DatabaseNodeViewModel database = new DatabaseNodeViewModel(model, _ruleFactory);

			// Databases start expanded so every table of every database can be seen at once.
			database.IsExpanded = true;

			foreach (TableNodeViewModel table in database.Tables)
			{
				table.GenerationSettingsChanged += OnTableSettingsChanged;
				_tablesByKey[table.Model.Key]    = table;
			}

			Databases.Add(database);
		}

		ApplyFilter();
		OnPropertyChanged(nameof(HasDatabases));
		OnPropertyChanged(nameof(TotalTableCount));
		OnSelectionChanged();
		SelectAllCommand.NotifyCanExecuteChanged();
		ExpandAllCommand.NotifyCanExecuteChanged();
		CollapseAllCommand.NotifyCanExecuteChanged();
		ShowAllCommand.NotifyCanExecuteChanged();
		OnGenerationSettingsChanged();
	}

	public IReadOnlyList<TableNodeViewModel> GetIncludedTables() => [.. AllTables.Where(table => table.IsIncluded)];

	public TableNodeViewModel? FindTable(string tableKey)
		=> _tablesByKey.TryGetValue(tableKey, out TableNodeViewModel? table) ? table : null;

	/// <summary>
	/// Includes several tables while raising <see cref="GenerationSettingsChanged"/> only once.
	/// </summary>
	public void IncludeTables(IEnumerable<TableNodeViewModel> tables)
	{
		ArgumentNullException.ThrowIfNull(tables);

		RunBulkUpdate(
			() =>
			{
				foreach (TableNodeViewModel table in tables)
				{
					table.IsIncluded = true;
				}
			}
		);
	}

	/// <summary>
	/// Selects a table, makes it the one being edited and brings it into view (used to point at a validation problem).
	/// </summary>
	public void Reveal(TableNodeViewModel table, RowSetViewModel? rowSet = null)
	{
		ArgumentNullException.ThrowIfNull(table);

		if (!table.IsVisibleInTree)
		{
			_filterText       = string.Empty;
			_showIncludedOnly = false;
			OnPropertyChanged(nameof(FilterText));
			OnPropertyChanged(nameof(ShowIncludedOnly));
			ClearFilterCommand.NotifyCanExecuteChanged();
			ApplyFilter();
		}

		table.Database.IsExpanded = true;
		ReplaceSelection([table]);
		_selectionAnchor = table;
		ActiveTable      = table;

		if (rowSet is not null && table.RowSets.Contains(rowSet))
		{
			table.SelectedRowSet = rowSet;
		}
	}

	private void SelectNode(object? parameter)
	{
		if (parameter is not TreeSelectionRequest { Item: TreeNodeViewModel node } request)
		{
			return;
		}

		// Right-clicking a selected row keeps the selection, so the context menu acts on all of it.
		if (request.Mode == TreeSelectionMode.Context && node.IsSelected)
		{
			return;
		}

		if (request.Mode == TreeSelectionMode.Toggle)
		{
			ToggleSelection(node);
		}
		else if (request.Mode != TreeSelectionMode.Range || !TrySelectRange(node))
		{
			ReplaceSelection([node]);
			_selectionAnchor = node;
		}

		if (node.IsSelected && node is TableNodeViewModel table)
		{
			ActiveTable = table;
		}
	}

	private void ToggleSelection(TreeNodeViewModel node)
	{
		if (_selectedNodes.Remove(node))
		{
			node.IsSelected = false;
		}
		else
		{
			_ = _selectedNodes.Add(node);
			node.IsSelected = true;
		}

		_selectionAnchor = node;
		OnSelectionChanged();
	}

	/// <summary>
	/// Selects every visible row between the anchor (the last row clicked without Shift) and <paramref name="node"/>.
	/// </summary>
	private bool TrySelectRange(TreeNodeViewModel node)
	{
		if (_selectionAnchor is null)
		{
			return false;
		}

		List<TreeNodeViewModel> visibleNodes = GetVisibleNodesInOrder();
		int                     anchorIndex  = visibleNodes.IndexOf(_selectionAnchor);
		int                     nodeIndex    = visibleNodes.IndexOf(node);

		if (anchorIndex < 0 || nodeIndex < 0)
		{
			return false;
		}

		ReplaceSelection(visibleNodes.GetRange(Math.Min(anchorIndex, nodeIndex), Math.Abs(nodeIndex - anchorIndex) + 1));
		return true;
	}

	private void SelectAllVisible()
	{
		ReplaceSelection(GetVisibleNodesInOrder());
		_selectionAnchor = null;
	}

	private void ClearSelection()
	{
		ClearSelectionCore();
		OnSelectionChanged();
	}

	private void ClearSelectionCore()
	{
		foreach (TreeNodeViewModel node in _selectedNodes)
		{
			node.IsSelected = false;
		}

		_selectedNodes.Clear();
		_selectionAnchor = null;
	}

	private void ReplaceSelection(IEnumerable<TreeNodeViewModel> nodes)
	{
		HashSet<TreeNodeViewModel> newSelection = [.. nodes];

		foreach (TreeNodeViewModel node in _selectedNodes.Where(node => !newSelection.Contains(node)))
		{
			node.IsSelected = false;
		}

		foreach (TreeNodeViewModel node in newSelection)
		{
			node.IsSelected = true;
		}

		_selectedNodes.Clear();
		_selectedNodes.UnionWith(newSelection);
		OnSelectionChanged();
	}

	private void OnSelectionChanged()
	{
		OnPropertyChanged(nameof(HasSelection));
		OnPropertyChanged(nameof(SelectionSummary));

		foreach (RelayCommand command in _selectionCommands)
		{
			command.NotifyCanExecuteChanged();
		}
	}

	/// <summary>
	/// Databases followed by their tables, in the order they appear in the tree (collapsed tables excluded).
	/// </summary>
	private List<TreeNodeViewModel> GetVisibleNodesInOrder()
	{
		List<TreeNodeViewModel> nodes = [];

		foreach (DatabaseNodeViewModel database in Databases.Where(database => database.IsVisibleInTree))
		{
			nodes.Add(database);

			if (database.IsExpanded)
			{
				nodes.AddRange(database.Tables.Where(table => table.IsVisibleInTree));
			}
		}

		return nodes;
	}

	private IEnumerable<TreeNodeViewModel> GetAllNodes()
	{
		foreach (DatabaseNodeViewModel database in Databases)
		{
			yield return database;

			foreach (TableNodeViewModel table in database.Tables)
			{
				yield return table;
			}
		}
	}

	/// <summary>
	/// Selected tables plus the (visible) tables of selected databases.
	/// </summary>
	private List<TableNodeViewModel> GetSelectedTargetTables()
	{
		HashSet<TableNodeViewModel> tables = [];

		foreach (TreeNodeViewModel node in _selectedNodes)
		{
			if (node is TableNodeViewModel table)
			{
				_ = tables.Add(table);
			}
			else if (node is DatabaseNodeViewModel database)
			{
				tables.UnionWith(database.Tables.Where(databaseTable => databaseTable.IsVisibleInTree));
			}
		}

		return [.. tables];
	}

	private void SetSelectedExpanded(bool isExpanded)
	{
		HashSet<DatabaseNodeViewModel> databases = [];

		foreach (TreeNodeViewModel node in _selectedNodes)
		{
			_ = databases.Add(node as DatabaseNodeViewModel ?? ((TableNodeViewModel)node).Database);
		}

		foreach (DatabaseNodeViewModel database in databases)
		{
			database.IsExpanded = isExpanded;
		}
	}

	private void SetAllExpanded(bool isExpanded)
	{
		foreach (DatabaseNodeViewModel database in Databases)
		{
			database.IsExpanded = isExpanded && !database.IsHidden;
		}
	}

	private void ToggleHidden(object? parameter)
	{
		if (parameter is not TreeNodeViewModel node)
		{
			return;
		}

		bool isHidden = !node.IsHidden;

		if (node.IsSelected && _selectedNodes.Count > 1)
		{
			SetHidden(_selectedNodes, isHidden);
		}
		else
		{
			node.IsHidden = isHidden;
		}
	}

	private static void SetHidden(IEnumerable<TreeNodeViewModel> nodes, bool isHidden)
	{
		foreach (TreeNodeViewModel node in nodes.ToList())
		{
			node.IsHidden = isHidden;
		}
	}

	private void SetSelectedIncluded(bool isIncluded)
	{
		List<TableNodeViewModel> tables = GetSelectedTargetTables();

		RunBulkUpdate(
			() =>
			{
				foreach (TableNodeViewModel table in tables)
				{
					table.IsIncluded = isIncluded;
				}
			}
		);
	}

	private void ExcludeAll()
		=> RunBulkUpdate(
				() =>
				{
					foreach (TableNodeViewModel table in AllTables)
					{
						table.IsIncluded = false;
					}
				}
			);

	private void ApplyRowCountToSelection()
	{
		if (!TryGetBulkRowCount(out int rowCount))
		{
			return;
		}

		List<TableNodeViewModel> tables = GetSelectedTargetTables();

		RunBulkUpdate(
			() =>
			{
				foreach (TableNodeViewModel table in tables)
				{
					table.SetRowCountForAllSets(rowCount);
					table.IsIncluded = true;
				}
			}
		);
	}

	private bool TryGetBulkRowCount(out int rowCount)
		=> int.TryParse(_bulkRowCountText.Trim(), NumberStyles.Integer | NumberStyles.AllowThousands, INVARIANT, out rowCount)
			&& rowCount >= 1
			&& rowCount <= RowSetViewModel.MAXIMUM_ROW_COUNT;

	private void AddRowSet()
	{
		if (_activeTable is not null)
		{
			_ = _activeTable.AddRowSet();
		}
	}

	private void DuplicateRowSet()
	{
		if (_activeTable?.SelectedRowSet is RowSetViewModel rowSet)
		{
			_ = _activeTable.DuplicateRowSet(rowSet);
		}
	}

	private void RemoveRowSet()
	{
		if (_activeTable?.SelectedRowSet is not RowSetViewModel rowSet || !_activeTable.CanRemoveRowSet)
		{
			return;
		}

		bool isConfirmed = _dialogService.Confirm(
			"Remove row set",
			$"Remove the row set '{rowSet.Name}' ({rowSet.RowCount:N0} rows) from {_activeTable.DisplayName}?"
				+ $"{Environment.NewLine}{Environment.NewLine}Its column rules will be lost."
		);

		if (isConfirmed)
		{
			_ = _activeTable.RemoveRowSet(rowSet);
		}
	}

	private void RefreshRowSetCommands()
	{
		AddRowSetCommand.NotifyCanExecuteChanged();
		DuplicateRowSetCommand.NotifyCanExecuteChanged();
		RemoveRowSetCommand.NotifyCanExecuteChanged();
	}

	private void ApplyFilter()
	{
		string filter    = _filterText.Trim();
		bool   hasFilter = filter.Length > 0;

		foreach (DatabaseNodeViewModel database in Databases)
		{
			bool databaseMatches  = !hasFilter || Matches(database.DisplayName, filter);
			bool hasVisibleTables = false;

			foreach (TableNodeViewModel table in database.Tables)
			{
				bool isVisible =
					(databaseMatches || Matches(table.DisplayName, filter))
					&& (!_showIncludedOnly || table.IsIncluded);

				table.IsVisibleInTree = isVisible;
				hasVisibleTables     |= isVisible;
			}

			database.IsVisibleInTree = hasVisibleTables || (databaseMatches && !_showIncludedOnly);

			if (hasFilter && hasVisibleTables && !database.IsHidden)
			{
				database.IsExpanded = true;
			}
		}

		DeselectFilteredNodes();
		ApplyStripes();
		OnPropertyChanged(nameof(HasNoVisibleNodes));
	}

	private void DeselectFilteredNodes()
	{
		List<TreeNodeViewModel> filteredNodes = [.. _selectedNodes.Where(node => !node.IsVisibleInTree)];

		if (filteredNodes.Count == 0)
		{
			return;
		}

		foreach (TreeNodeViewModel node in filteredNodes)
		{
			node.IsSelected = false;
			_ = _selectedNodes.Remove(node);
		}

		if (_selectionAnchor is not null && !_selectionAnchor.IsVisibleInTree)
		{
			_selectionAnchor = null;
		}

		OnSelectionChanged();
	}

	/// <summary>
	/// Alternates the background of visible database rows, and separately of the visible table rows of each database.
	/// </summary>
	private void ApplyStripes()
	{
		int databaseIndex = 0;

		foreach (DatabaseNodeViewModel database in Databases.Where(database => database.IsVisibleInTree))
		{
			database.IsAlternate = databaseIndex % 2 == 1;
			++databaseIndex;

			int tableIndex = 0;

			foreach (TableNodeViewModel table in database.Tables.Where(table => table.IsVisibleInTree))
			{
				table.IsAlternate = tableIndex % 2 == 1;
				++tableIndex;
			}
		}
	}

	private void RunBulkUpdate(Action update)
	{
		++_bulkUpdateDepth;

		try
		{
			update();
		}
		finally
		{
			--_bulkUpdateDepth;

			if (_bulkUpdateDepth == 0 && _hasPendingSettingsChange)
			{
				_hasPendingSettingsChange = false;
				OnGenerationSettingsChanged();
			}
		}
	}

	private IEnumerable<ColumnRuleViewModel> GetAllColumnRules()
		=> AllTables.SelectMany(table => table.RowSets).SelectMany(rowSet => rowSet.ColumnRules);

	private void OnSavedSettingsChanged(object? sender, EventArgs e)
	{
		foreach (ColumnRuleViewModel rule in GetAllColumnRules())
		{
			rule.RefreshAppliedSetting();
		}
	}

	private void OnApplyAutomaticSettingsRequested(object? sender, ApplyAutomaticSettingsEventArgs e)
	{
		int updatedColumnCount = 0;

		RunBulkUpdate(
			() =>
			{
				foreach (ColumnRuleViewModel rule in GetAllColumnRules())
				{
					if (rule.ApplyAutomaticSetting())
					{
						++updatedColumnCount;
					}
				}
			}
		);

		e.UpdatedColumnCount += updatedColumnCount;
	}

	private void OnTableSettingsChanged(object? sender, EventArgs e)
	{
		if (_bulkUpdateDepth > 0)
		{
			_hasPendingSettingsChange = true;
			return;
		}

		OnGenerationSettingsChanged();
	}

	private void OnGenerationSettingsChanged()
	{
		if (_showIncludedOnly)
		{
			ApplyFilter();
		}

		OnPropertyChanged(nameof(IncludedTableCount));
		OnPropertyChanged(nameof(IncludedRowCount));
		OnPropertyChanged(nameof(IncludedSummary));
		ExcludeAllCommand.NotifyCanExecuteChanged();
		GenerationSettingsChanged?.Invoke(this, EventArgs.Empty);
	}

	private static bool Matches(string text, string filter) => text.Contains(filter, StringComparison.OrdinalIgnoreCase);
}