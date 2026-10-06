using System.Collections.ObjectModel;
using DataGenerator.Infrastructure;

namespace DataGenerator.Models;

public sealed class DatabaseModel : ObservableObject
{
	#region FIELDS
	#region PRIVATE
	private string _name;
	#endregion PRIVATE
	#endregion FIELDS

	#region PROPERTIES
	#region PUBLIC
	public string Name
	{
		get => _name;
		set => SetProperty(ref _name, value);
	}

	public ObservableCollection<TableModel> Tables { get; }
	#endregion PUBLIC
	#endregion PROPERTIES

	#region CONSTRUCTORS
	#region PUBLIC
	/// <summary>
	///	Creates a new <see cref="DatabaseModel"/> and sets the default values of its fields and properties.
	/// </summary>
	public DatabaseModel()
	{
		_name  = string.Empty;
		Tables = [];
	}
	#endregion PUBLIC
	#endregion CONSTRUCTORS
}