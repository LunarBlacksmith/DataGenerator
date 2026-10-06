using DataGenerator.Models;

namespace DataGenerator.Interfaces;

/// <summary>
/// Reads and writes the setting of the "Value from table" mode:
/// <c>[[database.]schema.]Table.Column [UNIQUE] [FROM ANY|GENERATED|EXISTING] [WHERE pattern | WHERE REGEX re | WHERE SQL condition]</c>.
/// See Docs/StepsUpdatesAndLookups.md.
/// </summary>
public interface ILookupExpressionParser
{
	/// <param name="targetTable">The table of the column that receives the values; names without a database refer to its database.</param>
	bool TryParse(string expression, TableModel targetTable, out ColumnLookup? lookup, out string errorMessage);

	/// <summary>
	/// The expression for a lookup, leaving out the database when it is the database of <paramref name="targetTable"/>.
	/// </summary>
	string Format(ColumnLookup lookup, TableModel targetTable);
}
