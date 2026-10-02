namespace DataGenerator.Models;

/// <summary>
/// One statement of the post-generation SQL, run after the generated rows are inserted and before the transaction is
/// committed.
/// </summary>
public sealed class PostGenerationStatement
{
	public required PostGenerationStatementKind Kind      { get; init; }

	/// <summary>
	/// The SQL that is executed (<c>EXEC name;</c> for a stored procedure).
	/// </summary>
	public required string                      Sql       { get; init; }

	/// <summary>
	/// The procedure name as typed, or a short excerpt of the SQL.
	/// </summary>
	public required string                      Name      { get; init; }

	/// <summary>
	/// The first and last line (1-based) of the statement in the text the user typed.
	/// </summary>
	public required int                         FirstLine { get; init; }
	public required int                         LastLine  { get; init; }

	public string Description
	{
		get
		{
			string lines = FirstLine == LastLine ? $"Line {FirstLine}" : $"Lines {FirstLine}–{LastLine}";

			return Kind == PostGenerationStatementKind.StoredProcedure
				? $"{lines}: stored procedure {Name}"
				: $"{lines}: SQL";
		}
	}
}