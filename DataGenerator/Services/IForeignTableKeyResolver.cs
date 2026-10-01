using DataGenerator.Models;

namespace DataGenerator.Services;

/// <summary>
/// Infers relationships from the column naming convention: a column named "&lt;prefix&gt;FTK" (any casing) that is not a
/// declared foreign key is linked to a column named "&lt;prefix&gt;PK" in another table of the same database.
/// </summary>
public interface IForeignTableKeyResolver
{
	/// <returns>The number of inferred relationships that were added to the tables' foreign keys.</returns>
	int ResolveInferredKeys(IReadOnlyList<DatabaseModel> databases);
}