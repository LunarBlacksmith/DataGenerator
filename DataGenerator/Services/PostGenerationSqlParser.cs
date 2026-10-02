using System.Text.RegularExpressions;
using DataGenerator.Models;

namespace DataGenerator.Services;

/// <summary>
/// Turns the post-generation text into statements. A line holding only a name, such as <c>dbo.RebuildTotals</c> or
/// <c>[Sales].[Refresh Summary]; -- totals</c>, is a stored procedure, unless it continues SQL whose last line did not end in a
/// semicolon (or was not followed by a blank line). Every other line is SQL; consecutive SQL lines are run together
/// exactly as typed.
/// </summary>
public sealed partial class PostGenerationSqlParser : IPostGenerationSqlParser
{
	private const int    MAXIMUM_NAME_LENGTH = 60;
	private const string COMMENT_PREFIX      = "--";

	/// <summary>
	/// Words that can stand alone on a line of SQL and must not be mistaken for a stored procedure.
	/// </summary>
	private static readonly HashSet<string> SQL_KEYWORDS = new HashSet<string>(
		[
			"ALL", "AND", "AS", "BEGIN", "BREAK", "BY", "CASE", "CATCH", "CHECKPOINT", "COMMIT", "CONTINUE", "DECLARE",
			"DEFAULT", "DISTINCT", "ELSE", "END", "EXCEPT", "FROM", "GO", "GROUP", "HAVING", "IF", "INTERSECT", "INTO",
			"JOIN", "NOT", "NULL", "ON", "OR", "ORDER", "OUTPUT", "PRINT", "RECONFIGURE", "RETURN", "ROLLBACK", "SELECT",
			"SET", "THEN", "TRAN", "TRANSACTION", "TRY", "UNION", "VALUES", "WHEN", "WHERE", "WHILE", "WITH"
		],
		StringComparer.OrdinalIgnoreCase
	);

	public bool TryParse(string? text, out IReadOnlyList<PostGenerationStatement> statements, out string? error)
	{
		List<PostGenerationStatement> result      = [];
		List<string>                  sqlLines    = [];
		string[]                      lines       = (text ?? string.Empty).Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
		int                           sqlStart    = 0;
		int                           sqlEnd      = 0;
		bool                          sqlComplete = true;

		statements = result;
		error      = null;

		for (int index = 0; index < lines.Length; ++index)
		{
			string line       = lines[index].TrimEnd('\r');
			string trimmed    = line.Trim();
			int    lineNumber = index + 1;

			// A blank line ends a statement just like a semicolon does.
			if (trimmed.Length == 0)
			{
				if (sqlLines.Count > 0)
				{
					sqlLines.Add(line);
				}

				sqlComplete = true;
				continue;
			}

			if (BatchSeparatorRegex().IsMatch(trimmed))
			{
				statements = [];
				error      = $"Line {lineNumber}: GO cannot be used, because everything runs in one transaction. "
					+ "End each statement with a semicolon instead.";
				return false;
			}

			bool isComment = trimmed.StartsWith(COMMENT_PREFIX, StringComparison.Ordinal);

			if (!isComment && sqlComplete && TryGetProcedureName(trimmed, out string procedureName))
			{
				AddSqlBlock(result, sqlLines, sqlStart, sqlEnd);
				result.Add(
					new PostGenerationStatement
					{
						Kind      = PostGenerationStatementKind.StoredProcedure,
						Sql       = $"EXEC {procedureName};",
						Name      = procedureName,
						FirstLine = lineNumber,
						LastLine  = lineNumber
					}
				);
				continue;
			}

			if (sqlLines.Count == 0)
			{
				sqlStart = lineNumber;
			}

			sqlLines.Add(line);
			sqlEnd = lineNumber;

			if (!isComment)
			{
				sqlComplete = StatementEndRegex().IsMatch(trimmed);
			}
		}

		AddSqlBlock(result, sqlLines, sqlStart, sqlEnd);

		return true;
	}

	private static bool TryGetProcedureName(string line, out string procedureName)
	{
		Match match = ProcedureNameRegex().Match(line);

		procedureName = string.Empty;

		if (!match.Success)
		{
			return false;
		}

		string name = match.Groups["name"].Value;

		// A single unbracketed word such as END or COMMIT is SQL, not a procedure.
		if (!name.Contains('.', StringComparison.Ordinal) && !name.StartsWith('[') && SQL_KEYWORDS.Contains(name))
		{
			return false;
		}

		procedureName = name;
		return true;
	}

	/// <summary>
	/// Adds the collected SQL lines as one statement and clears them. Lines that hold only comments are dropped.
	/// </summary>
	private static void AddSqlBlock(List<PostGenerationStatement> statements, List<string> sqlLines, int firstLine, int lastLine)
	{
		bool hasSql = sqlLines.Any(
			line =>
			{
				string trimmed = line.Trim();

				return trimmed.Length > 0 && !trimmed.StartsWith(COMMENT_PREFIX, StringComparison.Ordinal);
			}
		);

		if (hasSql)
		{
			string sql   = string.Join(Environment.NewLine, sqlLines).Trim('\r', '\n');
			string first = sqlLines.Select(line => line.Trim()).First(line => line.Length > 0 && !line.StartsWith(COMMENT_PREFIX, StringComparison.Ordinal));

			statements.Add(
				new PostGenerationStatement
				{
					Kind      = PostGenerationStatementKind.Sql,
					Sql       = sql,
					Name      = first.Length > MAXIMUM_NAME_LENGTH ? $"{first[..MAXIMUM_NAME_LENGTH]}…" : first,
					FirstLine = firstLine,
					LastLine  = lastLine
				}
			);
		}

		sqlLines.Clear();
	}

	/// <summary>
	/// One to four name parts separated by dots, each a regular identifier or a [bracketed] one, optionally followed by a
	/// semicolon and a -- comment.
	/// </summary>
	[GeneratedRegex(@"^(?<name>(?:\[(?:[^\]]|\]\])+\]|[\p{L}_#][\p{L}\p{Nd}_@#$]*)(?:\s*\.\s*(?:\[(?:[^\]]|\]\])+\]|[\p{L}_#][\p{L}\p{Nd}_@#$]*)){0,3})\s*;?\s*(?:--.*)?$", RegexOptions.CultureInvariant)]
	private static partial Regex ProcedureNameRegex();

	[GeneratedRegex(@"^GO(?:\s+\d+)?\s*;?$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
	private static partial Regex BatchSeparatorRegex();

	/// <summary>
	/// A line that ends a statement: its last character, ignoring a trailing -- comment, is a semicolon.
	/// </summary>
	[GeneratedRegex(@";\s*(?:--.*)?$", RegexOptions.CultureInvariant)]
	private static partial Regex StatementEndRegex();
}