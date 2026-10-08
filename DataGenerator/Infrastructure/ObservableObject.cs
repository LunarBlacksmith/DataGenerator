using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DataGenerator.Infrastructure;

public abstract class ObservableObject : INotifyPropertyChanged
{
	#region EVENTS
	public event PropertyChangedEventHandler? PropertyChanged;
	#endregion EVENTS

	#region CONSTRUCTOR
	/// <summary>
	///	Creates a new <see cref="ObservableObject"/>.
	/// </summary>
	protected ObservableObject()
	{
	}
	#endregion CONSTRUCTOR

	#region METHODS
	/// <summary>
	///	Updates a backing field and raises a property change notification when the value actually changed.
	/// </summary>
	/// <typeparam name="T">
	///	The type of the backing field.
	/// </typeparam>
	/// <param name="field">
	///	The backing field to compare and update.
	/// </param>
	/// <param name="value">
	///	The new property value.
	/// </param>
	/// <param name="propertyName">
	///	The property name to report; filled in by the compiler when omitted.
	/// </param>
	/// <returns>
	///	<see langword="true"/> when the field was changed; otherwise <see langword="false"/>.
	/// </returns>
	protected bool SetProperty<T>(
		ref T field,
		T value,
		[CallerMemberName] string? propertyName = null
	)
	{
		if (EqualityComparer<T>.Default.Equals(field, value))
		{
			return false;
		}

		field = value;
		OnPropertyChanged(propertyName);
		return true;
	}

	/// <summary>
	///	Raises <see cref="PropertyChanged"/> for a property.
	/// </summary>
	/// <param name="propertyName">
	///	The property name to report; filled in by the compiler when omitted.
	/// </param>
	protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
		=> PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
	#endregion METHODS
}