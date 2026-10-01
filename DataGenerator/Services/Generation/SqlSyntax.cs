using System.Text;

namespace DataGenerator.Services.Generation;

internal static class SqlSyntax
{
	public static string QuoteIdentifier(string identifier)
		=> $"[{identifier.Replace("]", "]]", StringComparison.Ordinal)}]";

	public static string QuoteUnicodeText(string text)
		=> $"N'{text.Replace("'", "''", StringComparison.Ordinal)}'";

	public static string FormatTableName(string databaseName, string schemaName, string tableName)
		=> $"{QuoteIdentifier(databaseName)}.{QuoteIdentifier(schemaName)}.{QuoteIdentifier(tableName)}";

	/// <summary>
	/// Makes text safe to embed in a single-line <c>--</c> comment.
	/// </summary>
	public static string ToCommentText(string text)
	{
		StringBuilder builder = new StringBuilder(text.Length);

		foreach (char character in text)
		{
			_ = builder.Append(char.IsControl(character) ? ' ' : character);
		}

		return builder.ToString();
	}
}