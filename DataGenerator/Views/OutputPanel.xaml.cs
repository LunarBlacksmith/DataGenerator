using System.Windows.Controls;

namespace DataGenerator.Views;

/// <summary>
///	Output mode, script location, data cleanup options and the Generate button.
/// </summary>
public partial class OutputPanel : UserControl
{
	/// <summary>
	///	Creates the panel and loads its XAML-defined output and generation controls.
	/// </summary>
	public OutputPanel()
	{
		InitializeComponent();
	}
}