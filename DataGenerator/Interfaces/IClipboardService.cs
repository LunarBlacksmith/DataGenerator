namespace DataGenerator.Interfaces;

public interface IClipboardService
{
	/// <summary>
	///	Places text on the Windows clipboard.
	/// </summary>
	/// <param name="text">
	///	The text to copy.
	/// </param>
	/// <exception cref="ArgumentNullException">
	///	Thrown when <paramref name="text"/> is <see langword="null"/>.
	/// </exception>
	void SetText(string text);
}