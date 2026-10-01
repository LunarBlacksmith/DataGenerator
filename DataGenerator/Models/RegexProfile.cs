namespace DataGenerator.Models;

public sealed class RegexProfile
{
	/// <summary>
	/// Ready-made regular expressions offered next to the regex editor of a column rule.
	/// </summary>
	public static readonly IReadOnlyList<RegexProfile> DEFAULT_PROFILES =
	[
		new RegexProfile
		{
			Name    = "Uppercase code",
			Pattern = "[A-Z]{8}"
		},
		new RegexProfile
		{
			Name    = "Asset reference",
			Pattern = "ASSET-[0-9]{6}"
		},
		new RegexProfile
		{
			Name    = "Bin location",
			Pattern = "[A-Z]{2}-[0-9]{3}-[A-Z]{1}"
		},
		new RegexProfile
		{
			Name    = "Australian test mobile",
			Pattern = "04[0-9]{8}"
		}
	];

	public required string Name    { get; init; }
	public required string Pattern { get; init; }

	public override string ToString() => Name;
}
