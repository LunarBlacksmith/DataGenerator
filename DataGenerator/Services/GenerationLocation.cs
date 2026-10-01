using DataGenerator.Models;
using DataGenerator.Services.Generation;

namespace DataGenerator.Services;

/// <summary>
/// Formats the "where" part of validation and generation errors, e.g. <c>[Shop].[dbo].[Shirt] › Set 'Cheap' › Row 3 › Column [Price]</c>.
/// </summary>
public static class GenerationLocation
{
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
}