using System.Windows.Controls;

namespace DataGenerator.Views;

/// <summary>
///	SQL Server connection details and the button that loads the database metadata.
/// </summary>
public partial class ConnectionPanel : UserControl
{
	#region CONSTRUCTORS
	#region PUBLIC
	/// <summary>
	///	Creates the panel and loads its XAML-defined connection controls.
	/// </summary>
	public ConnectionPanel()
	{
		InitializeComponent();
	}
	#endregion PUBLIC
	#endregion CONSTRUCTORS
}