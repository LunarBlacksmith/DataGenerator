using DataGenerator.Models;

namespace DataGenerator.ViewModels;

/// <summary>
/// A table in the table list of the Value from table builder; tables of other databases show the database name.
/// </summary>
public sealed class LookupTableOption
{
	public LookupTableOption(TableModel table, TableModel targetTable)
	{
		ArgumentNullException.ThrowIfNull(targetTable);

		Table       = table ?? throw new ArgumentNullException(nameof(table));
		IsSameTable = ReferenceEquals(table, targetTable);
		DisplayName = string.Equals(table.DatabaseName, targetTable.DatabaseName, StringComparison.OrdinalIgnoreCase)
			? table.DisplayName
			: $"{table.DatabaseName}.{table.DisplayName}";

		if (IsSameTable)
		{
			DisplayName += " (this table)";
		}
	}

	public TableModel Table       { get; }
	public bool       IsSameTable { get; }
	public string     DisplayName { get; }

	public override string ToString() => DisplayName;
}
