namespace DataGenerator.Services;

/// <summary>
/// Gives access to the values already generated for the other columns of the row being built.
/// </summary>
public interface IRowValueLookup
{
	/// <summary>
	/// Returns the value of <paramref name="columnName"/> in the current row. Throws <see cref="InvalidOperationException"/>
	/// when the column is unknown or has no value yet.
	/// </summary>
	object? GetValue(string columnName);
}