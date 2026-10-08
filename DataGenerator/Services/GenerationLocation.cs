using DataGenerator.Models;
using DataGenerator.Services.Generation;

namespace DataGenerator.Services;

/// <summary>
///	Formats the "where" part of validation and generation errors, e.g. <c>[Shop].[dbo].[Shirt] › Set 'Cheap' › Row 3 › Column [Price]</c>.
/// </summary>
public static class GenerationLocation
{
	#region CONSTRUCTOR
	/// <summary>
	///	Initialises the static state of <see cref="GenerationLocation"/>.
	/// </summary>
	static GenerationLocation()
	{
	}
	#endregion CONSTRUCTOR

	#region METHODS
	/// <summary>
	///	Builds a readable location for a table, optional row set, row index and column.
	/// </summary>
	/// <param name="table">
	///	The table where the event occurred.
	/// </param>
	/// <param name="rowSetName">
	///	The optional row set name to include.
	/// </param>
	/// <param name="rowIndex">
	///	The optional zero-based row index to display as a one-based row number.
	/// </param>
	/// <param name="columnName">
	///	The optional column name to quote and include.
	/// </param>
	/// <returns>
	///	The formatted location string.
	/// </returns>
	/// <exception cref="ArgumentNullException">
	///	Thrown when <paramref name="table"/> is <see langword="null"/>.
	/// </exception>
	public static string Describe(TableModel table, string? rowSetName = null, long? rowIndex = null, string? columnName = null)
	{
		ArgumentNullException.ThrowIfNull(table);

		string location = table.FullyQualifiedName;

		if (rowSetName is not null)
		{
			location += $" › Set '{rowSetName}'";
		}

		if (rowIndex is not null)
		{
			location += $" › Row {rowIndex.Value + 1:N0}";
		}

		if (columnName is not null)
		{
			location += $" › Column {SqlSyntax.QuoteIdentifier(columnName)}";
		}

		return location;
	}
	#endregion METHODS
}