namespace DataGenerator.Models;

/// <summary>
///	A raw T-SQL expression that is resolved by SQL Server when a generated script runs
///	(for example a value captured from an earlier INSERT).
/// </summary>
public sealed record SqlFragment
{
	#region PROPERTIES
	public string Sql { get; init; }
	#endregion PROPERTIES

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
}