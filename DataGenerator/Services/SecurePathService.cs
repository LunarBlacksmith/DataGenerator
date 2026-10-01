using System.IO;

namespace DataGenerator.Services;

public static class SecurePathService
{
	private const string COMPANY_DIRECTORY        = "LocalTools";
	private const string APPLICATION_DIRECTORY    = "DataGenerator";
	private const string GENERATED_DATA_DIRECTORY = "GeneratedData";
	private const string METADATA_DIRECTORY       = "Metadata";

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

	public static string GetGeneratedDataDirectory()
	{
		string directory = Path.Combine(GetApplicationDataDirectory(), GENERATED_DATA_DIRECTORY);
		_ = Directory.CreateDirectory(directory);
		return directory;
	}

	public static string GetMetadataDirectory()
	{
		string directory = Path.Combine(GetApplicationDataDirectory(), METADATA_DIRECTORY);
		_ = Directory.CreateDirectory(directory);
		return directory;
	}

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
}