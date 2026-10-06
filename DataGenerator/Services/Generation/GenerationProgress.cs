using System.Diagnostics;

namespace DataGenerator.Services.Generation;

/// <summary>
/// Reports generation progress without flooding the UI thread with row-level messages.
/// </summary>
internal sealed class GenerationProgress
{
	private const long REPORT_INTERVAL_MILLISECONDS = 200;

	private readonly IProgress<string>? _progress;
	private readonly Stopwatch          _stopwatch = Stopwatch.StartNew();
	private long                        _lastReportMilliseconds = -REPORT_INTERVAL_MILLISECONDS;

	public GenerationProgress(IProgress<string>? progress)
	{
		_progress = progress;
	}

	public void Report(string message)
	{
		_progress?.Report(message);
		_lastReportMilliseconds = _stopwatch.ElapsedMilliseconds;
	}

	/// <summary>
	/// Reports row progress at most every <see cref="REPORT_INTERVAL_MILLISECONDS"/> milliseconds, and always for the last row.
	/// </summary>
	public void ReportRows(string action, string tableName, string rowSetName, long completedRows, long totalRows)
	{
		if (_progress is null)
		{
			return;
		}

		long elapsed = _stopwatch.ElapsedMilliseconds;

		if (completedRows < totalRows && elapsed - _lastReportMilliseconds < REPORT_INTERVAL_MILLISECONDS)
		{
			return;
		}

		double percentage = totalRows == 0 ? 100 : completedRows * 100d / totalRows;

		_progress.Report($"{action} {tableName} › Set '{rowSetName}': {completedRows:N0} of {totalRows:N0} rows ({percentage:0}%)");
		_lastReportMilliseconds = elapsed;
	}
}