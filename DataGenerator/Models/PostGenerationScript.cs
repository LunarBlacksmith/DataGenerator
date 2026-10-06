namespace DataGenerator.Models;

/// <summary>
///	The SQL that runs at the end of a generation, inside its transaction and just before the commit.
/// </summary>
public sealed class PostGenerationScript
{
	/// <summary>
	///	The database the statements run in, so that names without a database (e.g. dbo.RebuildTotals) are found.
	/// </summary>
	public required string                                 DatabaseName { get; init; }

	public required IReadOnlyList<PostGenerationStatement> Statements   { get; init; }
}