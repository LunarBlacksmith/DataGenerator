using System.Windows;
using System.Windows.Controls;

namespace DataGenerator.Infrastructure;

/// <summary>
///	How hints (tool tips) are shown throughout the application: how far from the mouse pointer they appear and how long
///	they stay open.
///	<para>
///		A hint closes on its own after a time that grows with the length of its text, so a short hint does not linger
///		while a long one stays open long enough to be read. Moving the mouse away still closes a hint straight away.
///	</para>
/// </summary>
public static class HintPresentation
{
	#region FIELDS
	#region PUBLIC
	/// <summary>
	///	How far, in device-independent pixels, a hint is moved to the right of the mouse pointer.
	/// </summary>
	public const double HORIZONTAL_OFFSET = 16;

	/// <summary>
	///	How far, in device-independent pixels, a hint is moved below the mouse pointer.
	/// </summary>
	public const double VERTICAL_OFFSET = 12;

	/// <summary>
	///	The time every hint is shown for before its reading time is added.
	/// </summary>
	public const int BASE_SHOW_DURATION_MILLISECONDS = 2000;

	/// <summary>
	///	The reading time added for every character of a hint's text (about 200 words a minute).
	/// </summary>
	public const int MILLISECONDS_PER_CHARACTER = 60;

	/// <summary>
	///	The shortest time a hint is shown for.
	/// </summary>
	public const int MINIMUM_SHOW_DURATION_MILLISECONDS = 5000;

	/// <summary>
	///	The longest time a hint is shown for, however long its text is.
	/// </summary>
	public const int MAXIMUM_SHOW_DURATION_MILLISECONDS = 45000;
	#endregion PUBLIC

	#region PRIVATE
	private static bool IS_REGISTERED;
	#endregion PRIVATE
	#endregion FIELDS

	#region CONSTRUCTORS
	#region STATIC
	/// <summary>
	///	Sets the default values of the static fields and properties of <see cref="HintPresentation"/>.
	/// </summary>
	static HintPresentation()
	{
		IS_REGISTERED = false;
	}
	#endregion STATIC
	#endregion CONSTRUCTORS

	#region METHODS
	#region PUBLIC
	/// <summary>
	///	Makes every hint in the application close on its own after its reading time. Call once at start-up, before any
	///	window is shown; later calls do nothing.
	/// </summary>
	public static void Register()
	{
		if (IS_REGISTERED)
		{
			return;
		}

		ToolTipEventHandler handler = OnToolTipOpening;

		EventManager.RegisterClassHandler(typeof(FrameworkElement), ToolTipService.ToolTipOpeningEvent, handler);
		EventManager.RegisterClassHandler(typeof(FrameworkContentElement), ToolTipService.ToolTipOpeningEvent, handler);

		IS_REGISTERED = true;
	}

	/// <summary>
	///	How long a hint with <paramref name="hintText"/> stays open: the base time plus the reading time of the text,
	///	kept between the minimum and maximum show durations.
	/// </summary>
	/// <param name="hintText">
	///	The text of the hint; <see langword="null"/> when the hint is not plain text.
	/// </param>
	/// <returns>
	///	The show duration in milliseconds.
	/// </returns>
	public static int GetShowDuration(string? hintText)
	{
		long readingTime = (long)BASE_SHOW_DURATION_MILLISECONDS + ((long)(hintText?.Length ?? 0) * MILLISECONDS_PER_CHARACTER);

		return (int)Math.Clamp(readingTime, MINIMUM_SHOW_DURATION_MILLISECONDS, MAXIMUM_SHOW_DURATION_MILLISECONDS);
	}
	#endregion PUBLIC

	#region PRIVATE
	/// <summary>
	///	Sets the show duration of the hint that is about to open. WPF reads the duration from the hint's owner straight
	///	after this event, so the value applies to this opening.
	///	<para>
	///		The value is set with <see cref="DependencyObject.SetCurrentValue"/> so that a duration set explicitly on an
	///		element is never replaced and the duration is worked out again whenever the hint's text changes.
	///	</para>
	/// </summary>
	/// <param name="sender">
	///	The element that owns the hint.
	/// </param>
	/// <param name="e">
	///	The event data of the opening hint.
	/// </param>
	private static void OnToolTipOpening(object sender, ToolTipEventArgs e)
	{
		if (sender is not DependencyObject owner)
		{
			return;
		}

		ValueSource source = DependencyPropertyHelper.GetValueSource(owner, ToolTipService.ShowDurationProperty);

		if (source.BaseValueSource != BaseValueSource.Default)
		{
			return;
		}

		owner.SetCurrentValue(ToolTipService.ShowDurationProperty, GetShowDuration(GetHintText(ToolTipService.GetToolTip(owner))));
	}

	/// <summary>
	///	The text of a hint, which is either the text itself or a <see cref="ToolTip"/> whose content is text.
	/// </summary>
	/// <param name="hint">
	///	The value of the owner's <c>ToolTip</c> property.
	/// </param>
	/// <returns>
	///	The text of the hint, or <see langword="null"/> when the hint is not plain text.
	/// </returns>
	private static string? GetHintText(object? hint)
		=> hint switch
		{
			string text                               => text,
			ToolTip { Content: string toolTipText }   => toolTipText,
			_                                         => null
		};
	#endregion PRIVATE
	#endregion METHODS
}
