using DataGenerator.Models;

namespace DataGenerator.Interfaces;

public interface IDataGenerationService
{
	/// <summary>
	///	Generates data from the prepared table plans, either by writing a SQL script or by inserting directly into SQL
	///	Server.
	/// </summary>
	/// <param name="request">
	///	The generation request containing mode, connection or output path, plans and post-generation SQL.
	/// </param>
	/// <param name="progress">
	///	Optional progress sink that receives user-facing status messages.
	/// </param>
	/// <param name="cancellationToken">
	///	Token used to cancel SQL, file and generation work.
	/// </param>
	/// <exception cref="ArgumentNullException">
	///	Thrown when <paramref name="request"/> is <see langword="null"/>.
	/// </exception>
	/// <exception cref="InvalidOperationException">
	///	Thrown when the request has no plans, is missing the required output path or connection string, or writes a
	///	script inside the application directory.
	/// </exception>
	Task GenerateAsync(
		GenerationRequest  request,
		IProgress<string>? progress,
		CancellationToken  cancellationToken
	);
}