using System.Globalization;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DataGenerator.Services;

/// <summary>
///	Reads and writes the indented JSON files of DataGenerator, so they can be read, reviewed, kept in source control
///	and shared.
/// </summary>
internal static class JsonDocumentFile
{
	private const string TEMPORARY_EXTENSION  = ".tmp";
	private const string UNREADABLE_FILE_NAME = "{0}.unreadable-{1:yyyyMMdd-HHmmss}.json";

	private static readonly JsonSerializerOptions SERIALIZER_OPTIONS = new()
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
	///	Reads a JSON document from disk using DataGenerator serializer settings.
	/// </summary>
	/// <typeparam name="T">
	///	The document type to deserialize.
	/// </typeparam>
	/// <param name="filePath">
	///	The JSON file to read.
	/// </param>
	/// <param name="fileDescription">
	///	The user-facing description used in invalid JSON error messages.
	/// </param>
	/// <returns>
	///	The deserialized document, or <see langword="null"/> when the JSON represents null.
	/// </returns>
	/// <exception cref="InvalidDataException">
	///	Thrown when the file is not valid JSON for <typeparamref name="T"/>.
	/// </exception>
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
	///	Writes a JSON document through a temporary file so interrupted saves do not leave partial output.
	/// </summary>
	/// <typeparam name="T">
	///	The document type to serialize.
	/// </typeparam>
	/// <param name="filePath">
	///	The destination JSON file path.
	/// </param>
	/// <param name="document">
	///	The document to write.
	/// </param>
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
	///	Renames an unreadable file so it is kept for reference instead of being overwritten.
	/// </summary>
	/// <param name="filePath">
	///	The file to move aside when it exists.
	/// </param>
	/// <returns>
	///	The new path of the renamed file, or <see langword="null"/> when the file does not exist.
	/// </returns>
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