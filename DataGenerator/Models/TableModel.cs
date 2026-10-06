using System.Collections.ObjectModel;
using DataGenerator.Infrastructure;

namespace DataGenerator.Models;

public sealed class TableModel : ObservableObject
{
	private string _databaseName = string.Empty;
	private string _schemaName   = string.Empty;
	private string _name         = string.Empty;

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

	public ObservableCollection<ColumnModel>     Columns     { get; } = [];
	public ObservableCollection<ForeignKeyModel> ForeignKeys { get; } = [];

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

	/// <summary>
	///	Raises change notifications for all derived table-name properties.
	/// </summary>
	private void OnNameChanged()
	{
		OnPropertyChanged(nameof(FullyQualifiedName));
		OnPropertyChanged(nameof(Key));
		OnPropertyChanged(nameof(DisplayName));
	}
}