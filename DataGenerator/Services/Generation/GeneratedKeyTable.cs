using DataGenerator.Models;

namespace DataGenerator.Services.Generation;

/// <summary>
/// Key values of one generated table that dependent tables use through "Generated key" rules.
/// </summary>
internal sealed class GeneratedKeyTable
{
	private readonly List<object?[]> _rows = [];
	private int                      _outputRowCount;

	public GeneratedKeyTable(TableModel table, IReadOnlyList<ColumnModel> columns, string? outputVariableName)
	{
		Table              = table;
		Columns            = columns;
		OutputVariableName = outputVariableName;
	}

	public TableModel                 Table   { get; }
	public IReadOnlyList<ColumnModel> Columns { get; }

	/// <summary>
	/// Script mode only: the table variable that SQL Server fills through an OUTPUT clause. When set, values are
	/// referenced by row number instead of being known up front (for example identity values).
	/// </summary>
	public string? OutputVariableName { get; }

	public int RowCount => OutputVariableName is null ? _rows.Count : _outputRowCount;

	public static string GetValueColumnName(int columnIndex) => $"V{columnIndex}";

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

	public void AddRow(object?[] values)
	{
		if (OutputVariableName is not null)
		{
			throw new InvalidOperationException("Rows of an OUTPUT-captured key table are added with AddOutputRow.");
		}

		_rows.Add(values);
	}

	public void AddOutputRow() => ++_outputRowCount;

	public object? GetValue(int rowIndex, int columnIndex)
	{
		if (OutputVariableName is null)
		{
			return _rows[rowIndex][columnIndex];
		}

		string valueColumn = SqlSyntax.QuoteIdentifier(GetValueColumnName(columnIndex));

		return new SqlFragment($"(SELECT {valueColumn} FROM {OutputVariableName} WHERE [RowNumber] = {rowIndex + 1})");
	}
}