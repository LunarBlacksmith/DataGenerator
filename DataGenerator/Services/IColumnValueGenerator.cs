using DataGenerator.Models;

namespace DataGenerator.Services;

/// <summary>
/// Produces client-side values for the Random, Fixed, Sequence, Regex, Pattern and Null modes.
/// Key-reference and database-generated modes are resolved by the generation engine.
/// </summary>
public interface IColumnValueGenerator
{
	/// <param name="rowIndex">Zero-based row index within the row set.</param>
	object? Generate(ColumnRule rule, long rowIndex);

	bool CanGenerate(ValueGenerationMode mode);

	string DescribeRandomValues(ColumnModel column);
}