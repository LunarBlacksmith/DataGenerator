using System.Collections.ObjectModel;
using DataGenerator.Infrastructure;

namespace DataGenerator.Models;

public sealed class TableModel : ObservableObject
{
	#region FIELDS
	private string _databaseName;
	private string _schemaName;
	private string _name;
	#endregion FIELDS

	#region PROPERTIES
	public string DatabaseName
	{
		get => _databaseName;
		set
		{
			if (SetProperty(ref _databaseName, value))
			{
				OnNameChanged();
			}
		}
	}

	public string SchemaName
	{
		get => _schemaName;
		set
		{
			if (SetProperty(ref _schemaName, value))
			{
				OnNameChanged();
			}
		}
	}

	public string Name
	{
		get => _name;
		set
		{
			if (SetProperty(ref _name, value))
			{
				OnNameChanged();
			}
		}
	}

	public string FullyQualifiedName
		=> $"[{DatabaseName.Replace("]", "]]")}]."
			+ $"[{SchemaName.Replace("]", "]]")}]."
			+ $"[{Name.Replace("]", "]]")}]";

	/// <summary>
	///	Unique, case-insensitive lookup key in the form database.schema.table.
	/// </summary>
	public string Key => CreateKey(DatabaseName, SchemaName, Name);

	public string DisplayName => $"{SchemaName}.{Name}";

	public ObservableCollection<ColumnModel>     Columns     { get; }
	public ObservableCollection<ForeignKeyModel> ForeignKeys { get; }
	#endregion PROPERTIES

	#region CONSTRUCTOR
	/// <summary>
	///	Creates a new <see cref="TableModel"/> and sets the default values of its fields and properties.
	/// </summary>
	public TableModel()
	{
		_databaseName = string.Empty;
		_schemaName   = string.Empty;
		_name         = string.Empty;
		Columns       = [];
		ForeignKeys   = [];
	}
	#endregion CONSTRUCTOR

	#region METHODS
	#region PUBLIC
	/// <summary>
	///	Builds the case-insensitive lookup key for a table.
	/// </summary>
	/// <param name="databaseName">
	///	The database name part of the key.
	/// </param>
	/// <param name="schemaName">
	///	The schema name part of the key.
	/// </param>
	/// <param name="tableName">
	///	The table name part of the key.
	/// </param>
	/// <returns>
	///	The key in database.schema.table form.
	/// </returns>
	public static string CreateKey(string databaseName, string schemaName, string tableName)
		=> $"{databaseName}.{schemaName}.{tableName}";
	#endregion PUBLIC

	#region PRIVATE
	/// <summary>
	///	Raises change notifications for all derived table-name properties.
	/// </summary>
	private void OnNameChanged()
	{
		OnPropertyChanged(nameof(FullyQualifiedName));
		OnPropertyChanged(nameof(Key));
		OnPropertyChanged(nameof(DisplayName));
	}
	#endregion PRIVATE
	#endregion METHODS
}