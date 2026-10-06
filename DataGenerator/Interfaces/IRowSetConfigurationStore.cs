using System.IO;
using DataGenerator.Models;

namespace DataGenerator.Interfaces;

/// <summary>
///	Reads and writes saved set configurations: the user's own list and files exported to share with others.
///	The files differ from saved column settings files, so one kind can never be read as the other.
/// </summary>
public interface IRowSetConfigurationStore
{
	/// <summary>
	///	The file that holds the user's set configurations.
	/// </summary>
	string LibraryFilePath { get; }

	/// <summary>
	///	Reads the user's set configurations; returns an empty list when nothing has been saved yet.
	///	Throws <see cref="InvalidDataException"/> when the file cannot be read as set configurations.
	/// </summary>
	/// <returns>
	///	The saved set configurations, or an empty list when the library file does not exist.
	/// </returns>
	/// <exception cref="InvalidDataException">
	///	Thrown when the library file is not a set-configurations file or uses a newer unsupported format.
	/// </exception>
	IReadOnlyList<SavedRowSetConfiguration> LoadLibrary();

	/// <summary>
	///	Replaces the user's saved set-configuration library.
	/// </summary>
	/// <param name="configurations">
	///	The configurations to save.
	/// </param>
	/// <exception cref="ArgumentNullException">
	///	Thrown when <paramref name="configurations"/> is <see langword="null"/>.
	/// </exception>
	void SaveLibrary(IReadOnlyList<SavedRowSetConfiguration> configurations);

	/// <summary>
	///	Renames an unreadable library file so it is kept for reference instead of being overwritten, and returns its new path.
	/// </summary>
	/// <returns>
	///	The new path of the set-aside file, or <see langword="null"/> when there was no file to move.
	/// </returns>
	string? SetAsideLibraryFile();

	/// <summary>
	///	Throws <see cref="InvalidDataException"/> when the file cannot be read as set configurations.
	/// </summary>
	/// <param name="filePath">
	///	The file to import.
	/// </param>
	/// <returns>
	///	The configurations read from <paramref name="filePath"/>.
	/// </returns>
	/// <exception cref="ArgumentException">
	///	Thrown when <paramref name="filePath"/> is <see langword="null"/>, empty or whitespace.
	/// </exception>
	/// <exception cref="InvalidDataException">
	///	Thrown when the file is not a set-configurations file or uses a newer unsupported format.
	/// </exception>
	IReadOnlyList<SavedRowSetConfiguration> Import(string filePath);

	/// <summary>
	///	Writes set configurations to a shareable file.
	/// </summary>
	/// <param name="filePath">
	///	The file to create or replace.
	/// </param>
	/// <param name="configurations">
	///	The configurations to export.
	/// </param>
	/// <exception cref="ArgumentException">
	///	Thrown when <paramref name="filePath"/> is <see langword="null"/>, empty or whitespace.
	/// </exception>
	/// <exception cref="ArgumentNullException">
	///	Thrown when <paramref name="configurations"/> is <see langword="null"/>.
	/// </exception>
	void Export(string filePath, IReadOnlyList<SavedRowSetConfiguration> configurations);
}