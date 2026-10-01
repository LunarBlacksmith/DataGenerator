namespace DataGenerator.Models;

public sealed class RegexProfile
{
	public required string Name    { get; init; }
	public required string Pattern { get; init; }

	public override string ToString() => Name;
}
