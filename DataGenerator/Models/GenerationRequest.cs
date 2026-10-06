namespace DataGenerator.Models;

public sealed class GenerationRequest
{
	public required IReadOnlyList<TableGenerationPlan> Plans              { get; init; }
	public required GenerationMode                     Mode               { get; init; }
	public string?                                     OutputFilePath     { get; init; }
	public string?                                     ConnectionString   { get; init; }

	/// <summary>
	///	Tables whose existing rows are deleted (inside the same transaction) before any data is inserted.
	/// </summary>
	public IReadOnlyList<TableModel>                   TablesToClear      { get; init; } = [];

	/// <summary>
	///	Resets the identity seed of every cleared table so new rows start numbering from the original seed again.
	/// </summary>
	public bool                                        ResetIdentitySeeds { get; init; }

	/// <summary>
	///	Stored procedures or SQL that run after the inserts and before the commit; null when there are none.
	/// </summary>
	public PostGenerationScript?                       PostGeneration     { get; init; }
}
