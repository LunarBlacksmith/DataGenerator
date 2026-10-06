using System.IO;
using DataGenerator.Interfaces;
using DataGenerator.Models;

namespace DataGenerator.Services;

/// <summary>
///	The user's saved set configurations. Every change is written to the <see cref="IRowSetConfigurationStore"/> straight
///	away; when writing fails the change is undone so the list always matches the file.
/// </summary>
public sealed class RowSetConfigurationLibrary
{
	public const int MAXIMUM_NAME_LENGTH = 100;

	private readonly IRowSetConfigurationStore      _store;
	private readonly List<SavedRowSetConfiguration> _configurations;

	/// <summary>
	///	Creates the library around the store that loads, saves, imports and exports row-set configurations.
	/// </summary>
	/// <param name="store">
	///	The persistent store used for the configuration library file.
	/// </param>
	/// <exception cref="ArgumentNullException">
	///	Thrown when <paramref name="store"/> is <see langword="null"/>.
	/// </exception>
	public RowSetConfigurationLibrary(IRowSetConfigurationStore store)
	{
		_store          = store ?? throw new ArgumentNullException(nameof(store));
		_configurations = [];
	}

	/// <summary>
	///	Raised after configurations are added, replaced, removed or loaded.
	/// </summary>
	public event EventHandler? Changed;

	/// <summary>
	///	The saved configurations, sorted by name. Change them only through the methods of this class.
	/// </summary>
	public IReadOnlyList<SavedRowSetConfiguration> Configurations => _configurations;

	public string FilePath => _store.LibraryFilePath;

	/// <summary>
	///	Reads the saved configurations. Returns a message for the user when the file could not be read; the unreadable
	///	file is then renamed so that it is not overwritten by the next save.
	/// </summary>
	/// <returns>
	///	<see langword="null"/> when the library was loaded; otherwise a message explaining why it started empty.
	/// </returns>
	public string? Load()
	{
		IReadOnlyList<SavedRowSetConfiguration> configurations;

		try
		{
			configurations = _store.LoadLibrary();
		}
		catch (Exception exception) when (IsFileProblem(exception))
		{
			_configurations.Clear();
			OnChanged();
			return $"The saved set configurations could not be read, so the list starts empty.{Environment.NewLine}{Environment.NewLine}"
				+ $"{exception.Message}{Environment.NewLine}{Environment.NewLine}{DescribeSetAsideFile()}";
		}

		_configurations.Clear();

		foreach (SavedRowSetConfiguration configuration in configurations)
		{
			AddCore(configuration);
		}

		Sort();
		OnChanged();
		return null;
	}

	/// <summary>
	///	Returns the problem with a configuration name, or <see langword="null"/> when it can be used.
	/// </summary>
	/// <param name="name">
	///	The proposed configuration name.
	/// </param>
	/// <returns>
	///	The validation message when the name is empty or too long; otherwise <see langword="null"/>.
	/// </returns>
	public static string? ValidateName(string? name)
	{
		if (string.IsNullOrWhiteSpace(name))
		{
			return "Enter a name, e.g. Shirts for the demo.";
		}

		return
			name.Trim().Length > MAXIMUM_NAME_LENGTH
				? $"Use at most {MAXIMUM_NAME_LENGTH} characters."
				: null;
	}

	/// <summary>
	///	Finds a saved row-set configuration by name, ignoring case and surrounding whitespace.
	/// </summary>
	/// <param name="name">
	///	The configuration name to find, or <see langword="null"/> or whitespace to find nothing.
	/// </param>
	/// <returns>
	///	The matching configuration when one exists; otherwise <see langword="null"/>.
	/// </returns>
	public SavedRowSetConfiguration? Find(string? name)
		=>
			string.IsNullOrWhiteSpace(name)
				? null
				: _configurations.FirstOrDefault(
					configuration => string.Equals(configuration.Name, name.Trim(), StringComparison.OrdinalIgnoreCase)
				);

	/// <summary>
	///	Adds the configuration, or replaces the saved configuration with the same name.
	/// </summary>
	/// <param name="configuration">
	///	The configuration to save; it is cloned before it is stored.
	/// </param>
	/// <exception cref="ArgumentNullException">
	///	Thrown when <paramref name="configuration"/> is <see langword="null"/>.
	/// </exception>
	/// <exception cref="ArgumentException">
	///	Thrown when the configuration name is empty or too long.
	/// </exception>
	public void Save(SavedRowSetConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(configuration);
		EnsureValidName(configuration.Name);

		SavedRowSetConfiguration copy = configuration.Clone();

		copy.Name = copy.Name.Trim();
		Update(() => AddCore(copy));
	}

	/// <summary>
	///	Removes the saved configuration with the supplied name and persists the change.
	/// </summary>
	/// <param name="name">
	///	The name of the configuration to remove.
	/// </param>
	/// <exception cref="InvalidOperationException">
	///	Thrown when no configuration with <paramref name="name"/> exists.
	/// </exception>
	public void Remove(string name)
	{
		SavedRowSetConfiguration configuration = Find(name)
			?? throw new InvalidOperationException($"There is no set configuration called '{name}'.");

		Update(() => _ = _configurations.Remove(configuration));
	}

	/// <summary>
	///	Reads configurations from a file without adding them, so the caller can decide what to do with names that already exist.
	/// </summary>
	/// <param name="filePath">
	///	The import file to read.
	/// </param>
	/// <returns>
	///	The configurations read from the import file.
	/// </returns>
	public IReadOnlyList<SavedRowSetConfiguration> ReadImportFile(string filePath) => _store.Import(filePath);

	/// <summary>
	///	Adds imported configurations. Configurations whose names already exist replace the saved ones when
	///	<paramref name="replaceExisting"/> is set, otherwise they are skipped.
	/// </summary>
	/// <param name="configurations">
	///	The configurations read from an import file.
	/// </param>
	/// <param name="replaceExisting">
	///	Whether imported configurations replace saved configurations with the same name.
	/// </param>
	/// <returns>
	///	How many configurations were added or replaced.
	/// </returns>
	/// <exception cref="ArgumentNullException">
	///	Thrown when <paramref name="configurations"/> is <see langword="null"/>.
	/// </exception>
	public int Import(IReadOnlyList<SavedRowSetConfiguration> configurations, bool replaceExisting)
	{
		ArgumentNullException.ThrowIfNull(configurations);

		int importedCount = 0;

		Update(
			() =>
			{
				foreach (SavedRowSetConfiguration configuration in configurations)
				{
					if (ValidateName(configuration.Name) is not null || (!replaceExisting && Find(configuration.Name) is not null))
					{
						continue;
					}

					SavedRowSetConfiguration copy = configuration.Clone();

					copy.Name = copy.Name.Trim();
					AddCore(copy);
					++importedCount;
				}
			}
		);

		return importedCount;
	}

	/// <summary>
	///	Writes the selected configurations to an export file.
	/// </summary>
	/// <param name="filePath">
	///	The export file to write.
	/// </param>
	/// <param name="configurations">
	///	The configurations to export.
	/// </param>
	public void Export(string filePath, IReadOnlyList<SavedRowSetConfiguration> configurations) => _store.Export(filePath, configurations);

	/// <summary>
	///	Adds a configuration to the in-memory list, replacing any existing configuration with the same name.
	/// </summary>
	/// <param name="configuration">
	///	The configuration to add.
	/// </param>
	private void AddCore(SavedRowSetConfiguration configuration)
	{
		SavedRowSetConfiguration? existing = Find(configuration.Name);

		if (existing is not null)
		{
			_ = _configurations.Remove(existing);
		}

		_configurations.Add(configuration);
	}

	/// <summary>
	///	Applies a change, saves the library and restores the previous in-memory list if saving fails.
	/// </summary>
	/// <param name="change">
	///	The in-memory change to apply before saving.
	/// </param>
	private void Update(Action change)
	{
		List<SavedRowSetConfiguration> backup = [.. _configurations.Select(configuration => configuration.Clone())];

		try
		{
			change();
			Sort();
			_store.SaveLibrary(_configurations);
		}
		catch
		{
			_configurations.Clear();
			_configurations.AddRange(backup);
			throw;
		}
		finally
		{
			OnChanged();
		}
	}

	/// <summary>
	///	Sorts the saved configurations by name for display and storage.
	/// </summary>
	private void Sort()
		=> _configurations.Sort((left, right) => StringComparer.CurrentCultureIgnoreCase.Compare(left.Name, right.Name));

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
			return $"The file '{_store.LibraryFilePath}' will be replaced when a set configuration is saved.";
		}
	}

	/// <summary>
	///	Raises the change notification after the in-memory list may have changed.
	/// </summary>
	private void OnChanged() => Changed?.Invoke(this, EventArgs.Empty);

	/// <summary>
	///	Throws when a row-set configuration name cannot be saved.
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
}