using DataGenerator.Models;

namespace DataGenerator.Interfaces;

public interface IDialogService
{
	bool Confirm(string title, string message);

	DialogChoice AskYesNoCancel(string title, string message);

	void ShowError(string title, string message);
}