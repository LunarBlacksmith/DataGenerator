namespace DataGenerator.Services;

public enum DialogChoice
{
	Yes    = 0,
	No     = 1,
	Cancel = 2
}

public interface IDialogService
{
	bool Confirm(string title, string message);

	DialogChoice AskYesNoCancel(string title, string message);

	void ShowError(string title, string message);
}