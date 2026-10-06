using DataGenerator.Interfaces;
using DataGenerator.Models;

namespace DataGenerator.Services;

public sealed class TableCatalog : ITableCatalog
{
	private IReadOnlyList<TableModel> _tables = [];

	public IReadOnlyList<TableModel> Tables => _tables;

	/// <summary>
	///	Replaces the catalog contents with a snapshot of the supplied tables.
	/// </summary>
	/// <param name="tables">
	///	The tables to make available for lookup resolution.
	/// </param>
	/// <exception cref="ArgumentNullException">
	///	Thrown when <paramref name="tables"/> is <see langword="null"/>.
	/// </exception>
	public void SetTables(IEnumerable<TableModel> tables)
	{
		ArgumentNullException.ThrowIfNull(tables);

		_tables = [.. tables];
	}
}
