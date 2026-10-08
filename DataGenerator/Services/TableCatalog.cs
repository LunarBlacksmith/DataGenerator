using DataGenerator.Interfaces;
using DataGenerator.Models;

namespace DataGenerator.Services;

public sealed class TableCatalog : ITableCatalog
{
	#region FIELDS
	private IReadOnlyList<TableModel> _tables;
	#endregion FIELDS

	#region PROPERTIES
	public IReadOnlyList<TableModel> Tables => _tables;
	#endregion PROPERTIES

	#region CONSTRUCTOR
	/// <summary>
	///	Creates a new <see cref="TableCatalog"/> and sets the default values of its fields and properties.
	/// </summary>
	public TableCatalog()
	{
		_tables = [];
	}
	#endregion CONSTRUCTOR

	#region METHODS
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
	#endregion METHODS
}
