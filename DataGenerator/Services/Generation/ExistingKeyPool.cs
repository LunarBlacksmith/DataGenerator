using System.Text;
using DataGenerator.Interfaces;
using DataGenerator.Models;

namespace DataGenerator.Services.Generation;

/// <summary>
///	A random sample of key values that already exist in a referenced table, used by "Existing key" rules.
///	Composite foreign keys share one pool so every column of the key comes from the same referenced row.
/// </summary>
internal sealed class ExistingKeyPool
{
	#region FIELDS
	#region PUBLIC
	public const int MAXIMUM_SAMPLE_SIZE = 10_000;
	#endregion PUBLIC

	#region PRIVATE
	private const int SCRIPT_CHOICE_RANGE = 1_000_000_000;

	private List<object?[]>? _rows;
	#endregion PRIVATE
	#endregion FIELDS

	#region PROPERTIES
	public int                        Number              { get; }
	public string                     ReferencedTableName { get; }
	public IReadOnlyList<string>      ReferencedColumns   { get; }

	/// <summary>
	///	The columns that receive the values; their types are used for the sampled values.
	/// </summary>
	public IReadOnlyList<ColumnModel> TargetColumns       { get; }

	public bool                       UsesScriptVariables { get; }

	public string VariableName      => $"@dg_existing_{Number}";
	public string CountVariableName => $"@dg_existing_{Number}_count";
	#endregion PROPERTIES

	#region CONSTRUCTOR
	/// <summary>
	///	Creates a pool descriptor for one referenced table and key shape.
	/// </summary>
	/// <param name="number">
	///	Unique number used in generated script variable names.
	/// </param>
	/// <param name="reference">
	///	The foreign key whose referenced table supplies the values.
	/// </param>
	/// <param name="referencedColumns">
	///	The referenced column names, in the same order as the target columns.
	/// </param>
	/// <param name="targetColumns">
	///	The columns that receive the sampled key values.
	/// </param>
	/// <param name="usesScriptVariables">
	///	Whether generated scripts, rather than in-memory rows, choose values from the pool.
	/// </param>
	public ExistingKeyPool(
		int                        number,
		ForeignKeyModel            reference,
		IReadOnlyList<string>      referencedColumns,
		IReadOnlyList<ColumnModel> targetColumns,
		bool                       usesScriptVariables
	)
	{
		_rows = null;

		Number              = number;
		ReferencedTableName = SqlSyntax.FormatTableName(reference.ReferencedDatabase, reference.ReferencedSchema, reference.ReferencedTable);
		ReferencedColumns   = referencedColumns;
		TargetColumns       = targetColumns;
		UsesScriptVariables = usesScriptVariables;
	}
	#endregion CONSTRUCTOR

	#region METHODS
	/// <summary>
	///	Builds the cache key that lets compatible existing-key rules share one sampled pool.
	/// </summary>
	/// <param name="reference">
	///	The foreign key that identifies the referenced table.
	/// </param>
	/// <param name="referencedColumns">
	///	The referenced column names, in key order.
	/// </param>
	/// <param name="targetColumns">
	///	The receiving columns whose SQL types control the sample conversion.
	/// </param>
	/// <param name="converter">
	///	The converter used to describe the receiving SQL types.
	/// </param>
	/// <returns>
	///	The stable signature for the referenced table, columns and converted target types.
	/// </returns>
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

	/// <summary>
	///	Builds the SQL statement that samples non-null referenced key values in random order.
	/// </summary>
	/// <param name="converter">
	///	The converter used to convert sampled values to the target column types.
	/// </param>
	/// <returns>
	///	A SELECT statement that returns up to the maximum sample size.
	/// </returns>
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

	/// <summary>
	///	Describes why the pool cannot be used when no referenced rows are available.
	/// </summary>
	/// <returns>
	///	A user-facing explanation for the missing existing-key values.
	/// </returns>
	public string DescribeEmptyPool()
	{
		string columns = string.Join(
			", ",
			ReferencedColumns
				.Select(SqlSyntax.QuoteIdentifier)
		);

		return $"No rows with values in {columns} exist in {ReferencedTableName}, "
			+ "so 'Existing key' columns that reference it cannot be filled. Generate data for that table as well, "
			+ "or choose another generation mode.";
	}

	/// <summary>
	///	Stores the sampled rows that direct insertion will choose from.
	/// </summary>
	/// <param name="rows">
	///	The sampled key rows, with one object array per referenced row.
	/// </param>
	/// <exception cref="ArgumentNullException">
	///	Thrown when <paramref name="rows"/> is <see langword="null"/>.
	/// </exception>
	/// <exception cref="InvalidOperationException">
	///	Thrown when the pool is empty.
	/// </exception>
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
	///	Picks a referenced row. In script mode the choice is resolved by SQL Server against the sampled table variable.
	/// </summary>
	/// <param name="random">
	///	The random number generator used to choose the row or script-side choice value.
	/// </param>
	/// <returns>
	///	The sampled row index, or a script-side random choice value when script variables are used.
	/// </returns>
	/// <exception cref="InvalidOperationException">
	///	Thrown when direct insertion asks for a value before the pool has been loaded.
	/// </exception>
	public int Choose(Random random)
		=>
			UsesScriptVariables
				? random.Next(SCRIPT_CHOICE_RANGE)
				: _rows is null
					? throw new InvalidOperationException($"The existing keys of {ReferencedTableName} have not been loaded.")
					: random.Next(_rows.Count);

	/// <summary>
	///	Gets one column value from a previously chosen referenced row.
	/// </summary>
	/// <param name="choice">
	///	The value returned by <see cref="Choose"/> for the shared key group.
	/// </param>
	/// <param name="columnIndex">
	///	Zero-based index of the referenced column within the pool.
	/// </param>
	/// <returns>
	///	The sampled value, or a SQL fragment that reads it from the script variable.
	/// </returns>
	public object? GetValue(int choice, int columnIndex)
	{
		if (!UsesScriptVariables)
		{
			return _rows![choice][columnIndex];
		}

		string valueColumn = SqlSyntax.QuoteIdentifier(GeneratedKeyTable.GetValueColumnName(columnIndex));

		return new SqlFragment($"(SELECT {valueColumn} FROM {VariableName} WHERE [RowNumber] = 1 + ({choice} % {CountVariableName}))");
	}
	#endregion METHODS
}