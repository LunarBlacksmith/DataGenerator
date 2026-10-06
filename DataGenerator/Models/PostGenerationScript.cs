namespace DataGenerator.Models;

/// <summary>
///	The SQL that runs at the end of a generation, inside its transaction and just before the commit.
/// </summary>
public sealed class PostGenerationScript
{
	#region PROPERTIES
	#region PUBLIC
	/// <summary>
	///	The database the statements run in, so that names without a database (e.g. dbo.RebuildTotals) are found.
	/// </summary>
	public required string                                 DatabaseName { get; init; }

	public required IReadOnlyList<PostGenerationStatement> Statements   { get; init; }
	#endregion PUBLIC
	#endregion PROPERTIES

	#region CONSTRUCTORS
	#region PUBLIC
	/// <summary>
	///	Creates a new <see cref="PostGenerationScript"/>.
	/// </summary>
	public PostGenerationScript()
	{
	}
	#endregion PUBLIC
	#endregion CONSTRUCTORS
}