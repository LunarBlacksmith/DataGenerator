using System.Globalization;
using System.Text;
using DataGenerator.Models;

namespace DataGenerator.ViewModels;

/// <summary>
///	A saved set configuration offered in the set configuration menu of a row set.
/// </summary>
public sealed class RowSetConfigurationOption
{
	#region PROPERTIES
	#region PUBLIC
	public SavedRowSetConfiguration Configuration       { get; }
	public int                      MatchingColumnCount { get; }
	public bool                     IsFromActiveTable   { get; }
	public string                   Summary             { get; }
	public string                   ToolTipText         { get; }

	public string Name => Configuration.Name;
	#endregion PUBLIC
	#endregion PROPERTIES

	#region CONSTRUCTORS
	#region PUBLIC
	/// <summary>
	///	Creates a saved row-set configuration choice for the active row set.
	/// </summary>
	/// <param name="configuration">
	///	The saved configuration offered to the user.
	/// </param>
	/// <param name="matchingColumnCount">
	///	How many columns of the active row set are present in the configuration.
	/// </param>
	/// <param name="isFromActiveTable">
	///	Whether the configuration was saved from the active table.
	/// </param>
	/// <exception cref="ArgumentNullException">
	///	Thrown when <paramref name="configuration"/> is <see langword="null"/>.
	/// </exception>
	public RowSetConfigurationOption(SavedRowSetConfiguration configuration, int matchingColumnCount, bool isFromActiveTable)
	{
		Configuration       = configuration ?? throw new ArgumentNullException(nameof(configuration));
		MatchingColumnCount = matchingColumnCount;
		IsFromActiveTable   = isFromActiveTable;
		Summary             =
			isFromActiveTable
				? $"{configuration.Columns.Count} columns · saved {configuration.SavedAt.ToString("g", CultureInfo.CurrentCulture)}"
				: $"From {configuration.TableName} · {matchingColumnCount} of its {configuration.Columns.Count} columns are in this table";
		ToolTipText         = BuildToolTipText(configuration);
	}
	#endregion PUBLIC
	#endregion CONSTRUCTORS

	#region METHODS
	#region PRIVATE
	/// <summary>
	///	Builds the tooltip that lists the saved values for every column in a configuration.
	/// </summary>
	/// <param name="configuration">
	///	The configuration to describe.
	/// </param>
	/// <returns>
	///	The multi-line tooltip text.
	/// </returns>
	private static string BuildToolTipText(SavedRowSetConfiguration configuration)
	{
		StringBuilder text = new();

		text.Append("Load into this row set. Saved from ").Append(configuration.TableName).AppendLine(":");

		foreach (SavedColumnSetting column in configuration.Columns)
		{
			text.AppendLine().Append(column.ColumnName).Append(": ").Append(SavedSettingDescriber.DescribeValues(column));
		}

		return text.ToString();
	}
	#endregion PRIVATE
	#endregion METHODS
}