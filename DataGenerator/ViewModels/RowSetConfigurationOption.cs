using System.Globalization;
using System.Text;
using DataGenerator.Models;

namespace DataGenerator.ViewModels;

/// <summary>
/// A saved set configuration offered in the set configuration menu of a row set.
/// </summary>
public sealed class RowSetConfigurationOption
{
	public RowSetConfigurationOption(SavedRowSetConfiguration configuration, int matchingColumnCount, bool isFromActiveTable)
	{
		Configuration       = configuration ?? throw new ArgumentNullException(nameof(configuration));
		MatchingColumnCount = matchingColumnCount;
		IsFromActiveTable   = isFromActiveTable;
		Summary             = isFromActiveTable
			? $"{configuration.Columns.Count} columns · saved {configuration.SavedAt.ToString("g", CultureInfo.CurrentCulture)}"
			: $"From {configuration.TableName} · {matchingColumnCount} of its {configuration.Columns.Count} columns are in this table";
		ToolTipText         = BuildToolTipText(configuration);
	}

	public SavedRowSetConfiguration Configuration       { get; }
	public int                      MatchingColumnCount { get; }
	public bool                     IsFromActiveTable   { get; }
	public string                   Summary             { get; }
	public string                   ToolTipText         { get; }

	public string Name => Configuration.Name;

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
}