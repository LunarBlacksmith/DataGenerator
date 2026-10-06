namespace DataGenerator.Models;

/// <summary>
/// The generation modes and settings of every column of a row set, saved under a name so a whole set can be set up
/// again in one go, in another row set, session or on a colleague's computer. Kept apart from
/// <see cref="SavedColumnSetting"/>s, which each hold the settings of a single column.
/// </summary>
public sealed class SavedRowSetConfiguration
{
	public string Name { get; set; } = string.Empty;

	/// <summary>
	/// The schema.table of the row set the configuration was saved from.
	/// </summary>
	public string TableName { get; set; } = string.Empty;

	public DateTime SavedAt { get; set; }

	/// <summary>
	/// One entry per column; <see cref="SavedColumnSetting.ColumnName"/> names the column it belongs to.
	/// </summary>
	public List<SavedColumnSetting> Columns { get; set; } = [];

	public bool IsFromTable(string tableName) => string.Equals(TableName, tableName, StringComparison.OrdinalIgnoreCase);

	public SavedColumnSetting? FindColumn(string columnName)
		=> Columns.FirstOrDefault(column => string.Equals(column.ColumnName, columnName, StringComparison.OrdinalIgnoreCase));

	public SavedRowSetConfiguration Clone() => new SavedRowSetConfiguration
	{
		Name      = Name,
		TableName = TableName,
		SavedAt   = SavedAt,
		Columns   = [.. Columns.Select(column => column.Clone())]
	};
}