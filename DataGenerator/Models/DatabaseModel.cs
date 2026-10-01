using System.Collections.ObjectModel;
using DataGenerator.Infrastructure;

namespace DataGenerator.Models;

public sealed class DatabaseModel : ObservableObject
{
	private string _name = string.Empty;

	public string Name
	{
		get => _name;
		set => SetProperty(ref _name, value);
	}

	public ObservableCollection<TableModel> Tables { get; } = [];
}