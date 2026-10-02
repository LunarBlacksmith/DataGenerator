using System.Globalization;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using DataGenerator.Models;

namespace DataGenerator.Services;

/// <summary>
/// Stores saved column settings as indented JSON, so the files can be read, reviewed, kept in source control and shared.
/// </summary>
public sealed class JsonSavedSettingsStore : ISavedSettingsStore
{
	private const int    FORMAT_VERSION       = 1;
	private const string TEMPORARY_EXTENSION  = ".tmp";
	private const string UNREADABLE_FILE_NAME = "{0}.unreadable-{1:yyyyMMdd-HHmmss}.json";

	private static readonly JsonSerializerOptions SERIALIZER_OPTIONS = new JsonSerializerOptions
	{
		AllowTrailingCommas         = true,
		Encoder                     = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
		IgnoreReadOnlyProperties    = true,
		PropertyNameCaseInsensitive = true,
		PropertyNamingPolicy        = JsonNamingPolicy.CamelCase,
		ReadCommentHandling         = JsonCommentHandling.Skip,
		WriteIndented               = true,
		Converters                  = { new JsonStringEnumConverter() }
	};

	public JsonSavedSettingsStore(string libraryFilePath)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(libraryFilePath);
		LibraryFilePath = libraryFilePath;
	}

	public string LibraryFilePath { get; }

	public IReadOnlyList<SavedColumnSetting> LoadLibrary() => File.Exists(LibraryFilePath) ? Read(LibraryFilePath) : [];

	public void SaveLibrary(IReadOnlyList<SavedColumnSetting> settings) => Write(LibraryFilePath, settings);

	public string? SetAsideLibraryFile()
	{
		if (!File.Exists(LibraryFilePath))
		{
			return null;
		}

		string unreadablePath = Path.Combine(
			Path.GetDirectoryName(LibraryFilePath) ?? string.Empty,
			string.Format(
				CultureInfo.InvariantCulture,
				UNREADABLE_FILE_NAME,
				Path.GetFileNameWithoutExtension(LibraryFilePath),
				DateTime.Now
			)
		);

		File.Move(LibraryFilePath, unreadablePath, true);
		return unreadablePath;
	}

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

	private static List<SavedColumnSetting> Read(string filePath)
	{
		SavedSettingsDocument? document;

		try
		{
			using FileStream stream = File.OpenRead(filePath);
			document = JsonSerializer.Deserialize<SavedSettingsDocument>(stream, SERIALIZER_OPTIONS);
		}
		catch (JsonException exception)
		{
			throw new InvalidDataException($"'{filePath}' is not a saved settings file: {exception.Message}", exception);
		}

		if (document?.Settings is null)
		{
			throw new InvalidDataException($"'{filePath}' is not a saved settings file: it has no \"settings\" list.");
		}

		if (document.FormatVersion > FORMAT_VERSION)
		{
			throw new InvalidDataException(
				$"'{filePath}' was saved by a newer version of DataGenerator (format {document.FormatVersion}). Update DataGenerator to read it."
			);
		}

		List<SavedColumnSetting> settings = new List<SavedColumnSetting>(document.Settings.Count);

		for (int index = 0; index < document.Settings.Count; ++index)
		{
			SavedColumnSetting? setting = document.Settings[index];

			if (setting is null || string.IsNullOrWhiteSpace(setting.Name))
			{
				throw new InvalidDataException($"Setting {index + 1} in '{filePath}' has no name.");
			}

			if (!Enum.IsDefined(setting.GenerationMode))
			{
				throw new InvalidDataException($"Setting '{setting.Name}' in '{filePath}' has an unknown generation mode.");
			}

			settings.Add(Normalize(setting));
		}

		return settings;
	}

	private static void Write(string filePath, IReadOnlyList<SavedColumnSetting> settings)
	{
		ArgumentNullException.ThrowIfNull(settings);

		string  fullPath      = Path.GetFullPath(filePath);
		string  temporaryPath = fullPath + TEMPORARY_EXTENSION;
		string? directory     = Path.GetDirectoryName(fullPath);

		if (!string.IsNullOrEmpty(directory))
		{
			_ = Directory.CreateDirectory(directory);
		}

		SavedSettingsDocument document = new SavedSettingsDocument
		{
			FormatVersion = FORMAT_VERSION,
			Settings      = [.. settings]
		};

		// Written to a temporary file first so an interrupted save never leaves a half-written library behind.
		using (FileStream stream = File.Create(temporaryPath))
		{
			JsonSerializer.Serialize(stream, document, SERIALIZER_OPTIONS);
		}

		File.Move(temporaryPath, fullPath, true);
	}

	// JSON null overrides the property initialisers, so missing text becomes empty again.
	private static SavedColumnSetting Normalize(SavedColumnSetting setting)
	{
		setting.Name              = setting.Name.Trim();
		setting.FixedValue        = setting.FixedValue ?? string.Empty;
		setting.SequenceStart     = setting.SequenceStart ?? SavedColumnSetting.DEFAULT_SEQUENCE_VALUE;
		setting.SequenceStep      = setting.SequenceStep ?? SavedColumnSetting.DEFAULT_SEQUENCE_VALUE;
		setting.RegexPattern      = setting.RegexPattern ?? string.Empty;
		setting.PatternExpression = setting.PatternExpression ?? string.Empty;
		setting.TableName         = string.IsNullOrWhiteSpace(setting.TableName) ? null : setting.TableName.Trim();
		setting.ColumnName        = string.IsNullOrWhiteSpace(setting.ColumnName) ? null : setting.ColumnName.Trim();
		setting.ApplyAutomatically &= setting.ColumnName is not null;
		return setting;
	}

	private sealed class SavedSettingsDocument
	{
		public int                        FormatVersion { get; set; }
		public List<SavedColumnSetting?>? Settings      { get; set; }
	}
}