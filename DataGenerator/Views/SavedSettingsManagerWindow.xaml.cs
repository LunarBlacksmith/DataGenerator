using System.Windows;

namespace DataGenerator.Views;

/// <summary>
///	Lists the saved column settings for renaming, deleting, importing and exporting.
/// </summary>
public partial class SavedSettingsManagerWindow : Window
{
	#region CONSTRUCTORS
	#region PUBLIC
	/// <summary>
	///	Creates the window and loads its XAML-defined saved-setting management controls.
	/// </summary>
	public SavedSettingsManagerWindow()
	{
		InitializeComponent();
	}
	#endregion PUBLIC
	#endregion CONSTRUCTORS
}