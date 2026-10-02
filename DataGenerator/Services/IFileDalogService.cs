namespace DataGenerator.Services;

public interface IFileDialogService
{
	string? SelectSqlOutputFile();

	string? SelectSettingsFileToImport();

	string? SelectSettingsExportFile();
}