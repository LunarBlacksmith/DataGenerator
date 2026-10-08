namespace DataGenerator.Models;

/// <summary>
///	The row count, generation modes and settings of every column of a row set, saved under a name so a whole set can be set up
///	again in one go, in another row set, session or on a colleague's computer. Kept apart from
///	<see cref="SavedColumnSetting"/>s, which each hold the settings of a single column.
/// </summary>
public sealed class SavedRowSetConfiguration
{
	#region PROPERTIES
	public string Name { get; set; }

	/// <summary>
	///	The schema.table of the row set the configuration was saved from.
	/// </summary>
	public string TableName { get; set; }

	public DateTime SavedAt { get; set; }

	/// <summary>
	///	The saved number of rows. Older configurations have no count and leave the current set's count unchanged.
	/// </summary>
	public int? RowCount { get; set; }

	/// <summary>
	///	One entry per column; <see cref="SavedColumnSetting.ColumnName"/> names the column it belongs to.
	/// </summary>
	public List<SavedColumnSetting> Columns { get; set; }
	#endregion PROPERTIES

	#region CONSTRUCTOR
	/// <summary>
	///	Creates a new <see cref="SavedRowSetConfiguration"/> and sets the default values of its fields and properties.
	/// </summary>
	public SavedRowSetConfiguration()
	{
		Name      = string.Empty;
		TableName = string.Empty;
		SavedAt   = default;
		RowCount  = null;
		Columns   = [];
	}
	#endregion CONSTRUCTOR

	#region METHODS
	/// <summary>
	///	Checks whether this configuration was saved from the given table.
	/// </summary>
	/// <param name="tableName">
	///	The schema.table name to compare with <see cref="TableName"/>.
	/// </param>
	/// <returns>
	///	<see langword="true"/> when the table names match ignoring case; otherwise <see langword="false"/>.
	/// </returns>
	public bool IsFromTable(string tableName) => string.Equals(TableName, tableName, StringComparison.OrdinalIgnoreCase);

	/// <summary>
	///	Finds the saved setting for a column in this configuration.
	/// </summary>
	/// <param name="columnName">
	///	The column name to find.
	/// </param>
	/// <returns>
	///	The matching saved column setting, or <see langword="null"/> when the configuration has no entry for
	///	<paramref name="columnName"/>.
	/// </returns>
	public SavedColumnSetting? FindColumn(string columnName)
		=>
			Columns
				.FirstOrDefault(
					column => string.Equals(
						column.ColumnName,
						columnName,
						StringComparison.OrdinalIgnoreCase
					)
				);

	/// <summary>
	///	Creates a copy of this row-set configuration and all of its column settings.
	/// </summary>
	/// <returns>
	///	A new configuration instance with cloned column settings.
	/// </returns>
	public SavedRowSetConfiguration Clone() => new SavedRowSetConfiguration
	{
		Name      = Name,
		TableName = TableName,
		SavedAt   = SavedAt,
		RowCount  = RowCount,
		Columns   = [..
			Columns
				.Select(column => column.Clone())
		]
	};
	#endregion METHODS
}