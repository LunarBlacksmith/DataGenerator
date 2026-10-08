using System.Windows.Controls;

namespace DataGenerator.Views;

/// <summary>
///	Row sets and column generation rules of the table being edited.
/// </summary>
public partial class ColumnRulesPanel : UserControl
{
	/// <summary>
	///	Creates the panel and loads its XAML-defined row-set and rule controls.
	/// </summary>
	public ColumnRulesPanel()
	{
		InitializeComponent();
	}
}