using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace DataGenerator.Infrastructure;

/// <summary>
///	Switches a window's title bar between the light and dark Windows styles, so it matches the application theme.
/// </summary>
public static class WindowTitleBar
{
	#region FIELDS
	#region PRIVATE
	private const int USE_IMMERSIVE_DARK_MODE_ATTRIBUTE        = 20;
	private const int LEGACY_USE_IMMERSIVE_DARK_MODE_ATTRIBUTE = 19;
	private const int S_OK                                     = 0;
	#endregion PRIVATE
	#endregion FIELDS

	#region CONSTRUCTORS
	#region STATIC
	/// <summary>
	///	Initialises the static state of <see cref="WindowTitleBar"/>.
	/// </summary>
	static WindowTitleBar()
	{
	}
	#endregion STATIC
	#endregion CONSTRUCTORS

	#region METHODS
	#region PUBLIC
	/// <summary>
	///	Asks Windows to draw the title bar dark or light; does nothing when the window has no handle yet or Windows does not support it.
	/// </summary>
	/// <param name="window">
	///	The window whose title bar should be updated.
	/// </param>
	/// <param name="useDarkMode">
	///	Whether the title bar should use the dark Windows style.
	/// </param>
	/// <exception cref="ArgumentNullException">
	///	Thrown when <paramref name="window"/> is <see langword="null"/>.
	/// </exception>
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
	#endregion PUBLIC

	#region PRIVATE
	/// <summary>
	///	Sets a Desktop Window Manager attribute on a native window handle.
	/// </summary>
	/// <param name="handle">
	///	The native window handle to update.
	/// </param>
	/// <param name="attribute">
	///	The DWM attribute identifier to set.
	/// </param>
	/// <param name="value">
	///	The integer value to assign to the attribute.
	/// </param>
	/// <param name="size">
	///	The size, in bytes, of <paramref name="value"/>.
	/// </param>
	/// <returns>
	///	The HRESULT returned by DWM; <c>0</c> means success.
	/// </returns>
	[DllImport("dwmapi.dll")]
	private static extern int DwmSetWindowAttribute(IntPtr handle, int attribute, ref int value, int size);
	#endregion PRIVATE
	#endregion METHODS
}