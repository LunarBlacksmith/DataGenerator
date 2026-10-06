using System.Windows;
using DataGenerator.Interfaces;

namespace DataGenerator.Services;

public sealed class ClipboardService : IClipboardService
{
	/// <summary>
	///	Places text on the Windows clipboard and keeps the data available after the application exits.
	/// </summary>
	/// <param name="text">
	///	The text to copy to the clipboard.
	/// </param>
	/// <exception cref="ArgumentNullException">
	///	Thrown when <paramref name="text"/> is <see langword="null"/>.
	/// </exception>
	public void SetText(string text)
	{
		ArgumentNullException.ThrowIfNull(text);

		// WPF retries internally while another application holds the clipboard open.
		Clipboard.SetDataObject(text, true);
	}
}