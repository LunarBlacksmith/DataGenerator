using DataGenerator.Models;

namespace DataGenerator.ViewModels;

/// <summary>
///	A database in the explorer tree. Its check box includes or excludes all of its tables at once.
/// </summary>
public sealed class DatabaseNodeViewModel : TreeNodeViewModel
{
	#region FIELDS
	#region PRIVATE
	private bool _wasExpandedBeforeHiding;
	#endregion PRIVATE
	#endregion FIELDS

	#region PROPERTIES
	#region PUBLIC
	public DatabaseModel                     Model  { get; }
	public IReadOnlyList<TableNodeViewModel> Tables { get; }

	public override string DisplayName => Model.Name;

	/// <summary>
	///	True when every table is included, false when none is, and <see langword="null"/> when only some are.
	///	Setting it includes or excludes every table of the database.
	/// </summary>
	public bool? IncludeState
	{
		get
		{
			int includedTableCount = IncludedTableCount;

			return
				includedTableCount == 0
					? false
					: includedTableCount == Tables.Count
						? true
						: null;
		}
		set
		{
			bool isIncluded = value ?? true;

			foreach (TableNodeViewModel table in Tables)
			{
				table.IsIncluded = isIncluded;
			}
		}
	}

	public int  IncludedTableCount => Tables.Count(table => table.IsIncluded);
	public long IncludedRowCount
		=>
			Tables
				.Where(table => table.IsIncluded)
				.Sum(table => (long)table.TotalRowCount);

	public string SummaryText
	{
		get
		{
			int includedTableCount = IncludedTableCount;

			return
				includedTableCount == 0
					? $"{Tables.Count:N0} tables"
					: $"{includedTableCount:N0} of {Tables.Count:N0} tables · {IncludedRowCount:N0} rows";
		}
	}

	public string IncludeToolTip
		=> $"Include or exclude all {Tables.Count:N0} tables of {Model.Name}. A partly filled box means only some tables are included.";
	#endregion PUBLIC
	#endregion PROPERTIES

	#region CONSTRUCTORS
	#region PUBLIC
	/// <summary>
	///	Creates the database node and its table child nodes.
	/// </summary>
	/// <param name="model">
	///	The database metadata represented by the node.
	/// </param>
	/// <param name="ruleFactory">
	///	The factory used by child tables when they create row-set rules.
	/// </param>
	/// <exception cref="ArgumentNullException">
	///	Thrown when <paramref name="model"/> or <paramref name="ruleFactory"/> is <see langword="null"/>.
	/// </exception>
	public DatabaseNodeViewModel(DatabaseModel model, ColumnRuleFactory ruleFactory)
	{
		_wasExpandedBeforeHiding = false;

		ArgumentNullException.ThrowIfNull(ruleFactory);

		Model  = model ?? throw new ArgumentNullException(nameof(model));
		Tables = [..
			model
				.Tables
				.OrderBy(table => table.SchemaName, StringComparer.OrdinalIgnoreCase)
				.ThenBy(table => table.Name, StringComparer.OrdinalIgnoreCase)
				.Select(table => new TableNodeViewModel(table, this, ruleFactory))
		];

		foreach (TableNodeViewModel table in Tables)
		{
			table.GenerationSettingsChanged += OnTableSettingsChanged;
		}
	}
	#endregion PUBLIC
	#endregion CONSTRUCTORS

	#region METHODS
	#region PROTECTED
	/// <summary>
	///	Collapses the database while hidden, restores its previous expansion when shown, and refreshes table dimming.
	/// </summary>
	protected override void OnHiddenChanged()
	{
		if (IsHidden)
		{
			_wasExpandedBeforeHiding = IsExpanded;
			IsExpanded               = false;
		}
		else if (_wasExpandedBeforeHiding)
		{
			IsExpanded = true;
		}

		foreach (TableNodeViewModel table in Tables)
		{
			table.RaiseDatabaseHiddenChanged();
		}
	}
	#endregion PROTECTED

	#region PRIVATE
	/// <summary>
	///	Refreshes aggregate inclusion and row-count text after one of the database's tables changes.
	/// </summary>
	/// <param name="sender">
	///	The table whose generation settings changed.
	/// </param>
	/// <param name="e">
	///	The event data for the settings change.
	/// </param>
	private void OnTableSettingsChanged(object? sender, EventArgs e)
	{
		OnPropertyChanged(nameof(IncludeState));
		OnPropertyChanged(nameof(IncludedTableCount));
		OnPropertyChanged(nameof(IncludedRowCount));
		OnPropertyChanged(nameof(SummaryText));
	}
	#endregion PRIVATE
	#endregion METHODS
}