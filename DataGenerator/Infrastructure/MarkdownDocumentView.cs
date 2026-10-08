using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using DataGenerator.Services;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace DataGenerator.Infrastructure;

/// <summary>
///	Shows a formatted HTML document, such as a rendered Markdown guide, in an embedded WebView2 browser styled with the
///	application's theme brushes.
///	<para>
///		Scripts in the page cannot reach the application: links are handed to <see cref="LinkCommand"/>, other navigation is
///		blocked and only the page's own inline script may run. The page follows theme changes without losing its scroll
///		position.
///	</para>
///	<para>
///		When the WebView2 Runtime is not available, the raw Markdown is shown in a read-only text box instead.
///	</para>
/// </summary>
public sealed class MarkdownDocumentView : Decorator
{
	#region FIELDS
	#region PUBLIC
	public static readonly DependencyProperty HTML_PROPERTY;
	public static readonly DependencyProperty MARKDOWN_PROPERTY;
	public static readonly DependencyProperty FRAGMENT_PROPERTY;
	public static readonly DependencyProperty LINK_COMMAND_PROPERTY;
	public static readonly DependencyProperty PAGE_BACKGROUND_PROPERTY;
	public static readonly DependencyProperty PAGE_FOREGROUND_PROPERTY;
	public static readonly DependencyProperty SECONDARY_FOREGROUND_PROPERTY;
	public static readonly DependencyProperty LINK_BRUSH_PROPERTY;
	public static readonly DependencyProperty RULE_BRUSH_PROPERTY;
	public static readonly DependencyProperty CODE_BACKGROUND_PROPERTY;
	public static readonly DependencyProperty HEADER_BACKGROUND_PROPERTY;
	public static readonly DependencyProperty ALTERNATE_ROW_BACKGROUND_PROPERTY;
	#endregion PUBLIC

	#region PRIVATE
	private const string WEB_VIEW_DATA_DIRECTORY = "WebView2";
	private const double DARK_LUMINANCE_LIMIT    = 128;
	private const string STYLE_SHEET             =
		"""
		html, body { margin: 0; padding: 0; background: var(--bg); color: var(--fg); }
		body { font-family: "Segoe UI", system-ui, sans-serif; font-size: 15px; line-height: 1.55; padding: 20px 28px 48px; word-wrap: break-word; }
		h1, h2, h3, h4, h5, h6 { margin: 1.4em 0 0.6em; font-weight: 600; line-height: 1.25; }
		h1 { font-size: 2em; padding-bottom: 0.3em; border-bottom: 1px solid var(--border); margin-top: 0.2em; }
		h2 { font-size: 1.5em; padding-bottom: 0.3em; border-bottom: 1px solid var(--border); }
		h3 { font-size: 1.25em; }
		h4 { font-size: 1em; }
		h5, h6 { font-size: 0.9em; color: var(--muted); }
		p, ul, ol, dl, table, pre, blockquote { margin: 0 0 16px; }
		ul, ol { padding-left: 2em; }
		li + li { margin-top: 0.25em; }
		a { color: var(--link); text-decoration: none; }
		a:hover { text-decoration: underline; }
		hr { height: 1px; border: 0; background: var(--border); margin: 24px 0; }
		code, kbd, pre { font-family: Consolas, "Cascadia Mono", monospace; font-size: 0.9em; }
		code { background: var(--code); border-radius: 4px; padding: 0.15em 0.35em; }
		pre { background: var(--code); border: 1px solid var(--border); border-radius: 6px; padding: 12px 16px; overflow-x: auto; line-height: 1.45; }
		pre code { background: transparent; padding: 0; border-radius: 0; }
		kbd { display: inline-block; padding: 2px 6px; border: 1px solid var(--border); border-bottom-width: 2px; border-radius: 4px; background: var(--code); line-height: 1.2; }
		blockquote { margin-left: 0; padding: 4px 16px; color: var(--muted); border-left: 4px solid var(--border); }
		table { display: block; width: max-content; max-width: 100%; overflow-x: auto; border-collapse: collapse; }
		th, td { border: 1px solid var(--border); padding: 6px 12px; vertical-align: top; }
		th { background: var(--header); font-weight: 600; text-align: left; }
		tr:nth-child(even) td { background: var(--alt); }
		img { max-width: 100%; }
		""";
	private const string PAGE_SCRIPT             =
		"""
		function scrollToFragment(id) {
			const target = document.getElementById(id) || document.getElementById(id.toLowerCase()) || document.getElementsByName(id)[0];
			if (target) {
				target.scrollIntoView({ block: 'start' });
			}
		}
		function setTheme(css) {
			document.getElementById('theme').textContent = css;
		}
		document.addEventListener('click', function (e) {
			const link = e.target && e.target.closest ? e.target.closest('a[href]') : null;
			if (!link) {
				return;
			}
			e.preventDefault();
			const href = link.getAttribute('href');
			if (href.charAt(0) === '#') {
				scrollToFragment(decodeURIComponent(href.substring(1)));
				return;
			}
			window.chrome.webview.postMessage(href);
		});
		document.addEventListener('auxclick', function (e) {
			if (e.target && e.target.closest && e.target.closest('a[href]')) {
				e.preventDefault();
			}
		});
		""";

	private WebView2? _webView;
	private bool      _isReady;
	private bool      _isNavigating;
	private bool      _isInitializing;
	private string?   _pendingFragment;
	#endregion PRIVATE
	#endregion FIELDS

	#region PROPERTIES
	public string? Html
	{
		get => (string?)GetValue(HTML_PROPERTY);
		set => SetValue(HTML_PROPERTY, value);
	}

	public string? Markdown
	{
		get => (string?)GetValue(MARKDOWN_PROPERTY);
		set => SetValue(MARKDOWN_PROPERTY, value);
	}

	public string? Fragment
	{
		get => (string?)GetValue(FRAGMENT_PROPERTY);
		set => SetValue(FRAGMENT_PROPERTY, value);
	}

	public ICommand? LinkCommand
	{
		get => (ICommand?)GetValue(LINK_COMMAND_PROPERTY);
		set => SetValue(LINK_COMMAND_PROPERTY, value);
	}

	public Brush? PageBackground
	{
		get => (Brush?)GetValue(PAGE_BACKGROUND_PROPERTY);
		set => SetValue(PAGE_BACKGROUND_PROPERTY, value);
	}

	public Brush? PageForeground
	{
		get => (Brush?)GetValue(PAGE_FOREGROUND_PROPERTY);
		set => SetValue(PAGE_FOREGROUND_PROPERTY, value);
	}

	public Brush? SecondaryForeground
	{
		get => (Brush?)GetValue(SECONDARY_FOREGROUND_PROPERTY);
		set => SetValue(SECONDARY_FOREGROUND_PROPERTY, value);
	}

	public Brush? LinkBrush
	{
		get => (Brush?)GetValue(LINK_BRUSH_PROPERTY);
		set => SetValue(LINK_BRUSH_PROPERTY, value);
	}

	public Brush? RuleBrush
	{
		get => (Brush?)GetValue(RULE_BRUSH_PROPERTY);
		set => SetValue(RULE_BRUSH_PROPERTY, value);
	}

	public Brush? CodeBackground
	{
		get => (Brush?)GetValue(CODE_BACKGROUND_PROPERTY);
		set => SetValue(CODE_BACKGROUND_PROPERTY, value);
	}

	public Brush? HeaderBackground
	{
		get => (Brush?)GetValue(HEADER_BACKGROUND_PROPERTY);
		set => SetValue(HEADER_BACKGROUND_PROPERTY, value);
	}

	public Brush? AlternateRowBackground
	{
		get => (Brush?)GetValue(ALTERNATE_ROW_BACKGROUND_PROPERTY);
		set => SetValue(ALTERNATE_ROW_BACKGROUND_PROPERTY, value);
	}
	#endregion PROPERTIES

	#region CONSTRUCTOR
	/// <summary>
	///	Registers the document, link and theme properties of <see cref="MarkdownDocumentView"/>.
	/// </summary>
	static MarkdownDocumentView()
	{
		HTML_PROPERTY                     = DependencyProperty.Register(
			nameof(Html),
			typeof(string),
			typeof(MarkdownDocumentView),
			new PropertyMetadata(null, OnHtmlChanged)
		);
		MARKDOWN_PROPERTY                 = DependencyProperty.Register(
			nameof(Markdown),
			typeof(string),
			typeof(MarkdownDocumentView),
			new PropertyMetadata(null)
		);
		FRAGMENT_PROPERTY                 = DependencyProperty.Register(
			nameof(Fragment),
			typeof(string),
			typeof(MarkdownDocumentView),
			new PropertyMetadata(null, OnFragmentChanged)
		);
		LINK_COMMAND_PROPERTY             = DependencyProperty.Register(
			nameof(LinkCommand),
			typeof(ICommand),
			typeof(MarkdownDocumentView),
			new PropertyMetadata(null)
		);
		PAGE_BACKGROUND_PROPERTY          = RegisterThemeBrush(nameof(PageBackground));
		PAGE_FOREGROUND_PROPERTY          = RegisterThemeBrush(nameof(PageForeground));
		SECONDARY_FOREGROUND_PROPERTY     = RegisterThemeBrush(nameof(SecondaryForeground));
		LINK_BRUSH_PROPERTY               = RegisterThemeBrush(nameof(LinkBrush));
		RULE_BRUSH_PROPERTY               = RegisterThemeBrush(nameof(RuleBrush));
		CODE_BACKGROUND_PROPERTY          = RegisterThemeBrush(nameof(CodeBackground));
		HEADER_BACKGROUND_PROPERTY        = RegisterThemeBrush(nameof(HeaderBackground));
		ALTERNATE_ROW_BACKGROUND_PROPERTY = RegisterThemeBrush(nameof(AlternateRowBackground));
	}

	/// <summary>
	///	Creates the view; the browser itself starts when the view is loaded.
	/// </summary>
	public MarkdownDocumentView()
	{
		_webView         = null;
		_isReady         = false;
		_isNavigating    = false;
		_isInitializing  = false;
		_pendingFragment = null;

		Loaded   += OnLoaded;
		Unloaded += OnUnloaded;
	}
	#endregion CONSTRUCTOR

	#region METHODS
	#region PUBLIC
	/// <summary>
	///	Builds the complete page shown by the browser: a strict content security policy, the theme, the style sheet, the
	///	sanitized document and the link-handling script.
	/// </summary>
	/// <param name="bodyHtml">
	///	The rendered document.
	/// </param>
	/// <param name="themeCss">
	///	The theme variables created by <see cref="BuildThemeCss"/>.
	/// </param>
	/// <param name="nonce">
	///	The one-time value that allows only this page's own script to run.
	/// </param>
	/// <returns>
	///	The HTML document.
	/// </returns>
	public static string BuildPage(string bodyHtml, string themeCss, string nonce)
	{
		StringBuilder page = new();

		_ = page.Append("<!DOCTYPE html><html><head><meta charset=\"utf-8\">");
		_ = page.Append("<meta http-equiv=\"Content-Security-Policy\" content=\"default-src 'none'; style-src 'unsafe-inline'; img-src data:; script-src 'nonce-");
		_ = page.Append(nonce);
		_ = page.Append("'\">");
		_ = page.Append("<style id=\"theme\">").Append(themeCss).Append("</style>");
		_ = page.Append("<style>").Append(STYLE_SHEET).Append("</style>");
		_ = page.Append("</head><body>");
		_ = page.Append(MarkdownHtmlSanitizer.Sanitize(bodyHtml));
		_ = page.Append("<script nonce=\"").Append(nonce).Append("\">").Append(PAGE_SCRIPT).Append("</script>");
		_ = page.Append("</body></html>");

		return page.ToString();
	}

	/// <summary>
	///	Converts a brush into a CSS colour.
	/// </summary>
	/// <param name="brush">
	///	The brush to convert; only solid colours are supported.
	/// </param>
	/// <param name="fallback">
	///	The CSS colour used when the brush is missing or not a solid colour.
	/// </param>
	/// <returns>
	///	A CSS <c>rgba()</c> colour.
	/// </returns>
	public static string ToCssColor(Brush? brush, string fallback)
		=>
			brush is SolidColorBrush solid
				? FormattableString.Invariant(
					$"rgba({solid.Color.R}, {solid.Color.G}, {solid.Color.B}, {solid.Color.A / 255.0:0.###})"
				)
				: fallback;
	#endregion PUBLIC

	#region PRIVATE
	/// <summary>
	///	Registers a theme brush property that restyles the page when it changes.
	/// </summary>
	/// <param name="name">
	///	The property name.
	/// </param>
	/// <returns>
	///	The registered property.
	/// </returns>
	private static DependencyProperty RegisterThemeBrush(string name)
		=> DependencyProperty.Register(
			name,
			typeof(Brush),
			typeof(MarkdownDocumentView),
			new PropertyMetadata(null, OnThemeBrushChanged)
		);

	/// <summary>
	///	Shows a newly selected document.
	/// </summary>
	/// <param name="element">
	///	The view whose document changed.
	/// </param>
	/// <param name="e">
	///	The old and new document.
	/// </param>
	private static void OnHtmlChanged(DependencyObject element, DependencyPropertyChangedEventArgs e)
	{
		if (element is MarkdownDocumentView view)
		{
			view._pendingFragment = null;
			view.NavigateToDocument();
		}
	}

	/// <summary>
	///	Scrolls to a requested heading, or remembers it until the current document has finished loading.
	/// </summary>
	/// <param name="element">
	///	The view whose heading request changed.
	/// </param>
	/// <param name="e">
	///	The old and new heading identifier.
	/// </param>
	private static void OnFragmentChanged(DependencyObject element, DependencyPropertyChangedEventArgs e)
	{
		if (element is not MarkdownDocumentView view || e.NewValue is not string fragment || fragment.Length == 0)
		{
			return;
		}

		if (!view._isReady || view._isNavigating)
		{
			view._pendingFragment = fragment;

			return;
		}

		view.ScrollToFragment(fragment);
	}

	/// <summary>
	///	Restyles the page when one of the theme brushes changes, such as when switching between light and dark mode.
	/// </summary>
	/// <param name="element">
	///	The view whose theme changed.
	/// </param>
	/// <param name="e">
	///	The old and new brush.
	/// </param>
	private static void OnThemeBrushChanged(DependencyObject element, DependencyPropertyChangedEventArgs e)
	{
		if (element is MarkdownDocumentView view)
		{
			view.ApplyTheme();
		}
	}

	/// <summary>
	///	Starts the browser when the view is shown, falling back to the plain Markdown text when it cannot start.
	/// </summary>
	/// <param name="sender">
	///	The view being loaded.
	/// </param>
	/// <param name="e">
	///	The routed event data.
	/// </param>
	private async void OnLoaded(object sender, RoutedEventArgs e)
	{
		if (_webView is not null || _isInitializing)
		{
			return;
		}

		_isInitializing = true;

		WebView2 webView = new()
		{
			DefaultBackgroundColor = ToDrawingColor(PageBackground)
		};

		_webView = webView;
		Child    = webView;

		try
		{
			string                  dataDirectory = System.IO.Path.Combine(SecurePathService.GetApplicationDataDirectory(), WEB_VIEW_DATA_DIRECTORY);
			CoreWebView2Environment environment   = await CoreWebView2Environment.CreateAsync(null, dataDirectory);

			await webView.EnsureCoreWebView2Async(environment);

			if (!ReferenceEquals(_webView, webView))
			{
				return;
			}

			CoreWebView2Settings settings = webView.CoreWebView2.Settings;

			settings.AreDevToolsEnabled            = false;
			settings.IsStatusBarEnabled            = false;
			settings.AreDefaultContextMenusEnabled = true;
			settings.AreHostObjectsAllowed         = false;
			settings.IsWebMessageEnabled           = true;
			settings.IsGeneralAutofillEnabled      = false;
			settings.IsPasswordAutosaveEnabled     = false;

			webView.CoreWebView2.WebMessageReceived  += OnWebMessageReceived;
			webView.CoreWebView2.NavigationStarting  += OnNavigationStarting;
			webView.CoreWebView2.NavigationCompleted += OnNavigationCompleted;
			webView.CoreWebView2.NewWindowRequested  += OnNewWindowRequested;

			_isReady = true;
			NavigateToDocument();
		}
		catch (Exception exception)
		{
			if (ReferenceEquals(_webView, webView))
			{
				ShowFallback(exception);
			}
		}
		finally
		{
			_isInitializing = false;
		}
	}

	/// <summary>
	///	Closes the browser when the view is removed, such as when the documentation window closes.
	/// </summary>
	/// <param name="sender">
	///	The view being unloaded.
	/// </param>
	/// <param name="e">
	///	The routed event data.
	/// </param>
	private void OnUnloaded(object sender, RoutedEventArgs e)
	{
		WebView2? webView = _webView;

		_webView      = null;
		_isReady      = false;
		_isNavigating = false;

		if (webView is not null)
		{
			Child = null;
			webView.Dispose();
		}
	}

	/// <summary>
	///	Loads the current document into the browser once it is ready.
	/// </summary>
	private void NavigateToDocument()
	{
		if (!_isReady || _webView?.CoreWebView2 is null)
		{
			return;
		}

		string page = BuildPage(Html ?? string.Empty, BuildThemeCss(), Guid.NewGuid().ToString("N"));

		_isNavigating = true;
		_webView.CoreWebView2.NavigateToString(page);
	}

	/// <summary>
	///	Restyles the loaded page and the browser background with the current theme brushes.
	/// </summary>
	private void ApplyTheme()
	{
		if (_webView is null)
		{
			return;
		}

		_webView.DefaultBackgroundColor = ToDrawingColor(PageBackground);

		if (_isReady && !_isNavigating && _webView.CoreWebView2 is not null)
		{
			_ = _webView.CoreWebView2.ExecuteScriptAsync($"setTheme({JsonSerializer.Serialize(BuildThemeCss())})");
		}
	}

	/// <summary>
	///	Scrolls the loaded page to a heading.
	/// </summary>
	/// <param name="fragment">
	///	The heading identifier.
	/// </param>
	private void ScrollToFragment(string fragment)
	{
		if (_webView?.CoreWebView2 is not null)
		{
			_ = _webView.CoreWebView2.ExecuteScriptAsync($"scrollToFragment({JsonSerializer.Serialize(fragment)})");
		}
	}

	/// <summary>
	///	Builds the CSS variables for the current theme brushes, choosing the browser's light or dark scheme for scroll bars
	///	and form controls from the page background.
	/// </summary>
	/// <returns>
	///	The theme style sheet.
	/// </returns>
	private string BuildThemeCss()
	{
		bool isDark =
			PageBackground is SolidColorBrush background
				&& 0.2126 * background.Color.R + 0.7152 * background.Color.G + 0.0722 * background.Color.B < DARK_LUMINANCE_LIMIT;

		return
			":root { color-scheme: " + (isDark ? "dark" : "light")
			+ "; --bg: " + ToCssColor(PageBackground, "#ffffff")
			+ "; --fg: " + ToCssColor(PageForeground, "#1f2328")
			+ "; --muted: " + ToCssColor(SecondaryForeground, "#59636e")
			+ "; --link: " + ToCssColor(LinkBrush, "#0969da")
			+ "; --border: " + ToCssColor(RuleBrush, "#d1d9e0")
			+ "; --code: " + ToCssColor(CodeBackground, "#f6f8fa")
			+ "; --header: " + ToCssColor(HeaderBackground, "#f6f8fa")
			+ "; --alt: " + ToCssColor(AlternateRowBackground, "#f6f8fa")
			+ "; }";
	}

	/// <summary>
	///	Passes links clicked in the page to <see cref="LinkCommand"/>.
	/// </summary>
	/// <param name="sender">
	///	The browser core.
	/// </param>
	/// <param name="e">
	///	The message sent by the page script, which is the link target.
	/// </param>
	private void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
	{
		string? href = e.TryGetWebMessageAsString();

		if (!string.IsNullOrWhiteSpace(href))
		{
			ExecuteLink(href);
		}
	}

	/// <summary>
	///	Blocks every navigation except loading the document itself; web links that get this far go to
	///	<see cref="LinkCommand"/> instead.
	/// </summary>
	/// <param name="sender">
	///	The browser core.
	/// </param>
	/// <param name="e">
	///	The requested address, and whether to cancel.
	/// </param>
	private void OnNavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
	{
		if (e.Uri.StartsWith("data:", StringComparison.OrdinalIgnoreCase)
			|| e.Uri.StartsWith("about:", StringComparison.OrdinalIgnoreCase))
		{
			return;
		}

		e.Cancel = true;
		ExecuteLink(e.Uri);
	}

	/// <summary>
	///	Applies the theme and any heading requested while the document was loading.
	/// </summary>
	/// <param name="sender">
	///	The browser core.
	/// </param>
	/// <param name="e">
	///	Whether the document loaded.
	/// </param>
	private void OnNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
	{
		_isNavigating = false;

		ApplyTheme();

		string? fragment = _pendingFragment;

		_pendingFragment = null;

		if (e.IsSuccess && !string.IsNullOrEmpty(fragment))
		{
			ScrollToFragment(fragment);
		}
	}

	/// <summary>
	///	Prevents the page from opening new browser windows; web links go to <see cref="LinkCommand"/> instead.
	/// </summary>
	/// <param name="sender">
	///	The browser core.
	/// </param>
	/// <param name="e">
	///	The requested address, and whether the request is handled.
	/// </param>
	private void OnNewWindowRequested(object? sender, CoreWebView2NewWindowRequestedEventArgs e)
	{
		e.Handled = true;
		ExecuteLink(e.Uri);
	}

	/// <summary>
	///	Runs <see cref="LinkCommand"/> for a link target when it can run.
	/// </summary>
	/// <param name="href">
	///	The link target.
	/// </param>
	private void ExecuteLink(string href)
	{
		ICommand? command = LinkCommand;

		if (command?.CanExecute(href) == true)
		{
			command.Execute(href);
		}
	}

	/// <summary>
	///	Replaces the browser with the raw Markdown in a read-only text box, explaining why the formatted page is unavailable.
	/// </summary>
	/// <param name="exception">
	///	The error that stopped the browser from starting.
	/// </param>
	private void ShowFallback(Exception exception)
	{
		WebView2? webView = _webView;

		_webView = null;
		_isReady = false;
		webView?.Dispose();

		TextBlock notice = new()
		{
			Text         = "Formatted documentation needs the Microsoft Edge WebView2 Runtime "
				+ "(https://developer.microsoft.com/microsoft-edge/webview2/). Showing the Markdown text instead. "
				+ $"Reason: {exception.Message}",
			TextWrapping = TextWrapping.Wrap,
			Margin       = new Thickness(0, 0, 0, 8)
		};
		TextBox text = new()
		{
			IsReadOnly                    = true,
			AcceptsReturn                 = true,
			FontFamily                    = new FontFamily("Consolas"),
			FontSize                      = 14,
			TextWrapping                  = TextWrapping.NoWrap,
			HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
			VerticalScrollBarVisibility   = ScrollBarVisibility.Auto
		};
		DockPanel panel = new();

		notice.SetResourceReference(TextBlock.ForegroundProperty, "SecondaryTextBrush");
		_ = text.SetBinding(TextBox.TextProperty, new Binding(nameof(Markdown)) { Source = this, Mode = BindingMode.OneWay });
		DockPanel.SetDock(notice, Dock.Top);
		_ = panel.Children.Add(notice);
		_ = panel.Children.Add(text);
		Child = panel;
	}

	/// <summary>
	///	Converts a brush into the colour the browser paints before the page has loaded, avoiding a white flash in dark mode.
	/// </summary>
	/// <param name="brush">
	///	The page background brush.
	/// </param>
	/// <returns>
	///	The brush colour, or white when the brush is not a solid colour.
	/// </returns>
	private static System.Drawing.Color ToDrawingColor(Brush? brush)
		=>
			brush is SolidColorBrush solid
				? System.Drawing.Color.FromArgb(255, solid.Color.R, solid.Color.G, solid.Color.B)
				: System.Drawing.Color.White;
	#endregion PRIVATE
	#endregion METHODS
}
