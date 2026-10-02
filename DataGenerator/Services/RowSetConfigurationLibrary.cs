using System.IO;
using DataGenerator.Models;

namespace DataGenerator.Services;

/// <summary>
/// The user's saved set configurations. Every change is written to the <see cref="IRowSetConfigurationStore"/> straight
/// away; when writing fails the change is undone so the list always matches the file.
/// </summary>
public sealed class RowSetConfigurationLibrary
{
	public const int MAXIMUM_NAME_LENGTH = 100;

	private readonly IRowSetConfigurationStore      _store;
	private readonly List<SavedRowSetConfiguration> _configurations;

	public RowSetConfigurationLibrary(IRowSetConfigurationStore store)
	{
		_store          = store ?? throw new ArgumentNullException(nameof(store));
		_configurations = [];
	}

	/// <summary>
	/// Raised after configurations are added, replaced, removed or loaded.
	/// </summary>
	public event EventHandler? Changed;

	/// <summary>
	/// The saved configurations, sorted by name. Change them only through the methods of this class.
	/// </summary>
	public IReadOnlyList<SavedRowSetConfiguration> Configurations => _configurations;

	public string FilePath => _store.LibraryFilePath;

	/// <summary>
	/// Reads the saved configurations. Returns a message for the user when the file could not be read; the unreadable
	/// file is then renamed so that it is not overwritten by the next save.
	/// </summary>
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
	/// Returns the problem with a configuration name, or <see langword="null"/> when it can be used.
	/// </summary>
	public static string? ValidateName(string? name)
	{
		if (string.IsNullOrWhiteSpace(name))
		{
			return "Enter a name, e.g. Shirts for the demo.";
		}

		return name.Trim().Length > MAXIMUM_NAME_LENGTH
			? $"Use at most {MAXIMUM_NAME_LENGTH} characters."
			: null;
	}

	public SavedRowSetConfiguration? Find(string? name)
		=> string.IsNullOrWhiteSpace(name)
			? null
			: _configurations.FirstOrDefault(
				configuration => string.Equals(configuration.Name, name.Trim(), StringComparison.OrdinalIgnoreCase)
			);

	/// <summary>
	/// Adds the configuration, or replaces the saved configuration with the same name.
	/// </summary>
	public void Save(SavedRowSetConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(configuration);
		EnsureValidName(configuration.Name);

		SavedRowSetConfiguration copy = configuration.Clone();

		copy.Name = copy.Name.Trim();
		Update(() => AddCore(copy));
	}

	public void Remove(string name)
	{
		SavedRowSetConfiguration configuration = Find(name)
			?? throw new InvalidOperationException($"There is no set configuration called '{name}'.");

		Update(() => _ = _configurations.Remove(configuration));
	}

	/// <summary>
	/// Reads configurations from a file without adding them, so the caller can decide what to do with names that already exist.
	/// </summary>
	public IReadOnlyList<SavedRowSetConfiguration> ReadImportFile(string filePath) => _store.Import(filePath);

	/// <summary>
	/// Adds imported configurations. Configurations whose names already exist replace the saved ones when
	/// <paramref name="replaceExisting"/> is set, otherwise they are skipped. Returns how many were added or replaced.
	/// </summary>
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

	public void Export(string filePath, IReadOnlyList<SavedRowSetConfiguration> configurations) => _store.Export(filePath, configurations);

	private void AddCore(SavedRowSetConfiguration configuration)
	{
		SavedRowSetConfiguration? existing = Find(configuration.Name);

		if (existing is not null)
		{
			_ = _configurations.Remove(existing);
		}

		_configurations.Add(configuration);
	}

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

	private void Sort()
		=> _configurations.Sort((left, right) => StringComparer.CurrentCultureIgnoreCase.Compare(left.Name, right.Name));

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
			return $"The file '{_store.LibraryFilePath}' will be replaced when a set configuration is saved.";
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