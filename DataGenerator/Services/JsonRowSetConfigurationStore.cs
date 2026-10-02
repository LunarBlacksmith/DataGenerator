using System.IO;
using DataGenerator.Models;

namespace DataGenerator.Services;

/// <summary>
/// Stores set configurations as indented JSON with a "setConfigurations" list, so the files can be reviewed and shared
/// and are never mistaken for saved column settings files (which have a "settings" list).
/// </summary>
public sealed class JsonRowSetConfigurationStore : IRowSetConfigurationStore
{
	private const int    FORMAT_VERSION   = 1;
	private const string FILE_DESCRIPTION = "set configurations file";

	public JsonRowSetConfigurationStore(string libraryFilePath)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(libraryFilePath);
		LibraryFilePath = libraryFilePath;
	}

	public string LibraryFilePath { get; }

	public IReadOnlyList<SavedRowSetConfiguration> LoadLibrary() => File.Exists(LibraryFilePath) ? Read(LibraryFilePath) : [];

	public void SaveLibrary(IReadOnlyList<SavedRowSetConfiguration> configurations) => Write(LibraryFilePath, configurations);

	public string? SetAsideLibraryFile() => JsonDocumentFile.SetAside(LibraryFilePath);

	public IReadOnlyList<SavedRowSetConfiguration> Import(string filePath)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
		return Read(filePath);
	}

	public void Export(string filePath, IReadOnlyList<SavedRowSetConfiguration> configurations)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
		Write(filePath, configurations);
	}

	private static List<SavedRowSetConfiguration> Read(string filePath)
	{
		RowSetConfigurationsDocument? document = JsonDocumentFile.Read<RowSetConfigurationsDocument>(filePath, FILE_DESCRIPTION);

		if (document?.SetConfigurations is null)
		{
			throw new InvalidDataException(
				$"'{filePath}' is not a {FILE_DESCRIPTION}: it has no \"setConfigurations\" list. "
					+ "Saved column settings files are imported from Saved column settings instead."
			);
		}

		if (document.FormatVersion > FORMAT_VERSION)
		{
			throw new InvalidDataException(
				$"'{filePath}' was saved by a newer version of DataGenerator (format {document.FormatVersion}). Update DataGenerator to read it."
			);
		}

		List<SavedRowSetConfiguration> configurations = new List<SavedRowSetConfiguration>(document.SetConfigurations.Count);

		for (int index = 0; index < document.SetConfigurations.Count; ++index)
		{
			configurations.Add(Normalize(document.SetConfigurations[index], $"Set configuration {index + 1} in '{filePath}'"));
		}

		return configurations;
	}

	private static SavedRowSetConfiguration Normalize(SavedRowSetConfiguration? configuration, string location)
	{
		if (configuration is null || string.IsNullOrWhiteSpace(configuration.Name))
		{
			throw new InvalidDataException($"{location} has no name.");
		}

		configuration.Name      = configuration.Name.Trim();
		configuration.TableName = configuration.TableName?.Trim() ?? string.Empty;
		configuration.Columns ??= [];

		for (int index = 0; index < configuration.Columns.Count; ++index)
		{
			string             columnLocation = $"Column {index + 1} of '{configuration.Name}' in {location}";
			SavedColumnSetting column         = JsonSavedSettingsStore.Normalize(configuration.Columns[index], columnLocation);

			if (column.ColumnName is null)
			{
				throw new InvalidDataException($"{columnLocation} does not name its column.");
			}

			column.ApplyAutomatically    = false;
			configuration.Columns[index] = column;
		}

		return configuration;
	}

	private static void Write(string filePath, IReadOnlyList<SavedRowSetConfiguration> configurations)
	{
		ArgumentNullException.ThrowIfNull(configurations);

		JsonDocumentFile.Write(
			filePath,
			new RowSetConfigurationsDocument
			{
				FormatVersion     = FORMAT_VERSION,
				SetConfigurations = [.. configurations]
			}
		);
	}

	private sealed class RowSetConfigurationsDocument
	{
		public int                              FormatVersion     { get; set; }
		public List<SavedRowSetConfiguration?>? SetConfigurations { get; set; }
	}
}