using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace DataGenerator.Infrastructure;

/// <summary>
/// Switches a window's title bar between the light and dark Windows styles, so it matches the application theme.
/// </summary>
public static class WindowTitleBar
{
	private const int USE_IMMERSIVE_DARK_MODE_ATTRIBUTE        = 20;
	private const int LEGACY_USE_IMMERSIVE_DARK_MODE_ATTRIBUTE = 19;
	private const int S_OK                                     = 0;

	/// <summary>
	/// Asks Windows to draw the title bar dark or light; does nothing when the window has no handle yet or Windows does not support it.
	/// </summary>
	public static void Apply(Window window, bool useDarkMode)
	{
		ArgumentNullException.ThrowIfNull(window);

		IntPtr handle = new WindowInteropHelper(window).Handle;

		if (handle == IntPtr.Zero)
		{
			return;
		}

		int value = useDarkMode ? 1 : 0;

		// Windows 10 builds before 20H1 only know the older attribute number.
		if (DwmSetWindowAttribute(handle, USE_IMMERSIVE_DARK_MODE_ATTRIBUTE, ref value, sizeof(int)) != S_OK)
		{
			_ = DwmSetWindowAttribute(handle, LEGACY_USE_IMMERSIVE_DARK_MODE_ATTRIBUTE, ref value, sizeof(int));
		}
	}

	[DllImport("dwmapi.dll")]
	private static extern int DwmSetWindowAttribute(IntPtr handle, int attribute, ref int value, int size);
}