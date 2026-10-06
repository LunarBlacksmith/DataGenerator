using System.IO;
using DataGenerator.Interfaces;
using DataGenerator.Models;

namespace DataGenerator.Services;

/// <summary>
///	The user's saved column settings. Every change is written to the <see cref="ISavedSettingsStore"/> straight away;
///	when writing fails the change is undone so the list always matches the file.
/// </summary>
public sealed class SavedSettingsLibrary
{
	#region FIELDS
	#region PUBLIC
	public const int MAXIMUM_NAME_LENGTH = 100;
	#endregion PUBLIC

	#region PRIVATE
	private readonly ISavedSettingsStore      _store;
	private readonly List<SavedColumnSetting> _settings;
	#endregion PRIVATE
	#endregion FIELDS

	#region PROPERTIES
	#region PUBLIC
	/// <summary>
	///	The saved settings, sorted by name. Change them only through the methods of this class.
	/// </summary>
	public IReadOnlyList<SavedColumnSetting> Settings => _settings;

	public string FilePath => _store.LibraryFilePath;
	#endregion PUBLIC
	#endregion PROPERTIES

	#region EVENTS
	#region PUBLIC
	/// <summary>
	///	Raised after settings are added, changed, removed or loaded.
	/// </summary>
	public event EventHandler? Changed;

	/// <summary>
	///	Raised when the user asks for the automatic settings to be applied to the row sets that already exist.
	///	Handlers report how many columns they updated.
	/// </summary>
	public event EventHandler<ApplyAutomaticSettingsEventArgs>? ApplyAutomaticSettingsRequested;
	#endregion PUBLIC
	#endregion EVENTS

	#region CONSTRUCTORS
	#region PUBLIC
	/// <summary>
	///	Creates the library around the store that loads, saves, imports and exports saved column settings.
	/// </summary>
	/// <param name="store">
	///	The persistent store used for the settings library file.
	/// </param>
	/// <exception cref="ArgumentNullException">
	///	Thrown when <paramref name="store"/> is <see langword="null"/>.
	/// </exception>
	public SavedSettingsLibrary(ISavedSettingsStore store)
	{
		_store    = store ?? throw new ArgumentNullException(nameof(store));
		_settings = [];
	}
	#endregion PUBLIC
	#endregion CONSTRUCTORS

	#region METHODS
	#region PUBLIC
	/// <summary>
	///	Returns the problem with a setting name, or <see langword="null"/> when it can be used.
	/// </summary>
	/// <param name="name">
	///	The proposed setting name.
	/// </param>
	/// <returns>
	///	The validation message when the name is empty or too long; otherwise <see langword="null"/>.
	/// </returns>
	public static string? ValidateName(string? name)
		=>
			string.IsNullOrWhiteSpace(name)
				? "Enter a name, e.g. Order numbers."
				: name.Trim().Length > MAXIMUM_NAME_LENGTH
					? $"Use at most {MAXIMUM_NAME_LENGTH} characters."
					: null;

	/// <summary>
	///	Reads the saved settings. Returns a message for the user when the file could not be read; the unreadable file is
	///	then renamed so that it is not overwritten by the next save.
	/// </summary>
	/// <returns>
	///	<see langword="null"/> when the library was loaded; otherwise a message explaining why it started empty.
	/// </returns>
	public string? Load()
	{
		IReadOnlyList<SavedColumnSetting> settings;

		try
		{
			settings = _store.LoadLibrary();
		}
		catch (Exception exception) when (IsFileProblem(exception))
		{
			_settings.Clear();
			OnChanged();
			return $"The saved column settings could not be read, so the list starts empty.{Environment.NewLine}{Environment.NewLine}"
				+ $"{exception.Message}{Environment.NewLine}{Environment.NewLine}{DescribeSetAsideFile()}";
		}

		_settings.Clear();

		foreach (SavedColumnSetting setting in settings)
		{
			_ = RemoveByName(setting.Name);
			_settings.Add(setting);
		}

		SortAndClearDuplicateAutomaticTargets();
		OnChanged();
		return null;
	}

	/// <summary>
	///	Finds a saved column setting by name, ignoring case and surrounding whitespace.
	/// </summary>
	/// <param name="name">
	///	The setting name to find, or <see langword="null"/> or whitespace to find nothing.
	/// </param>
	/// <returns>
	///	The matching setting when one exists; otherwise <see langword="null"/>.
	/// </returns>
	public SavedColumnSetting? Find(string? name)
		=>
			string.IsNullOrWhiteSpace(name)
				? null
				: _settings
					.FirstOrDefault(
						setting => string.Equals(
							setting.Name,
							name.Trim(),
							StringComparison.OrdinalIgnoreCase
						)
					);

	/// <summary>
	///	Finds the automatic setting for a column; a setting for the exact table and column wins over one for any table.
	/// </summary>
	/// <param name="tableName">
	///	The table name of the column being configured.
	/// </param>
	/// <param name="columnName">
	///	The column name being configured.
	/// </param>
	/// <returns>
	///	The best automatic setting for the target column, or <see langword="null"/> when none applies.
	/// </returns>
	public SavedColumnSetting? FindAutomatic(string tableName, string columnName)
	{
		SavedColumnSetting? anyTableMatch = null;

		foreach (SavedColumnSetting setting in _settings)
		{
			if (!setting.ApplyAutomatically || !setting.Matches(tableName, columnName))
			{
				continue;
			}

			if (!setting.AppliesToAnyTable)
			{
				return setting;
			}

			anyTableMatch ??= setting;
		}

		return anyTableMatch;
	}

	/// <summary>
	///	Finds another automatic setting that would be replaced as the automatic setting for the same target.
	/// </summary>
	/// <param name="candidate">
	///	The setting whose automatic target is being checked.
	/// </param>
	/// <returns>
	///	The conflicting automatic setting when one exists; otherwise <see langword="null"/>.
	/// </returns>
	/// <exception cref="ArgumentNullException">
	///	Thrown when <paramref name="candidate"/> is <see langword="null"/>.
	/// </exception>
	public SavedColumnSetting? FindAutomaticConflict(SavedColumnSetting candidate)
	{
		ArgumentNullException.ThrowIfNull(candidate);

		return
			candidate.ApplyAutomatically
				? _settings
					.FirstOrDefault(
						setting =>
							setting.ApplyAutomatically
							&& setting.HasSameTarget(candidate)
							&& !string.Equals(setting.Name, candidate.Name.Trim(), StringComparison.OrdinalIgnoreCase)
					)
				: null;
	}

	/// <summary>
	///	Finds the first saved setting with the same mode and values, preferring a supplied name when it matches.
	/// </summary>
	/// <param name="values">
	///	The setting values to compare with the saved settings.
	/// </param>
	/// <param name="preferredName">
	///	The preferred setting name to check first, or <see langword="null"/> to use the first equivalent setting.
	/// </param>
	/// <returns>
	///	The preferred or first equivalent setting when one exists; otherwise <see langword="null"/>.
	/// </returns>
	/// <exception cref="ArgumentNullException">
	///	Thrown when <paramref name="values"/> is <see langword="null"/>.
	/// </exception>
	public SavedColumnSetting? FindEquivalent(SavedColumnSetting values, string? preferredName)
	{
		ArgumentNullException.ThrowIfNull(values);

		SavedColumnSetting? preferred = Find(preferredName);

		return
			preferred is not null && preferred.HasSameValues(values)
				? preferred
				: _settings
					.FirstOrDefault(setting => setting.HasSameValues(values));
	}

	/// <summary>
	///	Adds the setting, or replaces the saved setting with the same name. An automatic setting takes over its target from
	///	any other automatic setting.
	/// </summary>
	/// <param name="setting">
	///	The setting to save; it is cloned before it is stored.
	/// </param>
	/// <exception cref="ArgumentNullException">
	///	Thrown when <paramref name="setting"/> is <see langword="null"/>.
	/// </exception>
	/// <exception cref="ArgumentException">
	///	Thrown when the setting name is empty or too long.
	/// </exception>
	public void Save(SavedColumnSetting setting)
	{
		ArgumentNullException.ThrowIfNull(setting);
		EnsureValidName(setting.Name);

		SavedColumnSetting copy = setting.Clone();

		copy.Name = copy.Name.Trim();
		Update(() => AddCore(copy));
	}

	/// <summary>
	///	Removes the saved setting with the supplied name and persists the change.
	/// </summary>
	/// <param name="name">
	///	The name of the setting to remove.
	/// </param>
	/// <exception cref="InvalidOperationException">
	///	Thrown when no setting with <paramref name="name"/> exists.
	/// </exception>
	public void Remove(string name)
	{
		SavedColumnSetting setting = FindExisting(name);
		Update(() => _ = _settings.Remove(setting));
	}

	/// <summary>
	///	Renames a saved setting, keeping the existing values and automatic target.
	/// </summary>
	/// <param name="currentName">
	///	The current name of the setting.
	/// </param>
	/// <param name="newName">
	///	The new name to assign.
	/// </param>
	/// <exception cref="ArgumentException">
	///	Thrown when <paramref name="newName"/> is empty or too long.
	/// </exception>
	/// <exception cref="InvalidOperationException">
	///	Thrown when the setting does not exist or another setting already uses <paramref name="newName"/>.
	/// </exception>
	public void Rename(string currentName, string newName)
	{
		EnsureValidName(newName);

		SavedColumnSetting setting = FindExisting(currentName);
		SavedColumnSetting? other  = Find(newName);

		if (other is not null && !ReferenceEquals(other, setting))
		{
			throw new InvalidOperationException($"A saved setting called '{other.Name}' already exists.");
		}

		Update(() => setting.Name = newName.Trim());
	}

	/// <summary>
	///	Turns automatic application on or off for a saved setting and clears any conflicting automatic setting.
	/// </summary>
	/// <param name="name">
	///	The name of the setting to change.
	/// </param>
	/// <param name="applyAutomatically">
	///	Whether the setting should be applied automatically to matching columns.
	/// </param>
	/// <exception cref="InvalidOperationException">
	///	Thrown when the setting does not exist or was not saved from a column.
	/// </exception>
	public void SetApplyAutomatically(string name, bool applyAutomatically)
	{
		SavedColumnSetting setting = FindExisting(name);

		if (applyAutomatically && setting.ColumnName is null)
		{
			throw new InvalidOperationException($"'{setting.Name}' was not saved from a column, so it cannot be applied automatically.");
		}

		Update(
			() =>
			{
				setting.ApplyAutomatically = applyAutomatically;
				ClearAutomaticConflicts(setting);
			}
		);
	}

	/// <summary>
	///	Reads settings from a file without adding them, so the caller can decide what to do with names that already exist.
	/// </summary>
	/// <param name="filePath">
	///	The import file to read.
	/// </param>
	/// <returns>
	///	The settings read from the import file.
	/// </returns>
	public IReadOnlyList<SavedColumnSetting> ReadImportFile(string filePath) => _store.Import(filePath);

	/// <summary>
	///	Adds imported settings. Settings whose names already exist replace the saved ones when
	///	<paramref name="replaceExisting"/> is set, otherwise they are skipped.
	/// </summary>
	/// <param name="settings">
	///	The settings read from the imported file.
	/// </param>
	/// <param name="replaceExisting">
	///	Whether settings with an existing name replace the saved ones.
	/// </param>
	/// <returns>
	///	How many settings were added or replaced.
	/// </returns>
	/// <exception cref="ArgumentNullException">
	///	Thrown when <paramref name="settings"/> is <see langword="null"/>.
	/// </exception>
	public int Import(IReadOnlyList<SavedColumnSetting> settings, bool replaceExisting)
	{
		ArgumentNullException.ThrowIfNull(settings);

		int importedCount = 0;

		Update(
			() =>
			{
				foreach (SavedColumnSetting setting in settings)
				{
					if (ValidateName(setting.Name) is not null || (!replaceExisting && Find(setting.Name) is not null))
					{
						continue;
					}

					SavedColumnSetting copy = setting.Clone();

					copy.Name = copy.Name.Trim();
					AddCore(copy);
					++importedCount;
				}
			}
		);

		return importedCount;
	}

	/// <summary>
	///	Writes the selected saved settings to an export file.
	/// </summary>
	/// <param name="filePath">
	///	The export file to write.
	/// </param>
	/// <param name="settings">
	///	The settings to export.
	/// </param>
	public void Export(string filePath, IReadOnlyList<SavedColumnSetting> settings) => _store.Export(filePath, settings);

	/// <summary>
	///	Asks the open tables to use the automatic settings now.
	/// </summary>
	/// <returns>
	///	The number of columns that were updated by event handlers.
	/// </returns>
	public int ApplyAutomaticSettingsToExistingRowSets()
	{
		ApplyAutomaticSettingsEventArgs arguments = new();
		ApplyAutomaticSettingsRequested?.Invoke(this, arguments);
		return arguments.UpdatedColumnCount;
	}
	#endregion PUBLIC

	#region PRIVATE
	/// <summary>
	///	Throws when a saved setting name cannot be saved.
	/// </summary>
	/// <param name="name">
	///	The name to validate.
	/// </param>
	/// <exception cref="ArgumentException">
	///	Thrown when <paramref name="name"/> is empty or too long.
	/// </exception>
	private static void EnsureValidName(string? name)
	{
		string? error = ValidateName(name);

		if (error is not null)
		{
			throw new ArgumentException(error, nameof(name));
		}
	}

	/// <summary>
	///	Checks whether an exception represents a recoverable library-file problem.
	/// </summary>
	/// <param name="exception">
	///	The exception raised while reading, writing or moving the library file.
	/// </param>
	/// <returns>
	///	<see langword="true"/> when the exception is an expected file problem; otherwise <see langword="false"/>.
	/// </returns>
	private static bool IsFileProblem(Exception exception)
		=> exception is IOException or UnauthorizedAccessException or InvalidDataException;

	/// <summary>
	///	Adds a setting to the in-memory list, replacing any existing setting with the same name.
	/// </summary>
	/// <param name="setting">
	///	The setting to add.
	/// </param>
	private void AddCore(SavedColumnSetting setting)
	{
		_ = RemoveByName(setting.Name);
		_settings.Add(setting);
		ClearAutomaticConflicts(setting);
	}

	/// <summary>
	///	Applies a change, saves the library and restores the previous in-memory list if saving fails.
	/// </summary>
	/// <param name="change">
	///	The in-memory change to apply before saving.
	/// </param>
	private void Update(Action change)
	{
		List<SavedColumnSetting> backup = [.. _settings.Select(setting => setting.Clone())];

		try
		{
			change();
			SortAndClearDuplicateAutomaticTargets();
			_store.SaveLibrary(_settings);
		}
		catch
		{
			_settings.Clear();
			_settings.AddRange(backup);
			throw;
		}
		finally
		{
			OnChanged();
		}
	}

	/// <summary>
	///	Removes a setting from the in-memory list without saving the library.
	/// </summary>
	/// <param name="name">
	///	The setting name to remove.
	/// </param>
	/// <returns>
	///	<see langword="true"/> when a matching setting was removed; otherwise <see langword="false"/>.
	/// </returns>
	private bool RemoveByName(string name)
	{
		SavedColumnSetting? existing = Find(name);
		return existing is not null && _settings.Remove(existing);
	}

	/// <summary>
	///	Clears automatic application from settings that target the same table and column as the winning setting.
	/// </summary>
	/// <param name="winner">
	///	The setting that keeps automatic application for its target.
	/// </param>
	private void ClearAutomaticConflicts(SavedColumnSetting winner)
	{
		if (!winner.ApplyAutomatically)
		{
			return;
		}

		foreach (SavedColumnSetting setting in _settings)
		{
			if (!ReferenceEquals(setting, winner) && setting.ApplyAutomatically && setting.HasSameTarget(winner))
			{
				setting.ApplyAutomatically = false;
			}
		}
	}

	// Files edited by hand may give one target several automatic settings; the first one by name keeps it.
	/// <summary>
	///	Sorts settings by name and leaves only the first automatic setting for each target.
	/// </summary>
	private void SortAndClearDuplicateAutomaticTargets()
	{
		_settings.Sort((left, right) => StringComparer.CurrentCultureIgnoreCase.Compare(left.Name, right.Name));

		List<SavedColumnSetting> automaticSettings = [];

		foreach (SavedColumnSetting setting in _settings.Where(setting => setting.ApplyAutomatically))
		{
			if (automaticSettings.Any(winner => winner.HasSameTarget(setting)))
			{
				setting.ApplyAutomatically = false;
			}
			else
			{
				automaticSettings.Add(setting);
			}
		}
	}

	/// <summary>
	///	Finds a saved setting and throws when it does not exist.
	/// </summary>
	/// <param name="name">
	///	The setting name to find.
	/// </param>
	/// <returns>
	///	The matching saved setting.
	/// </returns>
	/// <exception cref="InvalidOperationException">
	///	Thrown when no saved setting with <paramref name="name"/> exists.
	/// </exception>
	private SavedColumnSetting FindExisting(string name)
		=> Find(name) ?? throw new InvalidOperationException($"There is no saved setting called '{name}'.");

	/// <summary>
	///	Moves or describes the unreadable library file so a later save can create a clean file.
	/// </summary>
	/// <returns>
	///	A message describing where the unreadable file was kept, or what will happen on the next save.
	/// </returns>
	private string DescribeSetAsideFile()
	{
		try
		{
			string? setAsidePath = _store.SetAsideLibraryFile();

			return
				setAsidePath is null
					? string.Empty
					: $"The file was kept as '{setAsidePath}'.";
		}
		catch (Exception exception) when (IsFileProblem(exception))
		{
			return $"The file '{_store.LibraryFilePath}' will be replaced when a setting is saved.";
		}
	}

	/// <summary>
	///	Raises the change notification after the in-memory list may have changed.
	/// </summary>
	private void OnChanged() => Changed?.Invoke(this, EventArgs.Empty);
	#endregion PRIVATE
	#endregion METHODS
}

/// <summary>
///	Collects the number of columns updated by <see cref="SavedSettingsLibrary.ApplyAutomaticSettingsToExistingRowSets"/>.
/// </summary>
public sealed class ApplyAutomaticSettingsEventArgs : EventArgs
{
	#region PROPERTIES
	#region PUBLIC
	public int UpdatedColumnCount { get; set; }
	#endregion PUBLIC
	#endregion PROPERTIES

	#region CONSTRUCTORS
	#region PUBLIC
	/// <summary>
	///	Creates a new <see cref="ApplyAutomaticSettingsEventArgs"/> and sets the default values of its fields and properties.
	/// </summary>
	public ApplyAutomaticSettingsEventArgs()
	{
		UpdatedColumnCount = 0;
	}
	#endregion PUBLIC
	#endregion CONSTRUCTORS
}