using System.IO;

namespace DataGenerator.Services;

public static class SecurePathService
{
	#region FIELDS
	#region PRIVATE
	private const string COMPANY_DIRECTORY        = "LocalTools";
	private const string APPLICATION_DIRECTORY    = "DataGenerator";
	private const string GENERATED_DATA_DIRECTORY = "GeneratedData";
	private const string METADATA_DIRECTORY       = "Metadata";
	private const string SAVED_SETTINGS_FILE      = "SavedColumnSettings.json";
	private const string SET_CONFIGURATIONS_FILE  = "SavedSetConfigurations.json";
	private const string PREFERENCES_FILE         = "Preferences.json";
	#endregion PRIVATE
	#endregion FIELDS

	#region CONSTRUCTORS
	#region STATIC
	/// <summary>
	///	Initialises the static state of <see cref="SecurePathService"/>.
	/// </summary>
	static SecurePathService()
	{
	}
	#endregion STATIC
	#endregion CONSTRUCTORS

	#region METHODS
	#region PUBLIC
	/// <summary>
	///	Creates and returns the application data directory under the user's local application data folder.
	/// </summary>
	/// <returns>
	///	The full path of the application data directory.
	/// </returns>
	public static string GetApplicationDataDirectory()
	{
		string localApplicationData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
		string directory = Path.Combine(
			localApplicationData,
			COMPANY_DIRECTORY,
			APPLICATION_DIRECTORY
		);

		_ = Directory.CreateDirectory(directory);
		return directory;
	}

	/// <summary>
	///	Creates and returns the directory used for generated SQL data files.
	/// </summary>
	/// <returns>
	///	The full path of the generated data directory.
	/// </returns>
	public static string GetGeneratedDataDirectory()
	{
		string directory = Path.Combine(GetApplicationDataDirectory(), GENERATED_DATA_DIRECTORY);
		_ = Directory.CreateDirectory(directory);
		return directory;
	}

	/// <summary>
	///	Creates and returns the directory used for cached database metadata.
	/// </summary>
	/// <returns>
	///	The full path of the metadata directory.
	/// </returns>
	public static string GetMetadataDirectory()
	{
		string directory = Path.Combine(GetApplicationDataDirectory(), METADATA_DIRECTORY);
		_ = Directory.CreateDirectory(directory);
		return directory;
	}

	/// <summary>
	///	Builds the file path of the saved column settings library.
	/// </summary>
	/// <returns>
	///	The full path of the saved column settings file.
	/// </returns>
	public static string GetSavedSettingsFilePath() => Path.Combine(GetApplicationDataDirectory(), SAVED_SETTINGS_FILE);

	/// <summary>
	///	Builds the file path of the saved row-set configurations library.
	/// </summary>
	/// <returns>
	///	The full path of the saved row-set configurations file.
	/// </returns>
	public static string GetSetConfigurationsFilePath() => Path.Combine(GetApplicationDataDirectory(), SET_CONFIGURATIONS_FILE);

	/// <summary>
	///	Builds the file path of the user preferences file.
	/// </summary>
	/// <returns>
	///	The full path of the preferences file.
	/// </returns>
	public static string GetPreferencesFilePath() => Path.Combine(GetApplicationDataDirectory(), PREFERENCES_FILE);

	/// <summary>
	///	Creates a timestamped default name for an exported SQL data file.
	/// </summary>
	/// <returns>
	///	A file name such as <c>generated-data-20240131-235959.sql</c>.
	/// </returns>
	public static string CreateDefaultSqlFileName() => $"generated-data-{DateTime.Now:yyyyMMdd-HHmmss}.sql";

	/// <summary>
	///	Checks whether a path points inside the application's installation directory.
	/// </summary>
	/// <param name="candidatePath">
	///	The path to check.
	/// </param>
	/// <returns>
	///	<see langword="true"/> when the path is the installation directory or one of its descendants; otherwise
	///	<see langword="false"/>.
	/// </returns>
	public static bool IsInsideApplicationInstallationDirectory(string candidatePath)
	{
		if (string.IsNullOrWhiteSpace(candidatePath))
		{
			return false;
		}

		string installationDirectory =
			Path.GetFullPath(AppContext.BaseDirectory)
				.TrimEnd(
					Path.DirectorySeparatorChar,
					Path.AltDirectorySeparatorChar
				);

		string fullCandidatePath =
			Path.GetFullPath(candidatePath)
				.TrimEnd(
					Path.DirectorySeparatorChar,
					Path.AltDirectorySeparatorChar
				);

		string installationPrefix = installationDirectory + Path.DirectorySeparatorChar;

		return
			fullCandidatePath.Equals(
				installationDirectory,
				StringComparison.OrdinalIgnoreCase
			)
			|| fullCandidatePath.StartsWith(
				installationPrefix,
				StringComparison.OrdinalIgnoreCase
			);
	}
	#endregion PUBLIC
	#endregion METHODS
}