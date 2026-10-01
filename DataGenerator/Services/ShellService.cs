using System.Diagnostics;
using System.IO;

namespace DataGenerator.Services;

public sealed class ShellService : IShellService
{
	public void OpenFolder(string folderPath, string? fileToSelect = null)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(folderPath);

		if (!string.IsNullOrWhiteSpace(fileToSelect) && File.Exists(fileToSelect))
		{
			using Process? explorerWithSelection = Process.Start(new ProcessStartInfo
			{
				FileName        = "explorer.exe",
				Arguments       = $"/select,\"{Path.GetFullPath(fileToSelect)}\"",
				UseShellExecute = true
			});

			return;
		}

		if (!Directory.Exists(folderPath))
		{
			throw new DirectoryNotFoundException($"The folder '{folderPath}' does not exist yet.");
		}

		using Process? explorer = Process.Start(new ProcessStartInfo
		{
			FileName        = Path.GetFullPath(folderPath),
			UseShellExecute = true
		});
	}
}