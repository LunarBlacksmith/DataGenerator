namespace DataGenerator.Models;

public sealed class GenerationRequest
{
	public required IReadOnlyList<TableModel> Tables { get; init; }
	public required GenerationMode Mode              { get; init; }
	public string?                 OutputFilePath    { get; init; }
	public string?                 ConnectionString  { get; init; }
}
