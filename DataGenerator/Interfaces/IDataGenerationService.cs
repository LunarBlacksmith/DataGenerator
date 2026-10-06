using DataGenerator.Models;

namespace DataGenerator.Interfaces;

public interface IDataGenerationService
{
	Task GenerateAsync(
		GenerationRequest  request,
		IProgress<string>? progress,
		CancellationToken  cancellationToken
	);
}