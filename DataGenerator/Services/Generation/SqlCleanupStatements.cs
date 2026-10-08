using DataGenerator.Models;

namespace DataGenerator.Services.Generation;

/// <summary>
///	T-SQL used to empty tables before new data is inserted. Shared by the script writer and the direct inserter.
/// </summary>
internal static class SqlCleanupStatements
{
	#region FIELDS
	public const string RESEED_VARIABLE_NAME = "@dg_reseed";
	#endregion FIELDS

	#region CONSTRUCTOR
	/// <summary>
	///	Initialises the static state of <see cref="SqlCleanupStatements"/>.
	/// </summary>
	static SqlCleanupStatements()
	{
	}
	#endregion CONSTRUCTOR

	#region METHODS
	/// <summary>
	///	Checks whether a table contains an identity column.
	/// </summary>
	/// <param name="table">
	///	The table to inspect.
	/// </param>
	/// <returns>
	///	<see langword="true"/> when the table has an identity column; otherwise <see langword="false"/>.
	/// </returns>
	public static bool HasIdentityColumn(TableModel table)
		=>
			table
				.Columns
				.Any(column => column.IsIdentity);

	/// <summary>
	///	Builds the SQL statement that deletes all rows from a table.
	/// </summary>
	/// <param name="table">
	///	The table to empty.
	/// </param>
	/// <returns>
	///	A DELETE statement for the table.
	/// </returns>
	public static string CreateDeleteStatement(TableModel table) => $"DELETE FROM {table.FullyQualifiedName};";

	/// <summary>
	///	Builds the variable declaration required by identity reseed statements.
	/// </summary>
	/// <returns>
	///	A DECLARE statement for the reseed value variable.
	/// </returns>
	public static string CreateReseedDeclaration() => $"DECLARE {RESEED_VARIABLE_NAME} BIGINT;";

	/// <summary>
	///	Restarts the identity at its seed, so the next inserted row receives the seed value. Tables that never
	///	contained rows already start at their seed and are skipped. Requires <see cref="CreateReseedDeclaration"/>.
	/// </summary>
	/// <param name="table">
	///	The table whose identity value should be reset.
	/// </param>
	/// <returns>
	///	The SQL statements that read and apply the reseed value.
	/// </returns>
	public static IReadOnlyList<string> CreateReseedStatements(TableModel table)
	{
		string databaseName = SqlSyntax.QuoteIdentifier(table.DatabaseName);
		string objectName   = SqlSyntax.QuoteUnicodeText(table.FullyQualifiedName);
		string localName    = $"{SqlSyntax.QuoteIdentifier(table.SchemaName)}.{SqlSyntax.QuoteIdentifier(table.Name)}";
		string command      = $"DBCC CHECKIDENT ({SqlSyntax.QuoteUnicodeText(localName)}, RESEED, @value) WITH NO_INFOMSGS;";

		return
		[
			$"SET {RESEED_VARIABLE_NAME} = (SELECT CONVERT(BIGINT, [seed_value]) - CONVERT(BIGINT, [increment_value]) "
				+ $"FROM {databaseName}.[sys].[identity_columns] "
				+ $"WHERE [object_id] = OBJECT_ID({objectName}) AND [last_value] IS NOT NULL);",
			$"IF {RESEED_VARIABLE_NAME} IS NOT NULL EXEC {databaseName}.[sys].[sp_executesql] "
				+ $"{SqlSyntax.QuoteUnicodeText(command)}, N'@value BIGINT', @value = {RESEED_VARIABLE_NAME};"
		];
	}
	#endregion METHODS
}