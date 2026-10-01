using Microsoft.Win32;

namespace DataGenerator.Services;

public sealed class FileDialogService : IFileDialogService
{
	public string? SelectSqlOutputFile()
	{
		SaveFileDialog dialog = new()
		{
			AddExtension     = true,
			CheckPathExists  = true,
			DefaultExt       = ".sql",
			FileName         = $"generated-data-{DateTime.Now:yyyyMMdd-HHmmss}.sql",
			Filter           = "SQL script (*.sql)|*.sql",
			InitialDirectory = SecurePathService.GetGeneratedDataDirectory(),
			OverwritePrompt  = true,
			Title            = "Select SQL output file"
		};

		bool? result = dialog.ShowDialog();

		return result == true ? dialog.FileName : null;
	}
}