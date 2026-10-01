using DataGenerator.Models;

namespace DataGenerator.Services;

public interface ISqlMetadataService
{
	Task<IReadOnlyList<DatabaseModel>> LoadMetadataAsync(
		string             connectionString,
		IProgress<string>? progress,
		CancellationToken  cancellationToken
	);
}