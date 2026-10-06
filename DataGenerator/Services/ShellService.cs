using System.Diagnostics;
using System.IO;
using DataGenerator.Interfaces;

namespace DataGenerator.Services;

public sealed class ShellService : IShellService
{
	#region CONSTRUCTORS
	#region PUBLIC
	/// <summary>
	///	Creates a new <see cref="ShellService"/>.
	/// </summary>
	public ShellService()
	{
	}
	#endregion PUBLIC
	#endregion CONSTRUCTORS

	#region METHODS
	#region PUBLIC
	/// <summary>
	///	Opens a folder in Explorer, optionally selecting an existing file inside it.
	/// </summary>
	/// <param name="folderPath">
	///	The folder to open when no selectable file is supplied.
	/// </param>
	/// <param name="fileToSelect">
	///	An optional file to select in Explorer when it exists.
	/// </param>
	/// <exception cref="ArgumentException">
	///	Thrown when <paramref name="folderPath"/> is <see langword="null"/>, empty or whitespace.
	/// </exception>
	/// <exception cref="DirectoryNotFoundException">
	///	Thrown when <paramref name="folderPath"/> does not exist and no selectable file was supplied.
	/// </exception>
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
	#endregion PUBLIC
	#endregion METHODS
}