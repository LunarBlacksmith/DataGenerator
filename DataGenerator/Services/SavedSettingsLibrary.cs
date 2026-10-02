using System.IO;
using DataGenerator.Models;

namespace DataGenerator.Services;

/// <summary>
/// The user's saved column settings. Every change is written to the <see cref="ISavedSettingsStore"/> straight away;
/// when writing fails the change is undone so the list always matches the file.
/// </summary>
public sealed class SavedSettingsLibrary
{
	public const int MAXIMUM_NAME_LENGTH = 100;

	private readonly ISavedSettingsStore      _store;
	private readonly List<SavedColumnSetting> _settings;

	public SavedSettingsLibrary(ISavedSettingsStore store)
	{
		_store    = store ?? throw new ArgumentNullException(nameof(store));
		_settings = [];
	}

	/// <summary>
	/// Raised after settings are added, changed, removed or loaded.
	/// </summary>
	public event EventHandler? Changed;

	/// <summary>
	/// Raised when the user asks for the automatic settings to be applied to the row sets that already exist.
	/// Handlers report how many columns they updated.
	/// </summary>
	public event EventHandler<ApplyAutomaticSettingsEventArgs>? ApplyAutomaticSettingsRequested;

	/// <summary>
	/// The saved settings, sorted by name. Change them only through the methods of this class.
	/// </summary>
	public IReadOnlyList<SavedColumnSetting> Settings => _settings;

	public string FilePath => _store.LibraryFilePath;

	/// <summary>
	/// Reads the saved settings. Returns a message for the user when the file could not be read; the unreadable file is
	/// then renamed so that it is not overwritten by the next save.
	/// </summary>
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
	/// Returns the problem with a setting name, or <see langword="null"/> when it can be used.
	/// </summary>
	public static string? ValidateName(string? name)
	{
		if (string.IsNullOrWhiteSpace(name))
		{
			return "Enter a name, e.g. Order numbers.";
		}

		return name.Trim().Length > MAXIMUM_NAME_LENGTH
			? $"Use at most {MAXIMUM_NAME_LENGTH} characters."
			: null;
	}

	public SavedColumnSetting? Find(string? name)
		=> string.IsNullOrWhiteSpace(name)
			? null
			: _settings.FirstOrDefault(setting => string.Equals(setting.Name, name.Trim(), StringComparison.OrdinalIgnoreCase));

	/// <summary>
	/// The automatic setting for a column: one saved for exactly this table and column wins over one for the column
	/// name in any table.
	/// </summary>
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
	/// Another automatic setting that would be replaced as the automatic setting of the same target, if any.
	/// </summary>
	public SavedColumnSetting? FindAutomaticConflict(SavedColumnSetting candidate)
	{
		ArgumentNullException.ThrowIfNull(candidate);

		return candidate.ApplyAutomatically
			? _settings.FirstOrDefault(
				setting =>
					setting.ApplyAutomatically
					&& setting.HasSameTarget(candidate)
					&& !string.Equals(setting.Name, candidate.Name.Trim(), StringComparison.OrdinalIgnoreCase)
			)
			: null;
	}

	/// <summary>
	/// The first saved setting with the same mode and values, preferring <paramref name="preferredName"/>.
	/// </summary>
	public SavedColumnSetting? FindEquivalent(SavedColumnSetting values, string? preferredName)
	{
		ArgumentNullException.ThrowIfNull(values);

		SavedColumnSetting? preferred = Find(preferredName);

		return preferred is not null && preferred.HasSameValues(values)
			? preferred
			: _settings.FirstOrDefault(setting => setting.HasSameValues(values));
	}

	/// <summary>
	/// Adds the setting, or replaces the saved setting with the same name. An automatic setting takes over its target from
	/// any other automatic setting.
	/// </summary>
	public void Save(SavedColumnSetting setting)
	{
		ArgumentNullException.ThrowIfNull(setting);
		EnsureValidName(setting.Name);

		SavedColumnSetting copy = setting.Clone();

		copy.Name = copy.Name.Trim();
		Update(() => AddCore(copy));
	}

	public void Remove(string name)
	{
		SavedColumnSetting setting = FindExisting(name);
		Update(() => _ = _settings.Remove(setting));
	}

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
	/// Reads settings from a file without adding them, so the caller can decide what to do with names that already exist.
	/// </summary>
	public IReadOnlyList<SavedColumnSetting> ReadImportFile(string filePath) => _store.Import(filePath);

	/// <summary>
	/// Adds imported settings. Settings whose names already exist replace the saved ones when
	/// <paramref name="replaceExisting"/> is set, otherwise they are skipped. Returns how many settings were added or replaced.
	/// </summary>
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

	public void Export(string filePath, IReadOnlyList<SavedColumnSetting> settings) => _store.Export(filePath, settings);

	/// <summary>
	/// Asks the open tables to use the automatic settings now. Returns the number of columns that were updated.
	/// </summary>
	public int ApplyAutomaticSettingsToExistingRowSets()
	{
		ApplyAutomaticSettingsEventArgs arguments = new ApplyAutomaticSettingsEventArgs();
		ApplyAutomaticSettingsRequested?.Invoke(this, arguments);
		return arguments.UpdatedColumnCount;
	}

	private void AddCore(SavedColumnSetting setting)
	{
		_ = RemoveByName(setting.Name);
		_settings.Add(setting);
		ClearAutomaticConflicts(setting);
	}

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

	private bool RemoveByName(string name)
	{
		SavedColumnSetting? existing = Find(name);
		return existing is not null && _settings.Remove(existing);
	}

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

	private SavedColumnSetting FindExisting(string name)
		=> Find(name) ?? throw new InvalidOperationException($"There is no saved setting called '{name}'.");

	private string DescribeSetAsideFile()
	{
		try
		{
			string? setAsidePath = _store.SetAsideLibraryFile();

			return setAsidePath is null
				? string.Empty
				: $"The file was kept as '{setAsidePath}'.";
		}
		catch (Exception exception) when (IsFileProblem(exception))
		{
			return $"The file '{_store.LibraryFilePath}' will be replaced when a setting is saved.";
		}
	}

	private void OnChanged() => Changed?.Invoke(this, EventArgs.Empty);

	private static void EnsureValidName(string? name)
	{
		string? error = ValidateName(name);

		if (error is not null)
		{
			throw new ArgumentException(error, nameof(name));
		}
	}

	private static bool IsFileProblem(Exception exception)
		=> exception is IOException or UnauthorizedAccessException or InvalidDataException;
}

/// <summary>
/// Collects the number of columns updated by <see cref="SavedSettingsLibrary.ApplyAutomaticSettingsToExistingRowSets"/>.
/// </summary>
public sealed class ApplyAutomaticSettingsEventArgs : EventArgs
{
	public int UpdatedColumnCount { get; set; }
}