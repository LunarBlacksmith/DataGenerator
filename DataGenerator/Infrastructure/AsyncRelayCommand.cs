using System.Windows.Input;

namespace DataGenerator.Infrastructure;

public sealed class AsyncRelayCommand : ICommand
{
	#region FIELDS
	#region PRIVATE
	private readonly Func<object?, Task> _executeAsync;
	private readonly Predicate<object?>? _canExecute;
	private bool _isExecuting;
	#endregion PRIVATE
	#endregion FIELDS

	#region EVENTS
	#region PUBLIC
	public event EventHandler? CanExecuteChanged;
	#endregion PUBLIC
	#endregion EVENTS

	#region CONSTRUCTORS
	#region PUBLIC
	/// <summary>
	///	Creates a command that runs asynchronous work and disables itself while that work is running.
	/// </summary>
	/// <param name="executeAsync">
	///	The asynchronous action to run when the command executes.
	/// </param>
	/// <param name="canExecute">
	///	An optional test for whether the command may run for the supplied parameter.
	/// </param>
	/// <exception cref="ArgumentNullException">
	///	Thrown when <paramref name="executeAsync"/> is <see langword="null"/>.
	/// </exception>
	public AsyncRelayCommand(Func<object?, Task> executeAsync, Predicate<object?>? canExecute = null)
	{
		_isExecuting = false;

		_executeAsync = executeAsync ?? throw new ArgumentNullException(nameof(executeAsync));
		_canExecute   = canExecute;
	}
	#endregion PUBLIC
	#endregion CONSTRUCTORS

	#region METHODS
	#region PUBLIC
	/// <summary>
	///	Checks whether the command is idle and the optional predicate allows execution.
	/// </summary>
	/// <param name="parameter">
	///	The command parameter passed by WPF.
	/// </param>
	/// <returns>
	///	<see langword="true"/> when the command can currently execute; otherwise <see langword="false"/>.
	/// </returns>
	public bool CanExecute(object? parameter) => !_isExecuting && (_canExecute?.Invoke(parameter) ?? true);

	/// <summary>
	///	Runs the asynchronous command action when execution is currently allowed.
	/// </summary>
	/// <param name="parameter">
	///	The command parameter passed to the asynchronous action.
	/// </param>
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

	/// <summary>
	///	Raises <see cref="CanExecuteChanged"/> so WPF queries the command state again.
	/// </summary>
	public void NotifyCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
	#endregion PUBLIC
	#endregion METHODS
}