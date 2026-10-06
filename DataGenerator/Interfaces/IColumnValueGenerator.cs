using DataGenerator.Models;

namespace DataGenerator.Interfaces;

/// <summary>
///	Produces client-side values for the Random, Fixed, Sequence, Regex, Pattern, Copy of column and Null modes.
///	Key-reference and database-generated modes are resolved by the generation engine.
/// </summary>
public interface IColumnValueGenerator
{
	/// <summary>
	///	Generates the value for a column rule in one row, using already-generated row values when the rule refers to
	///	other columns.
	/// </summary>
	/// <param name="rule">
	///	The rule that describes the target column and generation mode.
	/// </param>
	/// <param name="rowIndex">
	///	Zero-based row index within the row set.
	/// </param>
	/// <param name="rowCount">
	///	The number of rows in the row set, used by LAST(...) in patterns.
	/// </param>
	/// <param name="rowValues">
	///	The values of the other columns of the row, used by the Copy of column mode and by COL(...) in patterns.
	/// </param>
	/// <returns>
	///	The generated CLR value, or <see langword="null"/> when the rule explicitly generates NULL for a nullable column.
	/// </returns>
	/// <exception cref="ArgumentNullException">
	///	Thrown when <paramref name="rule"/> is <see langword="null"/>.
	/// </exception>
	/// <exception cref="InvalidOperationException">
	///	Thrown when the rule's mode cannot be generated client-side, needs row values that were not supplied, or produces
	///	NULL for a non-nullable column.
	/// </exception>
	object? Generate(ColumnRule rule, long rowIndex, long rowCount, IRowValueLookup? rowValues = null);

	/// <summary>
	///	The names of the columns of the same row whose values the rule uses.
	/// </summary>
	/// <param name="rule">
	///	The rule to inspect.
	/// </param>
	/// <returns>
	///	The referenced column names in dependency order, or an empty list when the rule has no same-row references or the
	///	pattern is invalid.
	/// </returns>
	/// <exception cref="ArgumentNullException">
	///	Thrown when <paramref name="rule"/> is <see langword="null"/>.
	/// </exception>
	IReadOnlyList<string> GetReferencedColumns(ColumnRule rule);

	/// <summary>
	///	Checks whether a generation mode can produce values before SQL Server executes the script.
	/// </summary>
	/// <param name="mode">
	///	The generation mode to test.
	/// </param>
	/// <returns>
	///	<see langword="true"/> when the mode can generate a client-side value; otherwise <see langword="false"/>.
	/// </returns>
	bool CanGenerate(ValueGenerationMode mode);

	/// <summary>
	///	Describes the random values that would be generated for a column.
	/// </summary>
	/// <param name="column">
	///	The column whose SQL type controls the random range and shape.
	/// </param>
	/// <returns>
	///	A user-facing description of the random values, or a message that random values are unsupported for the type.
	/// </returns>
	/// <exception cref="ArgumentNullException">
	///	Thrown when <paramref name="column"/> is <see langword="null"/>.
	/// </exception>
	string DescribeRandomValues(ColumnModel column);
}