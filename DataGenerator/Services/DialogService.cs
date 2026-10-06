using System.Windows;
using DataGenerator.Interfaces;
using DataGenerator.Models;

namespace DataGenerator.Services;

public sealed class DialogService : IDialogService
{
	/// <summary>
	///	Shows a warning confirmation dialog with OK and Cancel buttons.
	/// </summary>
	/// <param name="title">
	///	The dialog title.
	/// </param>
	/// <param name="message">
	///	The dialog message.
	/// </param>
	/// <returns>
	///	<see langword="true"/> when the user selects OK; otherwise <see langword="false"/>.
	/// </returns>
	public bool Confirm(string title, string message)
		=> Show(message, title, MessageBoxButton.OKCancel, MessageBoxImage.Warning, MessageBoxResult.Cancel) == MessageBoxResult.OK;

	/// <summary>
	///	Shows a question dialog with Yes, No and Cancel choices.
	/// </summary>
	/// <param name="title">
	///	The dialog title.
	/// </param>
	/// <param name="message">
	///	The dialog message.
	/// </param>
	/// <returns>
	///	The choice selected by the user, with window close treated as cancel.
	/// </returns>
	public DialogChoice AskYesNoCancel(string title, string message)
		=> Show(message, title, MessageBoxButton.YesNoCancel, MessageBoxImage.Question, MessageBoxResult.Cancel) switch
		{
			MessageBoxResult.Yes => DialogChoice.Yes,
			MessageBoxResult.No  => DialogChoice.No,
			_                    => DialogChoice.Cancel
		};

	/// <summary>
	///	Shows an error dialog with an OK button.
	/// </summary>
	/// <param name="title">
	///	The dialog title.
	/// </param>
	/// <param name="message">
	///	The dialog message.
	/// </param>
	public void ShowError(string title, string message)
		=> _ = Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK);

	/// <summary>
	///	Shows a WPF message box owned by the active application window when one is available.
	/// </summary>
	/// <param name="message">
	///	The message shown in the dialog body.
	/// </param>
	/// <param name="title">
	///	The dialog title.
	/// </param>
	/// <param name="buttons">
	///	The buttons to display.
	/// </param>
	/// <param name="image">
	///	The icon to display.
	/// </param>
	/// <param name="defaultResult">
	///	The default result used by the message box.
	/// </param>
	/// <returns>
	///	The button selected by the user.
	/// </returns>
	private static MessageBoxResult Show(
		string           message,
		string           title,
		MessageBoxButton buttons,
		MessageBoxImage  image,
		MessageBoxResult defaultResult
	)
	{
		Window? owner =
			Application.Current?.Windows.OfType<Window>().FirstOrDefault(window => window.IsActive)
			?? Application.Current?.MainWindow;

		return
			owner is null
				? MessageBox.Show(message, title, buttons, image, defaultResult)
				: MessageBox.Show(owner, message, title, buttons, image, defaultResult);
	}
}