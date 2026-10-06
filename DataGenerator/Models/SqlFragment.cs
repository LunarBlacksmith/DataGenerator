namespace DataGenerator.Models;

/// <summary>
///	A raw T-SQL expression that is resolved by SQL Server when a generated script runs
///	(for example a value captured from an earlier INSERT).
/// </summary>
public sealed record SqlFragment
{
	#region PROPERTIES
	#region PUBLIC
	public string Sql { get; init; }
	#endregion PUBLIC
	#endregion PROPERTIES

	#region CONSTRUCTORS
	#region PUBLIC
	/// <summary>
	///	Creates a new <see cref="SqlFragment"/> from the supplied values.
	/// </summary>
	/// <param name="sql">
	///	The value of <see cref="Sql"/>.
	/// </param>
	public SqlFragment(string sql)
	{
		Sql = sql;
	}
	#endregion PUBLIC
	#endregion CONSTRUCTORS
}