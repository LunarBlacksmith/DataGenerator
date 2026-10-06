using DataGenerator.Interfaces;
using DataGenerator.Models;

namespace DataGenerator.Services;

public sealed class TableCatalog : ITableCatalog
{
	private IReadOnlyList<TableModel> _tables = [];

	public IReadOnlyList<TableModel> Tables => _tables;

	public void SetTables(IEnumerable<TableModel> tables)
	{
		ArgumentNullException.ThrowIfNull(tables);

		_tables = [.. tables];
	}
}
