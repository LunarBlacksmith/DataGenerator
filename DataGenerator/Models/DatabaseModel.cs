using System.Collections.ObjectModel;
using DataGenerator.Infrastructure;

namespace DataGenerator.Models;

public sealed class DatabaseModel : ObservableObject
{
	#region FIELDS
	private string _name;
	#endregion FIELDS

	#region PROPERTIES
	public string Name
	{
		get => _name;
		set => SetProperty(ref _name, value);
	}

	public ObservableCollection<TableModel> Tables { get; }
	#endregion PROPERTIES

	/// <summary>
	///	Creates a new <see cref="DatabaseModel"/> and sets the default values of its fields and properties.
	/// </summary>
	public DatabaseModel()
	{
		_name  = string.Empty;
		Tables = [];
	}
}