using System.Windows.Input;

namespace DataGenerator.Infrastructure;

public sealed class RelayCommand : ICommand
{
	private readonly Action<object?> _execute;
	private readonly Predicate<object?>? _canExecute;

	/// <summary>
	///	Creates a command from delegates supplied by a view model.
	/// </summary>
	/// <param name="execute">
	///	The action to run when the command executes.
	/// </param>
	/// <param name="canExecute">
	///	An optional test for whether the command may run for the supplied parameter.
	/// </param>
	/// <exception cref="ArgumentNullException">
	///	Thrown when <paramref name="execute"/> is <see langword="null"/>.
	/// </exception>
	public RelayCommand(Action<object?> execute, Predicate<object?>? canExecute = null)
	{
		_execute    = execute ?? throw new ArgumentNullException(nameof(execute));
		_canExecute = canExecute;
	}

	public event EventHandler? CanExecuteChanged;

	/// <summary>
	///	Checks whether the command may execute for the supplied parameter.
	/// </summary>
	/// <param name="parameter">
	///	The command parameter passed by WPF.
	/// </param>
	/// <returns>
	///	<see langword="true"/> when the command can execute; otherwise <see langword="false"/>.
	/// </returns>
	public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;

	/// <summary>
	///	Runs the command action.
	/// </summary>
	/// <param name="parameter">
	///	The command parameter passed to the action.
	/// </param>
	public void Execute(object? parameter)    => _execute(parameter);

	/// <summary>
	///	Raises <see cref="CanExecuteChanged"/> so WPF queries the command state again.
	/// </summary>
	public void NotifyCanExecuteChanged()     => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}