using System.IO;
using DataGenerator.Interfaces;
using DataGenerator.Models;

namespace DataGenerator.Services;

/// <summary>
/// Stores saved column settings as indented JSON, so the files can be read, reviewed, kept in source control and shared.
/// </summary>
public sealed class JsonSavedSettingsStore : ISavedSettingsStore
{
	private const int    FORMAT_VERSION   = 1;
	private const string FILE_DESCRIPTION = "saved column settings file";

	public JsonSavedSettingsStore(string libraryFilePath)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(libraryFilePath);
		LibraryFilePath = libraryFilePath;
	}

	public string LibraryFilePath { get; }

	public IReadOnlyList<SavedColumnSetting> LoadLibrary() => File.Exists(LibraryFilePath) ? Read(LibraryFilePath) : [];

	public void SaveLibrary(IReadOnlyList<SavedColumnSetting> settings) => Write(LibraryFilePath, settings);

	public string? SetAsideLibraryFile() => JsonDocumentFile.SetAside(LibraryFilePath);

	public IReadOnlyList<SavedColumnSetting> Import(string filePath)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
		return Read(filePath);
	}

	public void Export(string filePath, IReadOnlyList<SavedColumnSetting> settings)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
		Write(filePath, settings);
	}

	/// <summary>
	/// Checks a setting read from a file and fills in missing text. <paramref name="location"/> describes where the
	/// setting was found, e.g. "Setting 3 in 'settings.json'".
	/// Throws <see cref="InvalidDataException"/> when the setting cannot be used.
	/// </summary>
	internal static SavedColumnSetting Normalize(SavedColumnSetting? setting, string location)
	{
		if (setting is null || string.IsNullOrWhiteSpace(setting.Name))
		{
			throw new InvalidDataException($"{location} has no name.");
		}

		if (!Enum.IsDefined(setting.GenerationMode))
		{
			throw new InvalidDataException($"{location} ('{setting.Name}') has an unknown generation mode.");
		}

		// JSON null overrides the property initialisers, so missing text becomes empty again.
		setting.Name              = setting.Name.Trim();
		setting.FixedValue        = setting.FixedValue ?? string.Empty;
		setting.SequenceStart     = setting.SequenceStart ?? SavedColumnSetting.DEFAULT_SEQUENCE_VALUE;
		setting.SequenceStep      = setting.SequenceStep ?? SavedColumnSetting.DEFAULT_SEQUENCE_VALUE;
		setting.RegexPattern      = setting.RegexPattern ?? string.Empty;
		setting.PatternExpression = setting.PatternExpression ?? string.Empty;
		setting.SourceColumnName  = setting.SourceColumnName?.Trim() ?? string.Empty;
		setting.TableName         = string.IsNullOrWhiteSpace(setting.TableName) ? null : setting.TableName.Trim();
		setting.ColumnName        = string.IsNullOrWhiteSpace(setting.ColumnName) ? null : setting.ColumnName.Trim();
		setting.ApplyAutomatically &= setting.ColumnName is not null;
		return setting;
	}

	private static List<SavedColumnSetting> Read(string filePath)
	{
		SavedSettingsDocument? document = JsonDocumentFile.Read<SavedSettingsDocument>(filePath, FILE_DESCRIPTION);

		if (document?.Settings is null)
		{
			throw new InvalidDataException($"'{filePath}' is not a {FILE_DESCRIPTION}: it has no \"settings\" list.");
		}

		if (document.FormatVersion > FORMAT_VERSION)
		{
			throw new InvalidDataException(
				$"'{filePath}' was saved by a newer version of DataGenerator (format {document.FormatVersion}). Update DataGenerator to read it."
			);
		}

		List<SavedColumnSetting> settings = new(document.Settings.Count);

		for (int index = 0; index < document.Settings.Count; ++index)
		{
			settings.Add(Normalize(document.Settings[index], $"Setting {index + 1} in '{filePath}'"));
		}

		return settings;
	}

	private static void Write(string filePath, IReadOnlyList<SavedColumnSetting> settings)
	{
		ArgumentNullException.ThrowIfNull(settings);

		JsonDocumentFile.Write(
			filePath,
			new SavedSettingsDocument
			{
				FormatVersion = FORMAT_VERSION,
				Settings      = [.. settings]
			}
		);
	}

	private sealed class SavedSettingsDocument
	{
		public int                        FormatVersion { get; set; }
		public List<SavedColumnSetting?>? Settings      { get; set; }
	}
}