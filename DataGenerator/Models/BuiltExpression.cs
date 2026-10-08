namespace DataGenerator.Models;

/// <summary>
///	A named, reusable segment description and its generated expressions.
/// </summary>
public sealed class BuiltExpression
{
	#region PROPERTIES
	public string         Name        { get; set; }
	public string         Feed        { get; set; }
	public string         ColumnName  { get; set; }
	public string         Description { get; set; }
	public string         Pattern     { get; set; }
	public string         Regex       { get; set; }
	public string         Sql         { get; set; }
	public DateTimeOffset SavedAt     { get; set; }
	#endregion PROPERTIES

	#region CONSTRUCTOR
	/// <summary>
	///	Creates an empty expression entry and initializes its default values.
	/// </summary>
	public BuiltExpression()
	{
		Name        = string.Empty;
		Feed        = string.Empty;
		ColumnName  = "Value";
		Description = string.Empty;
		Pattern     = string.Empty;
		Regex       = string.Empty;
		Sql         = string.Empty;
		SavedAt     = default;
	}
	#endregion CONSTRUCTOR
}
