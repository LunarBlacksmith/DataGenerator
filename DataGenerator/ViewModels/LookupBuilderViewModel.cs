using DataGenerator.Infrastructure;
using DataGenerator.Interfaces;
using DataGenerator.Models;

namespace DataGenerator.ViewModels;

/// <summary>
///	Builds the setting of the Value from table mode by picking a table and column from lists instead of typing it.
/// </summary>
public sealed class LookupBuilderViewModel : ObservableObject
{
	#region FIELDS
	#region PUBLIC
	public static readonly IReadOnlyList<ChoiceOption<RowScope>> SCOPE_OPTIONS;

	public static readonly IReadOnlyList<ChoiceOption<LookupFilterKind>> FILTER_OPTIONS;
	#endregion PUBLIC

	#region PRIVATE
	private readonly TableModel              _targetTable;
	private readonly ILookupExpressionParser _parser;
	private readonly Action                  _onChanged;

	private TableModel?      _selectedTable;
	private ColumnModel?     _selectedColumn;
	private bool             _isUnique;
	private RowScope         _scope;
	private LookupFilterKind _filterKind;
	private string           _filterText;
	private string           _expression;
	private string?          _errorMessage;
	#endregion PRIVATE
	#endregion FIELDS

	#region PROPERTIES
	#region PUBLIC
	public IReadOnlyList<LookupTableOption>             Tables        { get; }
	public IReadOnlyList<ChoiceOption<RowScope>>         ScopeOptions  => SCOPE_OPTIONS;
	public IReadOnlyList<ChoiceOption<LookupFilterKind>> FilterOptions => FILTER_OPTIONS;

	public LookupTableOption? SelectedTableOption
	{
		get => Tables.FirstOrDefault(option => ReferenceEquals(option.Table, _selectedTable));
		set
		{
			if (ReferenceEquals(value?.Table, _selectedTable))
			{
				return;
			}

			_selectedTable  = value?.Table;
			_selectedColumn =
				_selectedTable
					?.Columns
					.FirstOrDefault(column => column.IsPrimaryKey)
				?? _selectedTable
					?.Columns
					.FirstOrDefault();
			OnPropertyChanged();
			OnPropertyChanged(nameof(Columns));
			OnPropertyChanged(nameof(SelectedColumn));
			UpdateExpression();
		}
	}

	public IReadOnlyList<ColumnModel> Columns => _selectedTable is null ? [] : [.. _selectedTable.Columns];

	public ColumnModel? SelectedColumn
	{
		get => _selectedColumn;
		set
		{
			if (SetProperty(ref _selectedColumn, value))
			{
				UpdateExpression();
			}
		}
	}

	public bool IsUnique
	{
		get => _isUnique;
		set
		{
			if (SetProperty(ref _isUnique, value))
			{
				UpdateExpression();
			}
		}
	}

	public ChoiceOption<RowScope> SelectedScope
	{
		get => SCOPE_OPTIONS.First(option => option.Value == _scope);
		set
		{
			if (value is not null && value.Value != _scope)
			{
				_scope = value.Value;
				OnPropertyChanged();
				UpdateExpression();
			}
		}
	}

	public ChoiceOption<LookupFilterKind> SelectedFilter
	{
		get => FILTER_OPTIONS.First(option => option.Value == _filterKind);
		set
		{
			if (value is not null && value.Value != _filterKind)
			{
				_filterKind = value.Value;
				OnPropertyChanged();
				OnPropertyChanged(nameof(HasFilter));
				UpdateExpression();
			}
		}
	}

	public bool HasFilter => _filterKind != LookupFilterKind.None;

	public string FilterText
	{
		get => _filterText;
		set
		{
			if (SetProperty(ref _filterText, value ?? string.Empty))
			{
				UpdateExpression();
			}
		}
	}

	/// <summary>
	///	The setting the choices make, which Apply puts into the Settings cell.
	/// </summary>
	public string Expression
	{
		get => _expression;
		private set => SetProperty(ref _expression, value);
	}

	public string? ErrorMessage
	{
		get => _errorMessage;
		private set
		{
			if (SetProperty(ref _errorMessage, value))
			{
				OnPropertyChanged(nameof(HasError));
			}
		}
	}

	public bool HasError => _errorMessage is not null;

	public bool CanApply => _errorMessage is null && _expression.Length > 0;
	#endregion PUBLIC
	#endregion PROPERTIES

	#region CONSTRUCTORS
	#region STATIC
	/// <summary>
	///	Sets the default values of the static fields and properties of <see cref="LookupBuilderViewModel"/>.
	/// </summary>
	static LookupBuilderViewModel()
	{
		SCOPE_OPTIONS =
		[
			new ChoiceOption<RowScope>(RowScope.Any, "All rows", "Rows that were already in the table and rows generated earlier in this run."),
			new ChoiceOption<RowScope>(RowScope.Generated, "Generated rows only", "Only rows inserted earlier in this run (earlier steps, or earlier row sets of the same step)."),
			new ChoiceOption<RowScope>(RowScope.Existing, "Existing rows only", "Only rows that were in the table before this run started.")
		];


		FILTER_OPTIONS =
		[
			new ChoiceOption<LookupFilterKind>(LookupFilterKind.None, "No filter", "Every value of the column can be used."),
			new ChoiceOption<LookupFilterKind>(LookupFilterKind.Pattern, "Matches pattern", "Only values the pattern could produce, e.g. 'S' THEN NUM(digits=5) GREATER THAN 50."),
			new ChoiceOption<LookupFilterKind>(LookupFilterKind.Regex, "Matches regex", "Only values that match a regular expression, e.g. ^S[0-9]{5}$ (needs SQL Server 2025)."),
			new ChoiceOption<LookupFilterKind>(LookupFilterKind.Sql, "SQL condition", "Only values of the rows that meet a SQL condition. s stands for the source table (the table the values come from) and [ ] holds a column name, e.g. s.[Size] = 'XL' AND s.[IsActive] = 1.")
		];
	}
	#endregion STATIC

	#region PUBLIC
	/// <summary>
	///	Creates a lookup builder for one target column and initialises it from the current lookup when present.
	/// </summary>
	/// <param name="tables">
	///	The loaded tables whose values can be used by the lookup.
	/// </param>
	/// <param name="targetTable">
	///	The table that contains the column being configured.
	/// </param>
	/// <param name="targetColumn">
	///	The column being configured.
	/// </param>
	/// <param name="current">
	///	The lookup the column uses now, which the lists start with.
	/// </param>
	/// <param name="parser">
	///	The parser used to format and validate the lookup expression.
	/// </param>
	/// <param name="onChanged">
	///	Called whenever the expression changes, so the Apply command can update.
	/// </param>
	/// <exception cref="ArgumentNullException">
	///	Thrown when <paramref name="tables"/>, <paramref name="targetTable"/>, <paramref name="targetColumn"/>,
	///	<paramref name="parser"/> or <paramref name="onChanged"/> is <see langword="null"/>.
	/// </exception>
	public LookupBuilderViewModel(
		IReadOnlyList<TableModel> tables,
		TableModel                targetTable,
		ColumnModel               targetColumn,
		ColumnLookup?             current,
		ILookupExpressionParser   parser,
		Action                    onChanged
	)
	{
		_selectedTable  = null;
		_selectedColumn = null;
		_isUnique       = false;
		_scope          = RowScope.Any;
		_filterKind     = LookupFilterKind.None;
		_filterText     = string.Empty;
		_expression     = string.Empty;
		_errorMessage   = null;

		ArgumentNullException.ThrowIfNull(tables);
		ArgumentNullException.ThrowIfNull(targetColumn);

		_targetTable = targetTable ?? throw new ArgumentNullException(nameof(targetTable));
		_parser      = parser ?? throw new ArgumentNullException(nameof(parser));
		_onChanged   = onChanged ?? throw new ArgumentNullException(nameof(onChanged));

		IEnumerable<TableModel> candidateTables =
			tables.Contains(targetTable)
				? tables
				: tables.Append(targetTable);

		// Tables of the same database first, so the likely choices are at the top.
		Tables = [..
			candidateTables
				.OrderBy(
					table =>
						string.Equals(table.DatabaseName, targetTable.DatabaseName, StringComparison.OrdinalIgnoreCase)
							? 0
							: 1
				)
				.ThenBy(table => table.DatabaseName, StringComparer.OrdinalIgnoreCase)
				.ThenBy(table => table.DisplayName, StringComparer.OrdinalIgnoreCase)
				.Select(table => new LookupTableOption(table, targetTable))
		];

		if (current is not null)
		{
			_selectedTable  = current.SourceTable;
			_selectedColumn = current.SourceColumn;
			_isUnique       = current.IsUnique;
			_scope          = current.Scope;
			_filterKind     = current.FilterKind;
			_filterText     = current.FilterText;
		}

		UpdateExpression();
	}
	#endregion PUBLIC
	#endregion CONSTRUCTORS

	#region METHODS
	#region PRIVATE
	/// <summary>
	///	Rebuilds the Settings expression from the chosen table, column, scope and filter, then validates it.
	/// </summary>
	private void UpdateExpression()
	{
		if (_selectedTable is null || _selectedColumn is null)
		{
			Expression   = string.Empty;
			ErrorMessage = "Choose the table and the column whose values are used.";
			_onChanged();
			return;
		}

		ColumnLookup lookup = new()
		{
			SourceTable  = _selectedTable,
			SourceColumn = _selectedColumn,
			IsUnique     = _isUnique,
			Scope        = _scope,
			FilterKind   = _filterKind,
			FilterText   = _filterText
		};

		Expression = _parser.Format(lookup, _targetTable);

		ErrorMessage =
			_filterKind != LookupFilterKind.None && _filterText.Trim().Length == 0
				? "Enter the filter, or choose No filter."
				: _parser.TryParse(Expression, _targetTable, out _, out string errorMessage)
					? null
					: errorMessage;

		_onChanged();
	}
	#endregion PRIVATE
	#endregion METHODS
}
