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

	/// <summary>
	///	Opens a web or e-mail link with the user's default application.
	/// </summary>
	/// <param name="uri">
	///	An absolute <c>http</c>, <c>https</c> or <c>mailto</c> link.
	/// </param>
	/// <exception cref="ArgumentNullException">
	///	Thrown when <paramref name="uri"/> is <see langword="null"/>.
	/// </exception>
	/// <exception cref="ArgumentException">
	///	Thrown when <paramref name="uri"/> is relative or uses another scheme, such as <c>file</c>.
	/// </exception>
	void OpenLink(Uri uri);
}