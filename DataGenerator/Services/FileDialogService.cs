using Microsoft.Win32;

namespace DataGenerator.Services;

public sealed class FileDialogService : IFileDialogService
{
	private const string SETTINGS_EXTENSION                  = ".json";
	private const string SETTINGS_EXPORT_FILE_NAME           = "DataGenerator column settings.json";
	private const string SETTINGS_FILTER                     = "Saved column settings (*.json)|*.json|All files (*.*)|*.*";
	private const string SET_CONFIGURATIONS_EXPORT_FILE_NAME = "DataGenerator set configurations.json";
	private const string SET_CONFIGURATIONS_FILTER           = "Set configurations (*.json)|*.json|All files (*.*)|*.*";

	public string? SelectSqlOutputFile()
	{
		SaveFileDialog dialog = new()
		{
			AddExtension     = true,
			CheckPathExists  = true,
			DefaultExt       = ".sql",
			FileName         = SecurePathService.CreateDefaultSqlFileName(),
			Filter           = "SQL script (*.sql)|*.sql",
			InitialDirectory = SecurePathService.GetGeneratedDataDirectory(),
			OverwritePrompt  = true,
			Title            = "Select SQL output file"
		};

		bool? result = dialog.ShowDialog();

		return result == true ? dialog.FileName : null;
	}

	public string? SelectSettingsFileToImport()
	{
		OpenFileDialog dialog = new()
		{
			CheckFileExists = true,
			DefaultExt      = SETTINGS_EXTENSION,
			Filter          = SETTINGS_FILTER,
			Title           = "Import saved column settings"
		};

		bool? result = dialog.ShowDialog();

		return result == true ? dialog.FileName : null;
	}

	public string? SelectSettingsExportFile()
	{
		SaveFileDialog dialog = new()
		{
			AddExtension    = true,
			CheckPathExists = true,
			DefaultExt      = SETTINGS_EXTENSION,
			FileName        = SETTINGS_EXPORT_FILE_NAME,
			Filter          = SETTINGS_FILTER,
			OverwritePrompt = true,
			Title           = "Export saved column settings"
		};

		bool? result = dialog.ShowDialog();

		return result == true ? dialog.FileName : null;
	}

	public string? SelectSetConfigurationsFileToImport()
	{
		OpenFileDialog dialog = new()
		{
			CheckFileExists = true,
			DefaultExt      = SETTINGS_EXTENSION,
			Filter          = SET_CONFIGURATIONS_FILTER,
			Title           = "Import set configurations"
		};

		bool? result = dialog.ShowDialog();

		return result == true ? dialog.FileName : null;
	}

	public string? SelectSetConfigurationsExportFile()
	{
		SaveFileDialog dialog = new()
		{
			AddExtension    = true,
			CheckPathExists = true,
			DefaultExt      = SETTINGS_EXTENSION,
			FileName        = SET_CONFIGURATIONS_EXPORT_FILE_NAME,
			Filter          = SET_CONFIGURATIONS_FILTER,
			OverwritePrompt = true,
			Title           = "Export set configurations"
		};

		bool? result = dialog.ShowDialog();

		return result == true ? dialog.FileName : null;
	}
}