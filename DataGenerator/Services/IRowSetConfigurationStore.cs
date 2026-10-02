using System.IO;
using DataGenerator.Models;

namespace DataGenerator.Services;

/// <summary>
/// Reads and writes saved set configurations: the user's own list and files exported to share with others.
/// The files differ from saved column settings files, so one kind can never be read as the other.
/// </summary>
public interface IRowSetConfigurationStore
{
	/// <summary>
	/// The file that holds the user's set configurations.
	/// </summary>
	string LibraryFilePath { get; }

	/// <summary>
	/// Reads the user's set configurations; returns an empty list when nothing has been saved yet.
	/// Throws <see cref="InvalidDataException"/> when the file cannot be read as set configurations.
	/// </summary>
	IReadOnlyList<SavedRowSetConfiguration> LoadLibrary();

	void SaveLibrary(IReadOnlyList<SavedRowSetConfiguration> configurations);

	/// <summary>
	/// Renames an unreadable library file so it is kept for reference instead of being overwritten, and returns its new path.
	/// </summary>
	string? SetAsideLibraryFile();

	/// <summary>
	/// Throws <see cref="InvalidDataException"/> when the file cannot be read as set configurations.
	/// </summary>
	IReadOnlyList<SavedRowSetConfiguration> Import(string filePath);

	void Export(string filePath, IReadOnlyList<SavedRowSetConfiguration> configurations);
}