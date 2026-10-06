using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using DataGenerator.Interfaces;
using DataGenerator.Models;

namespace DataGenerator.Services;

/// <summary>
///	Stores the user's preferences as indented JSON.
/// </summary>
public sealed class JsonUserPreferencesStore : IUserPreferencesStore
{
	private const string TEMPORARY_EXTENSION = ".tmp";

	private static readonly JsonSerializerOptions SERIALIZER_OPTIONS = new()
	{
		AllowTrailingCommas         = true,
		PropertyNameCaseInsensitive = true,
		PropertyNamingPolicy        = JsonNamingPolicy.CamelCase,
		ReadCommentHandling         = JsonCommentHandling.Skip,
		WriteIndented               = true,
		Converters                  = { new JsonStringEnumConverter() }
	};

	private readonly string _filePath;

	/// <summary>
	///	Creates a store for the user preferences file.
	/// </summary>
	/// <param name="filePath">
	///	The JSON file path used to persist preferences.
	/// </param>
	/// <exception cref="ArgumentNullException">
	///	Thrown when <paramref name="filePath"/> is <see langword="null"/>.
	/// </exception>
	/// <exception cref="ArgumentException">
	///	Thrown when <paramref name="filePath"/> is empty or white space.
	/// </exception>
	public JsonUserPreferencesStore(string filePath)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
		_filePath = filePath;
	}

	/// <summary>
	///	Loads user preferences, using defaults when the file is missing or unreadable.
	/// </summary>
	/// <returns>
	///	The normalised preferences from disk, or default preferences when no usable file is available.
	/// </returns>
	public UserPreferences Load()
	{
		if (!File.Exists(_filePath))
		{
			return new UserPreferences();
		}

		try
		{
			using FileStream stream = File.OpenRead(_filePath);

			UserPreferences? preferences = JsonSerializer.Deserialize<UserPreferences>(stream, SERIALIZER_OPTIONS);

			return Normalize(preferences ?? new UserPreferences());
		}
		catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
		{
			// Preferences are only conveniences: the defaults are used and the file is replaced on the next save.
			return new UserPreferences();
		}
	}

	/// <summary>
	///	Saves user preferences through a temporary file so interrupted saves do not leave partial output.
	/// </summary>
	/// <param name="preferences">
	///	The preferences to save.
	/// </param>
	/// <exception cref="ArgumentNullException">
	///	Thrown when <paramref name="preferences"/> is <see langword="null"/>.
	/// </exception>
	public void Save(UserPreferences preferences)
	{
		ArgumentNullException.ThrowIfNull(preferences);

		string  fullPath      = Path.GetFullPath(_filePath);
		string  temporaryPath = fullPath + TEMPORARY_EXTENSION;
		string? directory     = Path.GetDirectoryName(fullPath);

		if (!string.IsNullOrEmpty(directory))
		{
			_ = Directory.CreateDirectory(directory);
		}

		using (FileStream stream = File.Create(temporaryPath))
		{
			JsonSerializer.Serialize(stream, preferences, SERIALIZER_OPTIONS);
		}

		File.Move(temporaryPath, fullPath, true);
	}

	/// <summary>
	///	Normalises loaded preferences by discarding unknown themes and cleaning hidden column keys.
	/// </summary>
	/// <param name="preferences">
	///	The loaded preferences to normalise.
	/// </param>
	/// <returns>
	///	The same preferences object with invalid or duplicate values removed.
	/// </returns>
	private static UserPreferences Normalize(UserPreferences preferences)
	{
		if (preferences.Theme is AppTheme theme && !Enum.IsDefined(theme))
		{
			preferences.Theme = null;
		}

		preferences.HiddenRuleGridColumns =
		[
			.. (preferences.HiddenRuleGridColumns ?? [])
				.Where(key => !string.IsNullOrWhiteSpace(key))
				.Select(key => key.Trim())
				.Distinct(StringComparer.OrdinalIgnoreCase)
		];

		return preferences;
	}
}