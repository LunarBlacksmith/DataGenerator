using System.Diagnostics;

namespace DataGenerator.Services.Generation;

/// <summary>
///	Reports generation progress without flooding the UI thread with row-level messages.
/// </summary>
internal sealed class GenerationProgress
{
	private const long REPORT_INTERVAL_MILLISECONDS = 200;

	private readonly IProgress<string>? _progress;
	private readonly Stopwatch          _stopwatch = Stopwatch.StartNew();
	private long                        _lastReportMilliseconds = -REPORT_INTERVAL_MILLISECONDS;

	/// <summary>
	///	Creates a progress reporter that throttles row-level messages.
	/// </summary>
	/// <param name="progress">
	///	The UI progress sink, or <see langword="null"/> to suppress messages.
	/// </param>
	public GenerationProgress(IProgress<string>? progress)
	{
		_progress = progress;
	}

	/// <summary>
	///	Reports an immediate progress message and resets the row-progress throttle.
	/// </summary>
	/// <param name="message">
	///	The message to send to the progress sink.
	/// </param>
	public void Report(string message)
	{
		_progress?.Report(message);
		_lastReportMilliseconds = _stopwatch.ElapsedMilliseconds;
	}

	/// <summary>
	///	Reports row progress at most every <see cref="REPORT_INTERVAL_MILLISECONDS"/> milliseconds, and always for the last row.
	/// </summary>
	/// <param name="action">
	///	The current action, such as writing, preparing or inserting.
	/// </param>
	/// <param name="tableName">
	///	The display name of the table being processed.
	/// </param>
	/// <param name="rowSetName">
	///	The name of the row set being processed.
	/// </param>
	/// <param name="completedRows">
	///	How many rows have been processed so far.
	/// </param>
	/// <param name="totalRows">
	///	The total number of rows in the row set.
	/// </param>
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