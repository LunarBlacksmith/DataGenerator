using System.Text;
using DataGenerator.Interfaces;
using DataGenerator.Models;

namespace DataGenerator.Services.Generation;

/// <summary>
/// A random sample of key values that already exist in a referenced table, used by "Existing key" rules.
/// Composite foreign keys share one pool so every column of the key comes from the same referenced row.
/// </summary>
internal sealed class ExistingKeyPool
{
	public const int MAXIMUM_SAMPLE_SIZE = 10_000;

	private const int SCRIPT_CHOICE_RANGE = 1_000_000_000;

	private List<object?[]>? _rows;

	public ExistingKeyPool(
		int                        number,
		ForeignKeyModel            reference,
		IReadOnlyList<string>      referencedColumns,
		IReadOnlyList<ColumnModel> targetColumns,
		bool                       usesScriptVariables
	)
	{
		Number              = number;
		ReferencedTableName = SqlSyntax.FormatTableName(reference.ReferencedDatabase, reference.ReferencedSchema, reference.ReferencedTable);
		ReferencedColumns   = referencedColumns;
		TargetColumns       = targetColumns;
		UsesScriptVariables = usesScriptVariables;
	}

	public int                        Number              { get; }
	public string                     ReferencedTableName { get; }
	public IReadOnlyList<string>      ReferencedColumns   { get; }

	/// <summary>
	/// The columns that receive the values; their types are used for the sampled values.
	/// </summary>
	public IReadOnlyList<ColumnModel> TargetColumns       { get; }

	public bool                       UsesScriptVariables { get; }

	public string VariableName      => $"@dg_existing_{Number}";
	public string CountVariableName => $"@dg_existing_{Number}_count";

	public static string CreateSignature(
		ForeignKeyModel            reference,
		IReadOnlyList<string>      referencedColumns,
		IReadOnlyList<ColumnModel> targetColumns,
		ISqlValueConverter         converter
	)
	{
		StringBuilder builder = new(reference.ReferencedTableKey);

		for (int index = 0; index < referencedColumns.Count; ++index)
		{
			_ = builder
				.Append('|')
				.Append(referencedColumns[index])
				.Append(':')
				.Append(converter.GetTypeDeclaration(targetColumns[index]));
		}

		return builder.ToString();
	}

	public string BuildSelectStatement(ISqlValueConverter converter)
	{
		List<string> selections = [];
		List<string> conditions = [];

		for (int index = 0; index < ReferencedColumns.Count; ++index)
		{
			string column = SqlSyntax.QuoteIdentifier(ReferencedColumns[index]);

			selections.Add($"CONVERT({converter.GetTypeDeclaration(TargetColumns[index])}, {column})");
			conditions.Add($"{column} IS NOT NULL");
		}

		return $"SELECT TOP ({MAXIMUM_SAMPLE_SIZE}) {string.Join(", ", selections)} "
			+ $"FROM {ReferencedTableName} "
			+ $"WHERE {string.Join(" AND ", conditions)} "
			+ "ORDER BY NEWID()";
	}

	public string DescribeEmptyPool()
		=> $"No rows with values in {string.Join(", ", ReferencedColumns.Select(SqlSyntax.QuoteIdentifier))} exist in {ReferencedTableName}, "
			+ "so 'Existing key' columns that reference it cannot be filled. Generate data for that table as well, "
			+ "or choose another generation mode.";

	public void Load(List<object?[]> rows)
	{
		ArgumentNullException.ThrowIfNull(rows);

		if (rows.Count == 0)
		{
			throw new InvalidOperationException(DescribeEmptyPool());
		}

		_rows = rows;
	}

	/// <summary>
	/// Picks a referenced row. In script mode the choice is resolved by SQL Server against the sampled table variable.
	/// </summary>
	public int Choose(Random random)
	{
		if (UsesScriptVariables)
		{
			return random.Next(SCRIPT_CHOICE_RANGE);
		}

		return _rows is null
			? throw new InvalidOperationException($"The existing keys of {ReferencedTableName} have not been loaded.")
			: random.Next(_rows.Count);
	}

	public object? GetValue(int choice, int columnIndex)
	{
		if (!UsesScriptVariables)
		{
			return _rows![choice][columnIndex];
		}

		string valueColumn = SqlSyntax.QuoteIdentifier(GeneratedKeyTable.GetValueColumnName(columnIndex));

		return new SqlFragment($"(SELECT {valueColumn} FROM {VariableName} WHERE [RowNumber] = 1 + ({choice} % {CountVariableName}))");
	}
}