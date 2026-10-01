using System.Windows.Input;

namespace DataGenerator.Infrastructure;

public sealed class AsyncRelayCommand : ICommand
{
	private readonly Func<object?, Task> _executeAsync;
	private readonly Predicate<object?>? _canExecute;
	private bool _isExecuting;

	public AsyncRelayCommand(Func<object?, Task> executeAsync, Predicate<object?>? canExecute = null)
	{
		_executeAsync = executeAsync ?? throw new ArgumentNullException(nameof(executeAsync));
		_canExecute   = canExecute;
	}

	public event EventHandler? CanExecuteChanged;

	public bool CanExecute(object? parameter) => !_isExecuting && (_canExecute?.Invoke(parameter) ?? true);

	public async void Execute(object? parameter)
	{
		if (!CanExecute(parameter))
		{
			return;
		}

		try
		{
			_isExecuting = true;
			NotifyCanExecuteChanged();
			await _executeAsync(parameter);
		}
		finally
		{
			_isExecuting = false;
			NotifyCanExecuteChanged();
		}
	}

	public void NotifyCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}