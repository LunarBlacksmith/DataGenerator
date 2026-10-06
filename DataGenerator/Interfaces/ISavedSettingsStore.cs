using System.IO;
using DataGenerator.Models;

namespace DataGenerator.Interfaces;

/// <summary>
/// Reads and writes saved column settings: the user's own library and files exported to share with others.
/// </summary>
public interface ISavedSettingsStore
{
	/// <summary>
	/// The file that holds the user's library.
	/// </summary>
	string LibraryFilePath { get; }

	/// <summary>
	/// Reads the library; returns an empty list when nothing has been saved yet.
	/// Throws <see cref="InvalidDataException"/> when the file cannot be read as saved settings.
	/// </summary>
	IReadOnlyList<SavedColumnSetting> LoadLibrary();

	void SaveLibrary(IReadOnlyList<SavedColumnSetting> settings);

	/// <summary>
	/// Renames an unreadable library file so it is kept for reference instead of being overwritten, and returns its new path.
	/// </summary>
	string? SetAsideLibraryFile();

	/// <summary>
	/// Throws <see cref="InvalidDataException"/> when the file cannot be read as saved settings.
	/// </summary>
	IReadOnlyList<SavedColumnSetting> Import(string filePath);

	void Export(string filePath, IReadOnlyList<SavedColumnSetting> settings);
}