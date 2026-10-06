using System.Windows;
using DataGenerator.Interfaces;

namespace DataGenerator.Services;

public sealed class ClipboardService : IClipboardService
{
	public void SetText(string text)
	{
		ArgumentNullException.ThrowIfNull(text);

		// WPF retries internally while another application holds the clipboard open.
		Clipboard.SetDataObject(text, true);
	}
}