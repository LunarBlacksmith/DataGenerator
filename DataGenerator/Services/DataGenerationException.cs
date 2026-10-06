namespace DataGenerator.Services;

/// <summary>
/// Wraps a failure that occurred while generating a specific value or row, recording where in the data it happened.
/// </summary>
public sealed class DataGenerationException : Exception
{
	public DataGenerationException(string message, string dataLocation, Exception? innerException = null)
		: base(message, innerException)
	{
		DataLocation = dataLocation;
	}

	/// <summary>
	/// Human readable location, e.g. [Shop].[dbo].[Shirt] › Set 'Set 1' › Row 5 › Column [Size].
	/// </summary>
	public string DataLocation { get; }
}