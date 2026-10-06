using DataGenerator.Models;

namespace DataGenerator.Interfaces;

/// <summary>
///	Reads and writes the user's preferences.
/// </summary>
public interface IUserPreferencesStore
{
	/// <summary>
	///	Reads the preferences; returns the defaults when nothing has been saved yet or the file cannot be read.
	/// </summary>
	/// <returns>
	///	The saved preferences after normalisation, or default preferences when the file is missing or unreadable.
	/// </returns>
	UserPreferences Load();

	/// <summary>
	///	Saves the user's preferences, replacing any previous preferences file atomically.
	/// </summary>
	/// <param name="preferences">
	///	The preferences to write.
	/// </param>
	/// <exception cref="ArgumentNullException">
	///	Thrown when <paramref name="preferences"/> is <see langword="null"/>.
	/// </exception>
	void Save(UserPreferences preferences);
}