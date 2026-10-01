using DataGenerator.Models;

namespace DataGenerator.Services.Generation;

/// <summary>
/// T-SQL used to empty tables before new data is inserted. Shared by the script writer and the direct inserter.
/// </summary>
internal static class SqlCleanupStatements
{
	public const string RESEED_VARIABLE_NAME = "@dg_reseed";

	public static bool HasIdentityColumn(TableModel table) => table.Columns.Any(column => column.IsIdentity);

	public static string CreateDeleteStatement(TableModel table) => $"DELETE FROM {table.FullyQualifiedName};";

	public static string CreateReseedDeclaration() => $"DECLARE {RESEED_VARIABLE_NAME} BIGINT;";

	/// <summary>
	/// Restarts the identity at its seed, so the next inserted row receives the seed value. Tables that never
	/// contained rows already start at their seed and are skipped. Requires <see cref="CreateReseedDeclaration"/>.
	/// </summary>
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
}