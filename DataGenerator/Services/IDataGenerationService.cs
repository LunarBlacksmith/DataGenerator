using DataGenerator.Models;

namespace DataGenerator.Services;

public interface IDataGenerationService
{
	Task GenerateAsync(
		GenerationRequest  request,
		IProgress<string>? progress,
		CancellationToken  cancellationToken
	);
}