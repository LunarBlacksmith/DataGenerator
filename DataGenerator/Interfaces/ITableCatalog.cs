using DataGenerator.Models;

namespace DataGenerator.Interfaces;

/// <summary>
/// The tables of every loaded database, so rules can refer to columns of other tables by name.
/// </summary>
public interface ITableCatalog
{
	IReadOnlyList<TableModel> Tables { get; }

	/// <summary>
	/// Replaces the tables after the metadata was (re)loaded.
	/// </summary>
	void SetTables(IEnumerable<TableModel> tables);
}
