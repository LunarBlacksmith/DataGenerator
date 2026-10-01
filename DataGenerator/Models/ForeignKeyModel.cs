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
}
