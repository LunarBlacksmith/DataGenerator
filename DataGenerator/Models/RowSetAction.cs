namespace DataGenerator.Models;

/// <summary>
/// Whether a row set inserts new rows or changes rows that are already in the table.
/// </summary>
public enum RowSetAction
{
	Insert = 0,
	Update = 1
}
