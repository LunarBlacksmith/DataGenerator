using System.Globalization;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DataGenerator.Services;

/// <summary>
/// Reads and writes the indented JSON files of DataGenerator, so they can be read, reviewed, kept in source control
/// and shared.
/// </summary>
internal static class JsonDocumentFile
{
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

	/// <summary>
	/// Throws <see cref="InvalidDataException"/> when the file is not valid JSON for <typeparamref name="T"/>; the message
	/// calls the file a <paramref name="fileDescription"/>, e.g. "saved settings file".
	/// </summary>
	public static T? Read<T>(string filePath, string fileDescription) where T : class
	{
		try
		{
			using FileStream stream = File.OpenRead(filePath);
			return JsonSerializer.Deserialize<T>(stream, SERIALIZER_OPTIONS);
		}
		catch (JsonException exception)
		{
			throw new InvalidDataException($"'{filePath}' is not a {fileDescription}: {exception.Message}", exception);
		}
	}

	/// <summary>
	/// Writes to a temporary file first, so an interrupted save never leaves a half-written file behind.
	/// </summary>
	public static void Write<T>(string filePath, T document)
	{
		string  fullPath      = Path.GetFullPath(filePath);
		string  temporaryPath = fullPath + TEMPORARY_EXTENSION;
		string? directory     = Path.GetDirectoryName(fullPath);

		if (!string.IsNullOrEmpty(directory))
		{
			_ = Directory.CreateDirectory(directory);
		}

		using (FileStream stream = File.Create(temporaryPath))
		{
			JsonSerializer.Serialize(stream, document, SERIALIZER_OPTIONS);
		}

		File.Move(temporaryPath, fullPath, true);
	}

	/// <summary>
	/// Renames an unreadable file so it is kept for reference instead of being overwritten, and returns its new path.
	/// </summary>
	public static string? SetAside(string filePath)
	{
		if (!File.Exists(filePath))
		{
			return null;
		}

		string unreadablePath = Path.Combine(
			Path.GetDirectoryName(filePath) ?? string.Empty,
			string.Format(
				CultureInfo.InvariantCulture,
				UNREADABLE_FILE_NAME,
				Path.GetFileNameWithoutExtension(filePath),
				DateTime.Now
			)
		);

		File.Move(filePath, unreadablePath, true);
		return unreadablePath;
	}
}