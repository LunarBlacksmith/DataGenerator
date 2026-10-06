using System.Windows.Controls;

namespace DataGenerator.Views;

/// <summary>
///	The last error: what happened, where it happened and the technical details.
/// </summary>
public partial class ErrorPanel : UserControl
{
	/// <summary>
	///	The tallest the message area grows before it scrolls, so a long error never squashes the rest of the window.
	/// </summary>
	public const double MAXIMUM_MESSAGE_HEIGHT = 110;

	/// <summary>
	///	Creates the panel and loads its XAML-defined error display controls.
	/// </summary>
	public ErrorPanel()
	{
		InitializeComponent();
	}
}