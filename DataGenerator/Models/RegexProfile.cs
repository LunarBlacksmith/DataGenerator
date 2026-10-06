namespace DataGenerator.Models;

public sealed class RegexProfile
{
	#region FIELDS
	#region PUBLIC
	/// <summary>
	///	Ready-made regular expressions offered next to the regex editor of a column rule.
	/// </summary>
	public static readonly IReadOnlyList<RegexProfile> DEFAULT_PROFILES;
	#endregion PUBLIC
	#endregion FIELDS

	#region PROPERTIES
	#region PUBLIC
	public required string Name    { get; init; }
	public required string Pattern { get; init; }
	#endregion PUBLIC
	#endregion PROPERTIES

	#region CONSTRUCTORS
	#region STATIC
	/// <summary>
	///	Sets the default values of the static fields and properties of <see cref="RegexProfile"/>.
	/// </summary>
	static RegexProfile()
	{
		DEFAULT_PROFILES =
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
	}
	#endregion STATIC

	#region PUBLIC
	/// <summary>
	///	Creates a new <see cref="RegexProfile"/>.
	/// </summary>
	public RegexProfile()
	{
	}
	#endregion PUBLIC
	#endregion CONSTRUCTORS

	#region METHODS
	#region PUBLIC
	/// <summary>
	///	Returns the profile name shown in selection controls.
	/// </summary>
	/// <returns>
	///	The profile name.
	/// </returns>
	public override string ToString() => Name;
	#endregion PUBLIC
	#endregion METHODS
}
