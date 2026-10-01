using System.Windows;

namespace DataGenerator.Services;

public sealed class DialogService : IDialogService
{
	public bool Confirm(string title, string message)
		=> Show(message, title, MessageBoxButton.OKCancel, MessageBoxImage.Warning, MessageBoxResult.Cancel) == MessageBoxResult.OK;

	public DialogChoice AskYesNoCancel(string title, string message)
	{
		return Show(message, title, MessageBoxButton.YesNoCancel, MessageBoxImage.Question, MessageBoxResult.Cancel) switch
		{
			MessageBoxResult.Yes => DialogChoice.Yes,
			MessageBoxResult.No  => DialogChoice.No,
			_                    => DialogChoice.Cancel
		};
	}

	public void ShowError(string title, string message)
		=> _ = Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK);

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

		return owner is null
			? MessageBox.Show(message, title, buttons, image, defaultResult)
			: MessageBox.Show(owner, message, title, buttons, image, defaultResult);
	}
}