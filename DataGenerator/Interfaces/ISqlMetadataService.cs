using DataGenerator.Models;

namespace DataGenerator.Interfaces;

public interface ISqlMetadataService
{
	Task<IReadOnlyList<DatabaseModel>> LoadMetadataAsync(
		string             connectionString,
		IProgress<string>? progress,
		CancellationToken  cancellationToken
	);
}