using DataGenerator.Models;

namespace DataGenerator.ViewModels;

/// <summary>
///	A table in the table list of the Value from table builder; tables of other databases show the database name.
/// </summary>
public sealed class LookupTableOption
{
	#region PROPERTIES
	public TableModel Table       { get; }
	public bool       IsSameTable { get; }
	public string     DisplayName { get; }
	#endregion PROPERTIES

	#region CONSTRUCTOR
	/// <summary>
	///	Creates a table choice for a lookup builder and marks whether it is the target table.
	/// </summary>
	/// <param name="table">
	///	The table whose values can be used.
	/// </param>
	/// <param name="targetTable">
	///	The table that contains the column being configured.
	/// </param>
	/// <exception cref="ArgumentNullException">
	///	Thrown when <paramref name="table"/> or <paramref name="targetTable"/> is <see langword="null"/>.
	/// </exception>
	public LookupTableOption(TableModel table, TableModel targetTable)
	{
		ArgumentNullException.ThrowIfNull(targetTable);

		Table       = table ?? throw new ArgumentNullException(nameof(table));
		IsSameTable = ReferenceEquals(table, targetTable);
		DisplayName =
			string.Equals(table.DatabaseName, targetTable.DatabaseName, StringComparison.OrdinalIgnoreCase)
				? table.DisplayName
				: $"{table.DatabaseName}.{table.DisplayName}";

		if (IsSameTable)
		{
			DisplayName += " (this table)";
		}
	}
	#endregion CONSTRUCTOR

	#region METHODS
	/// <summary>
	///	Returns the text shown for this table choice.
	/// </summary>
	/// <returns>
	///	The display name, including the database or "this table" label when needed.
	/// </returns>
	public override string ToString() => DisplayName;
	#endregion METHODS
}
