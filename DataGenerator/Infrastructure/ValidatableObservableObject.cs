using System.Collections;
using System.ComponentModel;

namespace DataGenerator.Infrastructure;

/// <summary>
///	<see cref="ObservableObject"/> that reports one validation message per property through <see cref="INotifyDataErrorInfo"/>,
///	so bound controls show the standard WPF error template and tooltip.
/// </summary>
public abstract class ValidatableObservableObject : ObservableObject, INotifyDataErrorInfo
{
	#region FIELDS
	private static readonly string[] NO_ERRORS;

	private readonly Dictionary<string, string> _errors;
	#endregion FIELDS

	#region PROPERTIES
	public bool HasErrors => _errors.Count > 0;
	#endregion PROPERTIES

	#region EVENTS
	public event EventHandler<DataErrorsChangedEventArgs>? ErrorsChanged;
	#endregion EVENTS

	#region CONSTRUCTOR
	/// <summary>
	///	Sets the default values of the static fields and properties of <see cref="ValidatableObservableObject"/>.
	/// </summary>
	static ValidatableObservableObject()
	{
		NO_ERRORS = [];
	}

	/// <summary>
	///	Creates a new <see cref="ValidatableObservableObject"/> and sets the default values of its fields and properties.
	/// </summary>
	protected ValidatableObservableObject()
	{
		_errors = new(StringComparer.Ordinal);
	}
	#endregion CONSTRUCTOR

	#region METHODS
	#region PUBLIC
	/// <summary>
	///	Gets the validation messages for one property.
	/// </summary>
	/// <param name="propertyName">
	///	The property whose messages are requested.
	/// </param>
	/// <returns>
	///	The property's validation message as a one-item sequence, or an empty sequence when it has no message.
	/// </returns>
	public IEnumerable GetErrors(string? propertyName)
		=>
			!string.IsNullOrEmpty(propertyName) && _errors.TryGetValue(propertyName, out string? error)
				? new string[] { error }
				: NO_ERRORS;
	#endregion PUBLIC

	#region PROTECTED
	/// <summary>
	///	Gets the validation message currently stored for one property.
	/// </summary>
	/// <param name="propertyName">
	///	The property whose message is requested.
	/// </param>
	/// <returns>
	///	The current validation message, or <see langword="null"/> when the property has no message.
	/// </returns>
	protected string? GetError(string propertyName)
		=> _errors.TryGetValue(propertyName, out string? error) ? error : null;

	/// <summary>
	///	Adds, changes or clears the validation message for one property and raises the related notifications.
	/// </summary>
	/// <param name="propertyName">
	///	The property whose validation message changed.
	/// </param>
	/// <param name="error">
	///	The message to store, or <see langword="null"/> or empty to clear the property.
	/// </param>
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

	/// <summary>
	///	Clears validation messages for the supplied properties.
	/// </summary>
	/// <param name="propertyNames">
	///	The property names whose messages should be removed.
	/// </param>
	protected void ClearErrors(params string[] propertyNames)
	{
		foreach (string propertyName in propertyNames)
		{
			SetError(propertyName, null);
		}
	}

	/// <summary>
	///	Called after any validation message was added, changed or removed.
	/// </summary>
	protected virtual void OnErrorsChanged()
	{
	}
	#endregion PROTECTED
	#endregion METHODS
}