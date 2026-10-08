using System.Windows.Controls;

namespace DataGenerator.Views;

/// <summary>
///	Tree of every database and its tables, with filtering, hiding, multi-selection and bulk actions.
/// </summary>
public partial class DatabaseExplorerPanel : UserControl
{
	/// <summary>
	///	Creates the panel and loads its XAML-defined database tree controls.
	/// </summary>
	public DatabaseExplorerPanel()
	{
		InitializeComponent();
	}
}