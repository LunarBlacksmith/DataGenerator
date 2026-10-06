namespace DataGenerator.Interfaces;

public interface IShellService
{
	/// <summary>
	///	Opens <paramref name="folderPath"/> in File Explorer, selecting <paramref name="fileToSelect"/> when it exists.
	/// </summary>
	/// <param name="folderPath">
	///	The folder to open when no existing file is selected.
	/// </param>
	/// <param name="fileToSelect">
	///	Optional file to select in Explorer; ignored when <see langword="null"/>, empty, whitespace or missing.
	/// </param>
	/// <exception cref="ArgumentException">
	///	Thrown when <paramref name="folderPath"/> is <see langword="null"/>, empty or whitespace.
	/// </exception>
	/// <exception cref="System.IO.DirectoryNotFoundException">
	///	Thrown when <paramref name="folderPath"/> does not exist and no selectable file is supplied.
	/// </exception>
	void OpenFolder(string folderPath, string? fileToSelect = null);
}