using System.IO;
using DataGenerator.Models;
using DataGenerator.Services.Generation;

namespace DataGenerator.Services;

/// <summary>
/// Generates test data either as a transactional SQL script or by inserting it directly into SQL Server.
/// </summary>
public sealed class DataGenerationService : IDataGenerationService
{
	private readonly ISqlValueConverter    _converter;
	private readonly IColumnValueGenerator _columnValueGenerator;
	private readonly RowValueBuilder       _rowValueBuilder;

	public DataGenerationService(ISqlValueConverter converter, IColumnValueGenerator columnValueGenerator)
	{
		_converter            = converter ?? throw new ArgumentNullException(nameof(converter));
		_columnValueGenerator = columnValueGenerator ?? throw new ArgumentNullException(nameof(columnValueGenerator));
		_rowValueBuilder      = new RowValueBuilder(columnValueGenerator);
	}

	public async Task GenerateAsync(
		GenerationRequest  request,
		IProgress<string>? progress,
		CancellationToken  cancellationToken
	)
	{
		ArgumentNullException.ThrowIfNull(request);
		ValidateRequest(request);

		GenerationProgress  generationProgress = new GenerationProgress(progress);
		GenerationBlueprint blueprint          = new GenerationBlueprintBuilder(_converter, _columnValueGenerator).Build(request);

		generationProgress.Report(
			$"Preparing {blueprint.TotalRowCount:N0} row(s) in {blueprint.RowSetCount:N0} row set(s) for {blueprint.Tables.Count:N0} table(s)…"
		);

		if (request.Mode == GenerationMode.SqlFile)
		{
			string outputFilePath = Path.GetFullPath(request.OutputFilePath!);

			await new SqlScriptWriter(_converter, _rowValueBuilder).WriteAsync(
				blueprint,
				outputFilePath,
				generationProgress,
				cancellationToken
			);

			generationProgress.Report($"SQL script written to '{outputFilePath}'.");
			return;
		}

		await new DirectDataInserter(_converter, _rowValueBuilder).InsertAsync(
			blueprint,
			request.ConnectionString!,
			generationProgress,
			cancellationToken
		);

		generationProgress.Report($"Inserted {blueprint.TotalRowCount:N0} row(s) and committed the transaction.");
	}

	private static void ValidateRequest(GenerationRequest request)
	{
		if (request.Plans.Count == 0)
		{
			throw new InvalidOperationException("Select at least one table to generate data for.");
		}

		if (request.Mode == GenerationMode.SqlFile && string.IsNullOrWhiteSpace(request.OutputFilePath))
		{
			throw new InvalidOperationException("Select an output SQL file.");
		}

		if (	request.Mode == GenerationMode.SqlFile
				&& SecurePathService.IsInsideApplicationInstallationDirectory(request.OutputFilePath!)
		)
		{
			throw new InvalidOperationException(
				"The SQL output file cannot be stored inside the application installation or project directory. "
				+ "Select a location outside the application directory."
			);
		}

		if (request.Mode == GenerationMode.DirectInsert && string.IsNullOrWhiteSpace(request.ConnectionString))
		{
			throw new InvalidOperationException("A SQL Server connection is required for direct insertion.");
		}
	}
}