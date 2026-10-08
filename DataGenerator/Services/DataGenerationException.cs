namespace DataGenerator.Services;

/// <summary>
///	Wraps a failure that occurred while generating a specific value or row, recording where in the data it happened.
/// </summary>
public sealed class DataGenerationException : Exception
{
	#region PROPERTIES
	/// <summary>
	///	Human readable location, e.g. [Shop].[dbo].[Shirt] › Set 'Set 1' › Row 5 › Column [Size].
	/// </summary>
	public string DataLocation { get; }
	#endregion PROPERTIES

	/// <summary>
	///	Creates an exception for a generation failure at a specific data location.
	/// </summary>
	/// <param name="message">
	///	The message that describes the failure.
	/// </param>
	/// <param name="dataLocation">
	///	The table, set, row or column location where the failure occurred.
	/// </param>
	/// <param name="innerException">
	///	The exception that caused this failure, or <see langword="null"/> when there is no inner exception.
	/// </param>
	public DataGenerationException(string message, string dataLocation, Exception? innerException = null)
		: base(message, innerException)
	{
		DataLocation = dataLocation;
	}
}