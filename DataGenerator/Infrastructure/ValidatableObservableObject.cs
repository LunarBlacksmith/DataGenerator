using System.Collections;
using System.ComponentModel;

namespace DataGenerator.Infrastructure;

/// <summary>
/// <see cref="ObservableObject"/> that reports one validation message per property through <see cref="INotifyDataErrorInfo"/>,
/// so bound controls show the standard WPF error template and tooltip.
/// </summary>
public abstract class ValidatableObservableObject : ObservableObject, INotifyDataErrorInfo
{
	private static readonly string[] NO_ERRORS = [];

	private readonly Dictionary<string, string> _errors = new Dictionary<string, string>(StringComparer.Ordinal);

	public event EventHandler<DataErrorsChangedEventArgs>? ErrorsChanged;

	public bool HasErrors => _errors.Count > 0;

	public IEnumerable GetErrors(string? propertyName)
		=> !string.IsNullOrEmpty(propertyName) && _errors.TryGetValue(propertyName, out string? error)
			? new string[] { error }
			: NO_ERRORS;

	protected string? GetError(string propertyName)
		=> _errors.TryGetValue(propertyName, out string? error) ? error : null;

	protected void SetError(string propertyName, string? error)
	{
		bool hadErrors = HasErrors;

		if (string.IsNullOrEmpty(error))
		{
			if (!_errors.Remove(propertyName))
			{
				return;
			}
		}
		else
		{
			if (_errors.TryGetValue(propertyName, out string? existingError) && existingError == error)
			{
				return;
			}

			_errors[propertyName] = error;
		}

		ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(propertyName));

		if (hadErrors != HasErrors)
		{
			OnPropertyChanged(nameof(HasErrors));
		}

		OnErrorsChanged();
	}

	protected void ClearErrors(params string[] propertyNames)
	{
		foreach (string propertyName in propertyNames)
		{
			SetError(propertyName, null);
		}
	}

	/// <summary>
	/// Called after any validation message was added, changed or removed.
	/// </summary>
	protected virtual void OnErrorsChanged()
	{
	}
}