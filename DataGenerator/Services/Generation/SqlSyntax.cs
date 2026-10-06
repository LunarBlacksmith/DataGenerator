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
	/// Puts a condition typed by the user in parentheses. A line break is added before the closing parenthesis when the
	/// condition contains a -- comment, so the comment cannot hide the rest of the statement.
	/// </summary>
	public static string WrapCondition(string condition)
		=> condition.Contains("--", StringComparison.Ordinal) ? $"({condition}{Environment.NewLine})" : $"({condition})";

	/// <summary>
	/// Makes text safe to embed in a single-line <c>--</c> comment.
	/// </summary>
	public static string ToCommentText(string text)
	{
		StringBuilder builder = new(text.Length);

		foreach (char character in text)
		{
			_ = builder.Append(char.IsControl(character) ? ' ' : character);
		}

		return builder.ToString();
	}
}