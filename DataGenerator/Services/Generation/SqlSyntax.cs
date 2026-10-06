using System.Text;

namespace DataGenerator.Services.Generation;

internal static class SqlSyntax
{
	/// <summary>
	///	Quotes a SQL Server identifier with brackets and escapes closing brackets inside it.
	/// </summary>
	/// <param name="identifier">
	///	The identifier to quote.
	/// </param>
	/// <returns>
	///	The bracket-quoted identifier.
	/// </returns>
	public static string QuoteIdentifier(string identifier)
		=> $"[{identifier.Replace("]", "]]", StringComparison.Ordinal)}]";

	/// <summary>
	///	Quotes text as a Unicode SQL string literal and escapes single quotes inside it.
	/// </summary>
	/// <param name="text">
	///	The text to quote.
	/// </param>
	/// <returns>
	///	The quoted Unicode string literal.
	/// </returns>
	public static string QuoteUnicodeText(string text)
		=> $"N'{text.Replace("'", "''", StringComparison.Ordinal)}'";

	/// <summary>
	///	Formats a fully qualified SQL Server table name.
	/// </summary>
	/// <param name="databaseName">
	///	The database name.
	/// </param>
	/// <param name="schemaName">
	///	The schema name.
	/// </param>
	/// <param name="tableName">
	///	The table name.
	/// </param>
	/// <returns>
	///	The three-part table name with each identifier quoted.
	/// </returns>
	public static string FormatTableName(string databaseName, string schemaName, string tableName)
		=> $"{QuoteIdentifier(databaseName)}.{QuoteIdentifier(schemaName)}.{QuoteIdentifier(tableName)}";

	/// <summary>
	///	Puts a condition typed by the user in parentheses. A line break is added before the closing parenthesis when the
	///	condition contains a -- comment, so the comment cannot hide the rest of the statement.
	/// </summary>
	/// <param name="condition">
	///	The SQL condition text to wrap.
	/// </param>
	/// <returns>
	///	The condition enclosed in parentheses.
	/// </returns>
	public static string WrapCondition(string condition)
		=> condition.Contains("--", StringComparison.Ordinal) ? $"({condition}{Environment.NewLine})" : $"({condition})";

	/// <summary>
	///	Makes text safe to embed in a single-line <c>--</c> comment.
	/// </summary>
	/// <param name="text">
	///	The text to sanitise.
	/// </param>
	/// <returns>
	///	The text with control characters replaced by spaces.
	/// </returns>
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