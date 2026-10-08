using System.IO;
using DataGenerator.Interfaces;
using DataGenerator.Models;

namespace DataGenerator.Services;

/// <summary>
///	Stores set configurations as indented JSON with a "setConfigurations" list, so the files can be reviewed and shared
///	and are never mistaken for saved column settings files (which have a "settings" list).
/// </summary>
public sealed class JsonRowSetConfigurationStore : IRowSetConfigurationStore
{
	#region FIELDS
	private const int    FORMAT_VERSION   = 2;
	private const string FILE_DESCRIPTION = "set configurations file";
	#endregion FIELDS

	#region PROPERTIES
	public string LibraryFilePath { get; }
	#endregion PROPERTIES

	#region CONSTRUCTOR
	/// <summary>
	///	Creates a store for the set configuration library file.
	/// </summary>
	/// <param name="libraryFilePath">
	///	The JSON file path used for the saved set configuration library.
	/// </param>
	/// <exception cref="ArgumentNullException">
	///	Thrown when <paramref name="libraryFilePath"/> is <see langword="null"/>.
	/// </exception>
	/// <exception cref="ArgumentException">
	///	Thrown when <paramref name="libraryFilePath"/> is empty or white space.
	/// </exception>
	public JsonRowSetConfigurationStore(string libraryFilePath)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(libraryFilePath);
		LibraryFilePath = libraryFilePath;
	}
	#endregion CONSTRUCTOR

	#region METHODS
	#region PUBLIC
	/// <summary>
	///	Loads the saved set configuration library when it exists.
	/// </summary>
	/// <returns>
	///	The saved set configurations, or an empty list when the library file does not exist.
	/// </returns>
	public IReadOnlyList<SavedRowSetConfiguration> LoadLibrary() => File.Exists(LibraryFilePath) ? Read(LibraryFilePath) : [];

	/// <summary>
	///	Saves the set configuration library file.
	/// </summary>
	/// <param name="configurations">
	///	The configurations to save.
	/// </param>
	public void SaveLibrary(IReadOnlyList<SavedRowSetConfiguration> configurations) => Write(LibraryFilePath, configurations);

	/// <summary>
	///	Moves the library file aside when it exists.
	/// </summary>
	/// <returns>
	///	The new path of the renamed library file, or <see langword="null"/> when it does not exist.
	/// </returns>
	public string? SetAsideLibraryFile() => JsonDocumentFile.SetAside(LibraryFilePath);

	/// <summary>
	///	Reads set configurations from an import file.
	/// </summary>
	/// <param name="filePath">
	///	The import file to read.
	/// </param>
	/// <returns>
	///	The imported set configurations.
	/// </returns>
	/// <exception cref="ArgumentNullException">
	///	Thrown when <paramref name="filePath"/> is <see langword="null"/>.
	/// </exception>
	/// <exception cref="ArgumentException">
	///	Thrown when <paramref name="filePath"/> is empty or white space.
	/// </exception>
	public IReadOnlyList<SavedRowSetConfiguration> Import(string filePath)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
		return Read(filePath);
	}

	/// <summary>
	///	Writes set configurations to an export file.
	/// </summary>
	/// <param name="filePath">
	///	The export file to write.
	/// </param>
	/// <param name="configurations">
	///	The configurations to export.
	/// </param>
	/// <exception cref="ArgumentNullException">
	///	Thrown when <paramref name="filePath"/> is <see langword="null"/>.
	/// </exception>
	/// <exception cref="ArgumentException">
	///	Thrown when <paramref name="filePath"/> is empty or white space.
	/// </exception>
	public void Export(string filePath, IReadOnlyList<SavedRowSetConfiguration> configurations)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
		Write(filePath, configurations);
	}
	#endregion PUBLIC

	#region PRIVATE
	/// <summary>
	///	Reads and normalises a set configurations document.
	/// </summary>
	/// <param name="filePath">
	///	The JSON file to read.
	/// </param>
	/// <returns>
	///	The normalised set configurations from the file.
	/// </returns>
	/// <exception cref="InvalidDataException">
	///	Thrown when the file is not a set configurations document or was saved by a newer format.
	/// </exception>
	private static List<SavedRowSetConfiguration> Read(string filePath)
	{
		RowSetConfigurationsDocument? document = JsonDocumentFile.Read<RowSetConfigurationsDocument>(filePath, FILE_DESCRIPTION);

		if (document?.SetConfigurations is null)
		{
			throw new InvalidDataException(
				$"'{filePath}' is not a {FILE_DESCRIPTION}: it has no \"setConfigurations\" list. "
					+ "Saved column settings files are imported from Saved column settings instead."
			);
		}

		if (document.FormatVersion > FORMAT_VERSION)
		{
			throw new InvalidDataException(
				$"'{filePath}' was saved by a newer version of DataGenerator (format {document.FormatVersion}). Update DataGenerator to read it."
			);
		}

		List<SavedRowSetConfiguration> configurations = new(document.SetConfigurations.Count);

		for (int index = 0; index < document.SetConfigurations.Count; ++index)
		{
			configurations.Add(Normalize(document.SetConfigurations[index], $"Set configuration {index + 1} in '{filePath}'"));
		}

		return configurations;
	}

	/// <summary>
	///	Checks one set configuration read from a file and normalises its columns.
	/// </summary>
	/// <param name="configuration">
	///	The configuration to validate.
	/// </param>
	/// <param name="location">
	///	The user-facing location of the configuration in the file.
	/// </param>
	/// <returns>
	///	The validated and normalised configuration.
	/// </returns>
	/// <exception cref="InvalidDataException">
	///	Thrown when the configuration has no name, an invalid row count or a column without a column name.
	/// </exception>
	private static SavedRowSetConfiguration Normalize(SavedRowSetConfiguration? configuration, string location)
	{
		if (configuration is null || string.IsNullOrWhiteSpace(configuration.Name))
		{
			throw new InvalidDataException($"{location} has no name.");
		}

		if (configuration.RowCount is < 1 or > RowSetPlan.MAXIMUM_ROW_COUNT)
		{
			throw new InvalidDataException($"{location} has an invalid row count. Use a count between 1 and {RowSetPlan.MAXIMUM_ROW_COUNT:N0}.");
		}

		configuration.Name      = configuration.Name.Trim();
		configuration.TableName = configuration.TableName?.Trim() ?? string.Empty;
		configuration.Columns ??= [];

		for (int index = 0; index < configuration.Columns.Count; ++index)
		{
			string             columnLocation = $"Column {index + 1} of '{configuration.Name}' in {location}";
			SavedColumnSetting column         = JsonSavedSettingsStore.Normalize(configuration.Columns[index], columnLocation);

			if (column.ColumnName is null)
			{
				throw new InvalidDataException($"{columnLocation} does not name its column.");
			}

			column.ApplyAutomatically    = false;
			configuration.Columns[index] = column;
		}

		return configuration;
	}

	/// <summary>
	///	Writes set configurations inside the current document format.
	/// </summary>
	/// <param name="filePath">
	///	The JSON file to write.
	/// </param>
	/// <param name="configurations">
	///	The configurations to write.
	/// </param>
	/// <exception cref="ArgumentNullException">
	///	Thrown when <paramref name="configurations"/> is <see langword="null"/>.
	/// </exception>
	private static void Write(string filePath, IReadOnlyList<SavedRowSetConfiguration> configurations)
	{
		ArgumentNullException.ThrowIfNull(configurations);

		JsonDocumentFile.Write(
			filePath,
			new RowSetConfigurationsDocument
			{
				FormatVersion     = FORMAT_VERSION,
				SetConfigurations = [.. configurations]
			}
		);
	}
	#endregion PRIVATE
	#endregion METHODS

	#region TYPES
	private sealed class RowSetConfigurationsDocument
	{
		#region PROPERTIES
		public int                              FormatVersion     { get; set; }
		public List<SavedRowSetConfiguration?>? SetConfigurations { get; set; }
		#endregion PROPERTIES

		/// <summary>
		///	Creates a new <see cref="RowSetConfigurationsDocument"/> and sets the default values of its fields and properties.
		/// </summary>
		public RowSetConfigurationsDocument()
		{
			FormatVersion     = 0;
			SetConfigurations = null;
		}
	}
	#endregion TYPES
}