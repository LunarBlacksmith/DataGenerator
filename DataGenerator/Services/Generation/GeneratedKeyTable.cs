using DataGenerator.Models;

namespace DataGenerator.Services.Generation;

/// <summary>
///	Key values of one generated table that dependent tables use through "Generated key" rules.
/// </summary>
internal sealed class GeneratedKeyTable
{
	#region FIELDS
	private readonly List<object?[]> _rows;
	private int                      _outputRowCount;
	#endregion FIELDS

	#region PROPERTIES
	public TableModel                 Table   { get; }
	public IReadOnlyList<ColumnModel> Columns { get; }

	/// <summary>
	///	Script mode only: the table variable that SQL Server fills through an OUTPUT clause. When set, values are
	///	referenced by row number instead of being known up front (for example identity values).
	/// </summary>
	public string? OutputVariableName { get; }

	public int RowCount => OutputVariableName is null ? _rows.Count : _outputRowCount;
	#endregion PROPERTIES

	#region CONSTRUCTOR
	/// <summary>
	///	Creates a table that stores generated key values for later row sets.
	/// </summary>
	/// <param name="table">
	///	The table whose generated keys are stored.
	/// </param>
	/// <param name="columns">
	///	The key columns captured for dependent tables.
	/// </param>
	/// <param name="outputVariableName">
	///	The script table variable used for SQL Server-produced values, or <see langword="null"/> for in-memory values.
	/// </param>
	public GeneratedKeyTable(TableModel table, IReadOnlyList<ColumnModel> columns, string? outputVariableName)
	{
		_rows           = [];
		_outputRowCount = 0;

		Table              = table;
		Columns            = columns;
		OutputVariableName = outputVariableName;
	}
	#endregion CONSTRUCTOR

	#region METHODS
	/// <summary>
	///	Gets the generated value-column name for a captured key column.
	/// </summary>
	/// <param name="columnIndex">
	///	Zero-based index of the captured key column.
	/// </param>
	/// <returns>
	///	The script and temporary-table column name for the captured value.
	/// </returns>
	public static string GetValueColumnName(int columnIndex) => $"V{columnIndex}";

	/// <summary>
	///	Finds the position of a captured key column by name.
	/// </summary>
	/// <param name="columnName">
	///	The column name to find, matched case-insensitively.
	/// </param>
	/// <returns>
	///	The zero-based column index, or -1 when the column is not captured.
	/// </returns>
	public int IndexOf(string columnName)
	{
		for (int index = 0; index < Columns.Count; ++index)
		{
			if (string.Equals(Columns[index].Name, columnName, StringComparison.OrdinalIgnoreCase))
			{
				return index;
			}
		}

		return -1;
	}

	/// <summary>
	///	Adds a direct-insertion key row whose values are already known.
	/// </summary>
	/// <param name="values">
	///	The captured key values in <see cref="Columns"/> order.
	/// </param>
	/// <exception cref="InvalidOperationException">
	///	Thrown when the table captures keys through a script OUTPUT variable.
	/// </exception>
	public void AddRow(object?[] values)
	{
		if (OutputVariableName is not null)
		{
			throw new InvalidOperationException("Rows of an OUTPUT-captured key table are added with AddOutputRow.");
		}

		_rows.Add(values);
	}

	/// <summary>
	///	Records one script OUTPUT row whose values will be read by row number later.
	/// </summary>
	public void AddOutputRow() => ++_outputRowCount;

	/// <summary>
	///	Gets a captured key value for a generated-key reference.
	/// </summary>
	/// <param name="rowIndex">
	///	Zero-based index of the captured key row.
	/// </param>
	/// <param name="columnIndex">
	///	Zero-based index of the captured key column.
	/// </param>
	/// <returns>
	///	The captured value, or a SQL fragment that reads the value from the output table variable.
	/// </returns>
	public object? GetValue(int rowIndex, int columnIndex)
	{
		if (OutputVariableName is null)
		{
			return _rows[rowIndex][columnIndex];
		}

		string valueColumn = SqlSyntax.QuoteIdentifier(GetValueColumnName(columnIndex));

		return new SqlFragment($"(SELECT {valueColumn} FROM {OutputVariableName} WHERE [RowNumber] = {rowIndex + 1})");
	}
	#endregion METHODS
}