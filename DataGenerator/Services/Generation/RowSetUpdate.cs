using System.Text;
using DataGenerator.Interfaces;
using DataGenerator.Models;

namespace DataGenerator.Services.Generation;

/// <summary>
/// The T-SQL of an update set: the new values are first stored in a temporary staging table (one row per changed row),
/// then randomly chosen rows of the table that meet the scope and condition are changed to those values.
/// </summary>
internal sealed class RowSetUpdate
{
	/// <summary>
	/// The alias of the changed table in update conditions, e.g. t.[Size] = 'XL'.
	/// </summary>
	public const string TARGET_ALIAS = "t";

	public const string ROW_NUMBER_COLUMN = "[RowNumber]";

	private const string TARGETS_NAME     = "[dg_targets]";
	private const string TARGET_ROW_ALIAS = "[d]";
	private const string VALUE_ALIAS      = "[v]";
	private const string TARGET_ROW_NAME  = "[dg_row]";

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

	public int                        Number          { get; }

	/// <summary>
	/// The changed columns, in the order of the staging table's value columns (V0, V1, …).
	/// </summary>
	public IReadOnlyList<ColumnModel> Columns         { get; }

	/// <summary>
	/// The number of rows that must be changed; 0 when changing fewer rows is allowed.
	/// </summary>
	public int                        RequiredCount   { get; }
	public string                     Location        { get; }
	public string                     CreateStatement { get; }

	/// <summary>
	/// Changes the rows; must be followed directly by a check of @@ROWCOUNT.
	/// </summary>
	public string                     UpdateStatement { get; }

	public string StagingTableName => $"#dg_update_{Number}";

	public string BuildDropStatement() => $"DROP TABLE IF EXISTS {StagingTableName};";

	public string CreateInsertPrefix()
	{
		IEnumerable<string> valueColumns = Enumerable.Range(0, Columns.Count)
			.Select(index => SqlSyntax.QuoteIdentifier(GeneratedKeyTable.GetValueColumnName(index)));

		return $"INSERT INTO {StagingTableName} ({ROW_NUMBER_COLUMN}, {string.Join(", ", valueColumns)})";
	}

	/// <param name="changedCount">The number of rows changed; null when it is not known (in scripts).</param>
	public string DescribeShortage(long? changedCount)
	{
		string found = changedCount is long count ? $"only {count:N0} row(s)" : "fewer rows";

		return $"The update set must change {RequiredCount:N0} row(s), but {found} of the table meet its scope and condition, "
			+ "so nothing was saved. Lower the number of rows, loosen the condition, or clear 'Fail if fewer rows match'.";
	}

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

		string targetColumns = string.Join(", ", Columns.Select(column => $"{TARGET_ALIAS}.{SqlSyntax.QuoteIdentifier(column.Name)}"));
		string where         = conditions.Count == 0 ? string.Empty : $" WHERE {string.Join(" AND ", conditions)}";
		string assignments   = string.Join(
			", ",
			Columns.Select(
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
}
