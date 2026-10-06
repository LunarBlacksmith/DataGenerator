using DataGenerator.Models;

namespace DataGenerator.Interfaces;

/// <summary>
/// Produces client-side values for the Random, Fixed, Sequence, Regex, Pattern, Copy of column and Null modes.
/// Key-reference and database-generated modes are resolved by the generation engine.
/// </summary>
public interface IColumnValueGenerator
{
	/// <param name="rowIndex">Zero-based row index within the row set.</param>
	/// <param name="rowCount">The number of rows in the row set, used by LAST(...) in patterns.</param>
	/// <param name="rowValues">
	/// The values of the other columns of the row, used by the Copy of column mode and by COL(...) in patterns.
	/// </param>
	object? Generate(ColumnRule rule, long rowIndex, long rowCount, IRowValueLookup? rowValues = null);

	/// <summary>
	/// The names of the columns of the same row whose values the rule uses.
	/// </summary>
	IReadOnlyList<string> GetReferencedColumns(ColumnRule rule);

	bool CanGenerate(ValueGenerationMode mode);

	string DescribeRandomValues(ColumnModel column);
}