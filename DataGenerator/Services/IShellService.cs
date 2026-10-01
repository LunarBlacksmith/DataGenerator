namespace DataGenerator.Services;

public interface IShellService
{
	/// <summary>
	/// Opens <paramref name="folderPath"/> in File Explorer, selecting <paramref name="fileToSelect"/> when it exists.
	/// </summary>
	void OpenFolder(string folderPath, string? fileToSelect = null);
}