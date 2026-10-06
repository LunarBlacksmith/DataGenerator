using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using DataGenerator.Interfaces;
using DataGenerator.Models;

namespace DataGenerator.Services;

/// <summary>
/// Stores the user's preferences as indented JSON.
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

	public JsonUserPreferencesStore(string filePath)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
		_filePath = filePath;
	}

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