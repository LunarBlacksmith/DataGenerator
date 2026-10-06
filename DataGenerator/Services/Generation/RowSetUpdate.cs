using System.Text;
using DataGenerator.Interfaces;
using DataGenerator.Models;

namespace DataGenerator.Services.Generation;

/// <summary>
///	The T-SQL of an update set: the new values are first stored in a temporary staging table (one row per changed row),
///	then randomly chosen rows of the table that meet the scope and condition are changed to those values.
/// </summary>
internal sealed class RowSetUpdate
{
	#region FIELDS
	#region PUBLIC
	/// <summary>
	///	The alias of the changed table in update conditions, e.g. t.[Size] = 'XL'.
	/// </summary>
	public const string TARGET_ALIAS = "t";

	public const string ROW_NUMBER_COLUMN = "[RowNumber]";
	#endregion PUBLIC

	#region PRIVATE
	private const string TARGETS_NAME     = "[dg_targets]";
	private const string TARGET_ROW_ALIAS = "[d]";
	private const string VALUE_ALIAS      = "[v]";
	private const string TARGET_ROW_NAME  = "[dg_row]";
	#endregion PRIVATE
	#endregion FIELDS

	#region PROPERTIES
	#region PUBLIC
	public int                        Number          { get; }

	/// <summary>
	///	The changed columns, in the order of the staging table's value columns (V0, V1, …).
	/// </summary>
	public IReadOnlyList<ColumnModel> Columns         { get; }

	/// <summary>
	///	The number of rows that must be changed; 0 when changing fewer rows is allowed.
	/// </summary>
	public int                        RequiredCount   { get; }
	public string                     Location        { get; }
	public string                     CreateStatement { get; }

	/// <summary>
	///	Changes the rows; must be followed directly by a check of @@ROWCOUNT.
	/// </summary>
	public string                     UpdateStatement { get; }

	public string StagingTableName => $"#dg_update_{Number}";
	#endregion PUBLIC
	#endregion PROPERTIES

	#region CONSTRUCTORS
	#region PUBLIC
	/// <summary>
	///	Creates the SQL metadata needed to stage and apply one update row set.
	/// </summary>
	/// <param name="number">
	///	Unique number used in the staging table name.
	/// </param>
	/// <param name="table">
	///	The table whose rows will be changed.
	/// </param>
	/// <param name="rowSet">
	///	The update row set plan.
	/// </param>
	/// <param name="columns">
	///	The columns that receive new values.
	/// </param>
	/// <param name="scopeCondition">
	///	The optional generated/existing-row scope condition.
	/// </param>
	/// <param name="converter">
	///	The converter used to declare staging value columns.
	/// </param>
	public RowSetUpdate(
		int                        number,
		TableModel                 table,
		RowSetPlan                 rowSet,
		IReadOnlyList<ColumnModel> columns,
		string?                    scopeCondition,
		ISqlValueConverter         converter
	)
	{
		Number          = number;
		Columns         = columns;
		RequiredCount   = rowSet.RequireAllRows ? rowSet.RowCount : 0;
		Location        = GenerationBlueprintBuilder.DescribeLocation(table, rowSet);
		CreateStatement = BuildCreateStatement(converter);
		UpdateStatement = BuildUpdateStatement(table, rowSet, scopeCondition);
	}
	#endregion PUBLIC
	#endregion CONSTRUCTORS

	#region METHODS
	#region PUBLIC
	/// <summary>
	///	Builds the SQL statement that removes this update staging table if it exists.
	/// </summary>
	/// <returns>
	///	A DROP TABLE IF EXISTS statement for the staging table.
	/// </returns>
	public string BuildDropStatement() => $"DROP TABLE IF EXISTS {StagingTableName};";

	/// <summary>
	///	Builds the INSERT prefix used when filling the update staging table.
	/// </summary>
	/// <returns>
	///	The INSERT INTO statement prefix without VALUES.
	/// </returns>
	public string CreateInsertPrefix()
	{
		IEnumerable<string> valueColumns =
			Enumerable
				.Range(0, Columns.Count)
				.Select(index => SqlSyntax.QuoteIdentifier(GeneratedKeyTable.GetValueColumnName(index)));

		return $"INSERT INTO {StagingTableName} ({ROW_NUMBER_COLUMN}, {string.Join(", ", valueColumns)})";
	}

	/// <summary>
	///	Describes a failed required update when too few target rows matched.
	/// </summary>
	/// <param name="changedCount">
	///	The number of rows changed; null when it is not known (in scripts).
	/// </param>
	/// <returns>
	///	A user-facing explanation of the shortage.
	/// </returns>
	public string DescribeShortage(long? changedCount)
	{
		string found = changedCount is long count ? $"only {count:N0} row(s)" : "fewer rows";

		return $"The update set must change {RequiredCount:N0} row(s), but {found} of the table meet its scope and condition, "
			+ "so nothing was saved. Lower the number of rows, loosen the condition, or clear 'Fail if fewer rows match'.";
	}
	#endregion PUBLIC

	#region PRIVATE
	/// <summary>
	///	Builds the CREATE TABLE statement for the update staging table.
	/// </summary>
	/// <param name="converter">
	///	The converter used to declare value column types.
	/// </param>
	/// <returns>
	///	A CREATE TABLE statement for the staging table.
	/// </returns>
	private string BuildCreateStatement(ISqlValueConverter converter)
	{
		StringBuilder builder = new($"CREATE TABLE {StagingTableName} ({ROW_NUMBER_COLUMN} INT NOT NULL PRIMARY KEY");

		for (int index = 0; index < Columns.Count; ++index)
		{
			_ = builder
				.Append(", ")
				.Append(SqlSyntax.QuoteIdentifier(GeneratedKeyTable.GetValueColumnName(index)))
				.Append(' ')
				.Append(converter.GetTypeDeclaration(Columns[index]))
				.Append(" NULL");
		}

		return builder.Append(");").ToString();
	}

	/// <summary>
	///	Builds the UPDATE statement that applies staged values to randomly chosen matching rows.
	/// </summary>
	/// <param name="table">
	///	The table whose rows will be changed.
	/// </param>
	/// <param name="rowSet">
	///	The update row set plan.
	/// </param>
	/// <param name="scopeCondition">
	///	The optional generated/existing-row scope condition.
	/// </param>
	/// <returns>
	///	The UPDATE statement, ending before the row-count check.
	/// </returns>
	private string BuildUpdateStatement(TableModel table, RowSetPlan rowSet, string? scopeCondition)
	{
		List<string> conditions = [];

		if (scopeCondition is not null)
		{
			conditions.Add(scopeCondition);
		}

		if (!string.IsNullOrWhiteSpace(rowSet.UpdateCondition))
		{
			conditions.Add(SqlSyntax.WrapCondition(rowSet.UpdateCondition.Trim()));
		}

		string targetColumns = string.Join(
			", ",
			Columns
				.Select(column => $"{TARGET_ALIAS}.{SqlSyntax.QuoteIdentifier(column.Name)}")
		);
		string where         = conditions.Count == 0 ? string.Empty : $" WHERE {string.Join(" AND ", conditions)}";
		string assignments   = string.Join(
			", ",
			Columns
				.Select(
					(column, index) => $"{TARGET_ROW_ALIAS}.{SqlSyntax.QuoteIdentifier(column.Name)} = "
						+ $"{VALUE_ALIAS}.{SqlSyntax.QuoteIdentifier(GeneratedKeyTable.GetValueColumnName(index))}"
				)
		);

		// ROW_NUMBER over NEWID() numbers the matching rows in random order; row N receives the values of staging row N.
		return $"WITH {TARGETS_NAME} AS (SELECT {targetColumns}, ROW_NUMBER() OVER (ORDER BY NEWID()) AS {TARGET_ROW_NAME} "
			+ $"FROM {table.FullyQualifiedName} AS {TARGET_ALIAS}{where}) "
			+ $"UPDATE {TARGET_ROW_ALIAS} SET {assignments} "
			+ $"FROM {TARGETS_NAME} AS {TARGET_ROW_ALIAS} "
			+ $"INNER JOIN {StagingTableName} AS {VALUE_ALIAS} ON {VALUE_ALIAS}.{ROW_NUMBER_COLUMN} = {TARGET_ROW_ALIAS}.{TARGET_ROW_NAME};";
	}
	#endregion PRIVATE
	#endregion METHODS
}
