using System.Windows.Controls;

namespace DataGenerator.Views;

/// <summary>
/// Tree of every database and its tables, with filtering, hiding, multi-selection and bulk actions.
/// </summary>
public partial class DatabaseExplorerPanel : UserControl
{
	public DatabaseExplorerPanel()
	{
		InitializeComponent();
	}
}