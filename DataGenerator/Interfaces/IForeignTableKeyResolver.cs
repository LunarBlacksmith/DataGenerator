using DataGenerator.Models;

namespace DataGenerator.Interfaces;

/// <summary>
/// Infers relationships from the column naming convention: a column named "&lt;prefix&gt;FTK" (any casing) that is not a
/// declared foreign key is linked to a key column with the same prefix in another table of the same database: one named
/// "&lt;prefix&gt;PK" (any casing), "&lt;prefix&gt;TK" where the prefix ends in a lower-case letter, or "&lt;prefix&gt;_tk"
/// (any casing). An underscore between the prefix and the suffix is ignored, so tag_FTK also matches tagPK.
/// </summary>
public interface IForeignTableKeyResolver
{
	/// <returns>The number of inferred relationships that were added to the tables' foreign keys.</returns>
	int ResolveInferredKeys(IReadOnlyList<DatabaseModel> databases);
}