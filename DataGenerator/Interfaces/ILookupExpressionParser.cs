using DataGenerator.Models;

namespace DataGenerator.Interfaces;

/// <summary>
///	Reads and writes the setting of the "Value from table" mode:
///	<c>[[database.]schema.]Table.Column [UNIQUE] [FROM ANY|GENERATED|EXISTING] [WHERE pattern | WHERE REGEX re | WHERE SQL condition]</c>.
///	See Docs/StepsUpdatesAndLookups.md.
/// </summary>
public interface ILookupExpressionParser
{
	/// <summary>
	///	Parses a "Value from table" expression and resolves its source table and column against the table catalog.
	/// </summary>
	/// <param name="expression">
	///	The expression to parse. <see langword="null"/> is treated as empty text.
	/// </param>
	/// <param name="targetTable">
	///	The table of the column that receives the values; names without a database refer to its database.
	/// </param>
	/// <param name="lookup">
	///	The resolved lookup when parsing succeeds; otherwise <see langword="null"/>.
	/// </param>
	/// <param name="errorMessage">
	///	Empty text when parsing succeeds; otherwise a user-facing explanation of the problem.
	/// </param>
	/// <returns>
	///	<see langword="true"/> when the expression names a valid lookup; otherwise <see langword="false"/>.
	/// </returns>
	/// <exception cref="ArgumentNullException">
	///	Thrown when <paramref name="targetTable"/> is <see langword="null"/>.
	/// </exception>
	bool TryParse(string expression, TableModel targetTable, out ColumnLookup? lookup, out string errorMessage);

	/// <summary>
	///	The expression for a lookup, leaving out the database when it is the database of <paramref name="targetTable"/>.
	/// </summary>
	/// <param name="lookup">
	///	The lookup to format.
	/// </param>
	/// <param name="targetTable">
	///	The table that owns the rule using the lookup.
	/// </param>
	/// <returns>
	///	The lookup expression, quoted where needed and including UNIQUE, FROM and WHERE options when present.
	/// </returns>
	/// <exception cref="ArgumentNullException">
	///	Thrown when <paramref name="lookup"/> or <paramref name="targetTable"/> is <see langword="null"/>.
	/// </exception>
	string Format(ColumnLookup lookup, TableModel targetTable);
}
