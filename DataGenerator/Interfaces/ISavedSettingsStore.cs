using System.IO;
using DataGenerator.Models;

namespace DataGenerator.Interfaces;

/// <summary>
///	Reads and writes saved column settings: the user's own library and files exported to share with others.
/// </summary>
public interface ISavedSettingsStore
{
	/// <summary>
	///	The file that holds the user's library.
	/// </summary>
	string LibraryFilePath { get; }

	/// <summary>
	///	Reads the library; returns an empty list when nothing has been saved yet.
	///	Throws <see cref="InvalidDataException"/> when the file cannot be read as saved settings.
	/// </summary>
	/// <returns>
	///	The saved column settings, or an empty list when the library file does not exist.
	/// </returns>
	/// <exception cref="InvalidDataException">
	///	Thrown when the library file is not a saved-column-settings file or uses a newer unsupported format.
	/// </exception>
	IReadOnlyList<SavedColumnSetting> LoadLibrary();

	/// <summary>
	///	Replaces the user's saved column-settings library.
	/// </summary>
	/// <param name="settings">
	///	The settings to save.
	/// </param>
	/// <exception cref="ArgumentNullException">
	///	Thrown when <paramref name="settings"/> is <see langword="null"/>.
	/// </exception>
	void SaveLibrary(IReadOnlyList<SavedColumnSetting> settings);

	/// <summary>
	///	Renames an unreadable library file so it is kept for reference instead of being overwritten, and returns its new path.
	/// </summary>
	/// <returns>
	///	The new path of the set-aside file, or <see langword="null"/> when there was no file to move.
	/// </returns>
	string? SetAsideLibraryFile();

	/// <summary>
	///	Throws <see cref="InvalidDataException"/> when the file cannot be read as saved settings.
	/// </summary>
	/// <param name="filePath">
	///	The file to import.
	/// </param>
	/// <returns>
	///	The settings read from <paramref name="filePath"/>.
	/// </returns>
	/// <exception cref="ArgumentException">
	///	Thrown when <paramref name="filePath"/> is <see langword="null"/>, empty or whitespace.
	/// </exception>
	/// <exception cref="InvalidDataException">
	///	Thrown when the file is not a saved-column-settings file or uses a newer unsupported format.
	/// </exception>
	IReadOnlyList<SavedColumnSetting> Import(string filePath);

	/// <summary>
	///	Writes saved column settings to a shareable file.
	/// </summary>
	/// <param name="filePath">
	///	The file to create or replace.
	/// </param>
	/// <param name="settings">
	///	The settings to export.
	/// </param>
	/// <exception cref="ArgumentException">
	///	Thrown when <paramref name="filePath"/> is <see langword="null"/>, empty or whitespace.
	/// </exception>
	/// <exception cref="ArgumentNullException">
	///	Thrown when <paramref name="settings"/> is <see langword="null"/>.
	/// </exception>
	void Export(string filePath, IReadOnlyList<SavedColumnSetting> settings);
}