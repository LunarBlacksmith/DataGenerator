using DataGenerator.Models;

namespace DataGenerator.Interfaces;

/// <summary>
/// Reads and writes the user's preferences.
/// </summary>
public interface IUserPreferencesStore
{
	/// <summary>
	/// Reads the preferences; returns the defaults when nothing has been saved yet or the file cannot be read.
	/// </summary>
	UserPreferences Load();

	void Save(UserPreferences preferences);
}