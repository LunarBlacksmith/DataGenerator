using System.IO;
using DataGenerator.Interfaces;
using DataGenerator.Models;

namespace DataGenerator.Services;

/// <summary>
///	Stores saved column settings as indented JSON, so the files can be read, reviewed, kept in source control and shared.
/// </summary>
public sealed class JsonSavedSettingsStore : ISavedSettingsStore
{
	private const int    FORMAT_VERSION   = 1;
	private const string FILE_DESCRIPTION = "saved column settings file";

	/// <summary>
	///	Creates a store for the saved column settings library file.
	/// </summary>
	/// <param name="libraryFilePath">
	///	The JSON file path used for the saved column settings library.
	/// </param>
	/// <exception cref="ArgumentNullException">
	///	Thrown when <paramref name="libraryFilePath"/> is <see langword="null"/>.
	/// </exception>
	/// <exception cref="ArgumentException">
	///	Thrown when <paramref name="libraryFilePath"/> is empty or white space.
	/// </exception>
	public JsonSavedSettingsStore(string libraryFilePath)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(libraryFilePath);
		LibraryFilePath = libraryFilePath;
	}

	public string LibraryFilePath { get; }

	/// <summary>
	///	Loads the saved column settings library when it exists.
	/// </summary>
	/// <returns>
	///	The saved column settings, or an empty list when the library file does not exist.
	/// </returns>
	public IReadOnlyList<SavedColumnSetting> LoadLibrary() => File.Exists(LibraryFilePath) ? Read(LibraryFilePath) : [];

	/// <summary>
	///	Saves the column settings library file.
	/// </summary>
	/// <param name="settings">
	///	The settings to save.
	/// </param>
	public void SaveLibrary(IReadOnlyList<SavedColumnSetting> settings) => Write(LibraryFilePath, settings);

	/// <summary>
	///	Moves the library file aside when it exists.
	/// </summary>
	/// <returns>
	///	The new path of the renamed library file, or <see langword="null"/> when it does not exist.
	/// </returns>
	public string? SetAsideLibraryFile() => JsonDocumentFile.SetAside(LibraryFilePath);

	/// <summary>
	///	Reads saved column settings from an import file.
	/// </summary>
	/// <param name="filePath">
	///	The import file to read.
	/// </param>
	/// <returns>
	///	The imported saved column settings.
	/// </returns>
	/// <exception cref="ArgumentNullException">
	///	Thrown when <paramref name="filePath"/> is <see langword="null"/>.
	/// </exception>
	/// <exception cref="ArgumentException">
	///	Thrown when <paramref name="filePath"/> is empty or white space.
	/// </exception>
	public IReadOnlyList<SavedColumnSetting> Import(string filePath)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
		return Read(filePath);
	}

	/// <summary>
	///	Writes saved column settings to an export file.
	/// </summary>
	/// <param name="filePath">
	///	The export file to write.
	/// </param>
	/// <param name="settings">
	///	The settings to export.
	/// </param>
	/// <exception cref="ArgumentNullException">
	///	Thrown when <paramref name="filePath"/> is <see langword="null"/>.
	/// </exception>
	/// <exception cref="ArgumentException">
	///	Thrown when <paramref name="filePath"/> is empty or white space.
	/// </exception>
	public void Export(string filePath, IReadOnlyList<SavedColumnSetting> settings)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
		Write(filePath, settings);
	}

	/// <summary>
	///	Checks a setting read from a file, trims text and fills in missing optional values.
	/// </summary>
	/// <param name="setting">
	///	The setting to validate and normalise.
	/// </param>
	/// <param name="location">
	///	The user-facing location of the setting in the file.
	/// </param>
	/// <returns>
	///	The validated and normalised setting.
	/// </returns>
	/// <exception cref="InvalidDataException">
	///	Thrown when the setting has no name or an unknown generation mode.
	/// </exception>
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

	/// <summary>
	///	Reads and normalises a saved column settings document.
	/// </summary>
	/// <param name="filePath">
	///	The JSON file to read.
	/// </param>
	/// <returns>
	///	The normalised saved column settings from the file.
	/// </returns>
	/// <exception cref="InvalidDataException">
	///	Thrown when the file is not a saved column settings document or was saved by a newer format.
	/// </exception>
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

	/// <summary>
	///	Writes saved column settings inside the current document format.
	/// </summary>
	/// <param name="filePath">
	///	The JSON file to write.
	/// </param>
	/// <param name="settings">
	///	The settings to write.
	/// </param>
	/// <exception cref="ArgumentNullException">
	///	Thrown when <paramref name="settings"/> is <see langword="null"/>.
	/// </exception>
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