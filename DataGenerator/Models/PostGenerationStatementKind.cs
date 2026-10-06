namespace DataGenerator.Models;

public enum PostGenerationStatementKind
{
	/// <summary>
	///	A line holding only the name of a stored procedure, which is run with EXEC.
	/// </summary>
	StoredProcedure = 0,

	/// <summary>
	///	SQL that is run exactly as it was typed.
	/// </summary>
	Sql             = 1
}