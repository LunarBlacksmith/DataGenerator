namespace DataGenerator.Interfaces;

/// <summary>
///	Gives access to the values already generated for the other columns of the row being built.
/// </summary>
public interface IRowValueLookup
{
	/// <summary>
	///	Returns the value of <paramref name="columnName"/> in the current row. Throws <see cref="InvalidOperationException"/>
	///	when the column is unknown or has no value yet.
	/// </summary>
	/// <param name="columnName">
	///	The name of the column whose generated value is needed.
	/// </param>
	/// <returns>
	///	The generated value, including <see langword="null"/> when that column generated NULL.
	/// </returns>
	/// <exception cref="InvalidOperationException">
	///	Thrown when the column is not inserted by the row set or has not generated a value yet.
	/// </exception>
	object? GetValue(string columnName);
}