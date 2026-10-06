namespace DataGenerator.Models;

public sealed class ForeignKeyModel
{
	public required string Name               { get; init; }

	public required string ParentDatabase     { get; init; }

	public required string ParentSchema       { get; init; }

	public required string ParentTable        { get; init; }

	public required string ParentColumn       { get; init; }

	public required string ReferencedDatabase { get; init; }

	public required string ReferencedSchema   { get; init; }

	public required string ReferencedTable    { get; init; }

	public required string ReferencedColumn   { get; init; }

	/// <summary>
	/// True when the relationship was inferred from the "...FTK" / "...PK", "...TK" or "..._tk" column naming convention
	/// instead of being declared as a foreign key constraint in SQL Server.
	/// </summary>
	public bool IsInferred                    { get; init; }

	public string ReferencedTableKey => TableModel.CreateKey(ReferencedDatabase, ReferencedSchema, ReferencedTable);

	public string ReferencedColumnKey => $"{ReferencedTableKey}.{ReferencedColumn}";

	public string ReferencedDisplayName => $"{ReferencedSchema}.{ReferencedTable}.{ReferencedColumn}";
}
