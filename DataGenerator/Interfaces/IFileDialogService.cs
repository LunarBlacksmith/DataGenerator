namespace DataGenerator.Interfaces;

public interface IFileDialogService
{
	/// <summary>
	///	Asks where the generated SQL script should be saved.
	/// </summary>
	/// <returns>
	///	The selected file path, or <see langword="null"/> when the dialog is cancelled.
	/// </returns>
	string? SelectSqlOutputFile();

	/// <summary>
	///	Asks for a saved-column-settings file to import.
	/// </summary>
	/// <returns>
	///	The selected file path, or <see langword="null"/> when the dialog is cancelled.
	/// </returns>
	string? SelectSettingsFileToImport();

	/// <summary>
	///	Asks where saved column settings should be exported.
	/// </summary>
	/// <returns>
	///	The selected file path, or <see langword="null"/> when the dialog is cancelled.
	/// </returns>
	string? SelectSettingsExportFile();

	/// <summary>
	///	Asks for a saved set-configuration file to import.
	/// </summary>
	/// <returns>
	///	The selected file path, or <see langword="null"/> when the dialog is cancelled.
	/// </returns>
	string? SelectSetConfigurationsFileToImport();

	/// <summary>
	///	Asks where saved set configurations should be exported.
	/// </summary>
	/// <returns>
	///	The selected file path, or <see langword="null"/> when the dialog is cancelled.
	/// </returns>
	string? SelectSetConfigurationsExportFile();
}