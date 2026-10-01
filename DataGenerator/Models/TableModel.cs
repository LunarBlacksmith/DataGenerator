using System.Collections.ObjectModel;
using DataGenerator.Infrastructure;

namespace DataGenerator.Models;

public sealed class TableModel : ObservableObject
{
	private string _databaseName = string.Empty;
	private string _schemaName   = string.Empty;
	private string _name         = string.Empty;
	private bool _isSelected     = false;
	private int _rowCount        = 10;

	public string DatabaseName
	{
		get => _databaseName;
		set
		{
			if (SetProperty(ref _databaseName, value))
			{
				OnPropertyChanged(nameof(FullyQualifiedName));
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
				OnPropertyChanged(nameof(FullyQualifiedName));
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
				OnPropertyChanged(nameof(FullyQualifiedName));
			}
		}
	}

	public string FullyQualifiedName
		=> $"[{DatabaseName.Replace("]", "]]")}]."
			+ $"[{SchemaName.Replace("]", "]]")}]."
			+ $"[{Name.Replace("]", "]]")}]";

	public bool IsSelected
	{
		get => _isSelected;
		set => SetProperty(ref _isSelected, value);
	}

	public int RowCount
	{
		get => _rowCount;
		set => SetProperty(ref _rowCount, Math.Max(1, value));
	}

	public ObservableCollection<ColumnModel> Columns { get; } = [];
	public ObservableCollection<ForeignKeyModel> ForeignKeys { get; } = [];
}