using DataGenerator.Models;

namespace DataGenerator.Interfaces;

public interface IDialogService
{
	/// <summary>
	///	Shows an OK/Cancel confirmation dialog.
	/// </summary>
	/// <param name="title">
	///	The dialog title.
	/// </param>
	/// <param name="message">
	///	The message shown in the dialog body.
	/// </param>
	/// <returns>
	///	<see langword="true"/> when the user chooses OK; otherwise <see langword="false"/>.
	/// </returns>
	bool Confirm(string title, string message);

	/// <summary>
	///	Shows a Yes/No/Cancel question dialog.
	/// </summary>
	/// <param name="title">
	///	The dialog title.
	/// </param>
	/// <param name="message">
	///	The message shown in the dialog body.
	/// </param>
	/// <returns>
	///	The choice made by the user; closing the dialog returns <see cref="DialogChoice.Cancel"/>.
	/// </returns>
	DialogChoice AskYesNoCancel(string title, string message);

	/// <summary>
	///	Shows an error dialog with an OK button.
	/// </summary>
	/// <param name="title">
	///	The dialog title.
	/// </param>
	/// <param name="message">
	///	The error message shown in the dialog body.
	/// </param>
	void ShowError(string title, string message);
}