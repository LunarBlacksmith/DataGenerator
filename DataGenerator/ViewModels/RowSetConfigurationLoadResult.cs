namespace DataGenerator.ViewModels;

/// <summary>
///	What happened when a set configuration was loaded into a row set.
/// </summary>
public sealed class RowSetConfigurationLoadResult
{
	public int UpdatedCount { get; init; }

	/// <summary>
	///	Columns of the configuration that the row set has, but that could not use the saved settings, with the reason.
	/// </summary>
	public IReadOnlyList<(string ColumnName, string Problem)> Skipped { get; init; } = [];

	/// <summary>
	///	Columns of the row set that the configuration does not mention; they keep their settings.
	/// </summary>
	public IReadOnlyList<string> NotInConfiguration { get; init; } = [];

	/// <summary>
	///	Columns of the configuration that the row set does not have.
	/// </summary>
	public IReadOnlyList<string> UnknownColumns { get; init; } = [];
}