using DataGenerator.Models;

namespace DataGenerator.Interfaces;

public interface ISqlMetadataService
{
	/// <summary>
	///	Loads accessible user databases, tables, columns and foreign keys from SQL Server.
	/// </summary>
	/// <param name="connectionString">
	///	The SQL Server connection string used first against the server and then against each accessible database.
	/// </param>
	/// <param name="progress">
	///	Optional progress sink that receives user-facing status messages.
	/// </param>
	/// <param name="cancellationToken">
	///	Token used to cancel database queries.
	/// </param>
	/// <returns>
	///	The loaded database metadata, or an empty list when no non-system databases are accessible.
	/// </returns>
	Task<IReadOnlyList<DatabaseModel>> LoadMetadataAsync(
		string             connectionString,
		IProgress<string>? progress,
		CancellationToken  cancellationToken
	);
}