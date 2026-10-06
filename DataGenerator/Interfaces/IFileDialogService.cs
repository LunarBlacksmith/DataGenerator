namespace DataGenerator.Interfaces;

public interface IFileDialogService
{
	string? SelectSqlOutputFile();

	string? SelectSettingsFileToImport();

	string? SelectSettingsExportFile();

	string? SelectSetConfigurationsFileToImport();

	string? SelectSetConfigurationsExportFile();
}