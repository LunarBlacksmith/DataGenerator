using DataGenerator.Models;

namespace DataGenerator.ViewModels;

/// <summary>
/// A database in the explorer tree. Its check box includes or excludes all of its tables at once.
/// </summary>
public sealed class DatabaseNodeViewModel : TreeNodeViewModel
{
	private bool _wasExpandedBeforeHiding;

	public DatabaseNodeViewModel(DatabaseModel model, ColumnRuleFactory ruleFactory)
	{
		ArgumentNullException.ThrowIfNull(ruleFactory);

		Model  = model ?? throw new ArgumentNullException(nameof(model));
		Tables =
		[
			.. model.Tables
					.OrderBy(table => table.SchemaName, StringComparer.OrdinalIgnoreCase)
					.ThenBy(table => table.Name, StringComparer.OrdinalIgnoreCase)
					.Select(table => new TableNodeViewModel(table, this, ruleFactory))
		];

		foreach (TableNodeViewModel table in Tables)
		{
			table.GenerationSettingsChanged += OnTableSettingsChanged;
		}
	}

	public DatabaseModel                     Model  { get; }
	public IReadOnlyList<TableNodeViewModel> Tables { get; }

	public override string DisplayName => Model.Name;

	/// <summary>
	/// True when every table is included, false when none is, and <see langword="null"/> when only some are.
	/// Setting it includes or excludes every table of the database.
	/// </summary>
	public bool? IncludeState
	{
		get
		{
			int includedTableCount = IncludedTableCount;

			if (includedTableCount == 0)
			{
				return false;
			}

			return includedTableCount == Tables.Count ? true : null;
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
	public long IncludedRowCount   => Tables.Where(table => table.IsIncluded).Sum(table => (long)table.TotalRowCount);

	public string SummaryText
	{
		get
		{
			int includedTableCount = IncludedTableCount;

			return includedTableCount == 0
				? $"{Tables.Count:N0} tables"
				: $"{includedTableCount:N0} of {Tables.Count:N0} tables · {IncludedRowCount:N0} rows";
		}
	}

	public string IncludeToolTip
		=> $"Include or exclude all {Tables.Count:N0} tables of {Model.Name}. A partly filled box means only some tables are included.";

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

	private void OnTableSettingsChanged(object? sender, EventArgs e)
	{
		OnPropertyChanged(nameof(IncludeState));
		OnPropertyChanged(nameof(IncludedTableCount));
		OnPropertyChanged(nameof(IncludedRowCount));
		OnPropertyChanged(nameof(SummaryText));
	}
}