using Microsoft.Win32;
using DataGenerator.Interfaces;

namespace DataGenerator.Services;

public sealed class FileDialogService : IFileDialogService
{
	private const string SETTINGS_EXTENSION                  = ".json";
	private const string SETTINGS_EXPORT_FILE_NAME           = "DataGenerator column settings.json";
	private const string SETTINGS_FILTER                     = "Saved column settings (*.json)|*.json|All files (*.*)|*.*";
	private const string SET_CONFIGURATIONS_EXPORT_FILE_NAME = "DataGenerator set configurations.json";
	private const string SET_CONFIGURATIONS_FILTER           = "Set configurations (*.json)|*.json|All files (*.*)|*.*";

	/// <summary>
	///	Shows the save dialog used to choose a SQL script output file.
	/// </summary>
	/// <returns>
	///	The selected SQL file path, or <see langword="null"/> when the user cancels.
	/// </returns>
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

	/// <summary>
	///	Shows the open dialog used to choose a saved column settings file to import.
	/// </summary>
	/// <returns>
	///	The selected settings file path, or <see langword="null"/> when the user cancels.
	/// </returns>
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

	/// <summary>
	///	Shows the save dialog used to choose where saved column settings are exported.
	/// </summary>
	/// <returns>
	///	The selected export file path, or <see langword="null"/> when the user cancels.
	/// </returns>
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

	/// <summary>
	///	Shows the open dialog used to choose a set configurations file to import.
	/// </summary>
	/// <returns>
	///	The selected set configurations file path, or <see langword="null"/> when the user cancels.
	/// </returns>
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

	/// <summary>
	///	Shows the save dialog used to choose where set configurations are exported.
	/// </summary>
	/// <returns>
	///	The selected export file path, or <see langword="null"/> when the user cancels.
	/// </returns>
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