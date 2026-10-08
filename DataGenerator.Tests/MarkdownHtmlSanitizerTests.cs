using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using DataGenerator.Infrastructure;
using DataGenerator.Services;
using Xunit;

namespace DataGenerator.Tests;

public sealed class MarkdownHtmlSanitizerTests
{
	#region CONSTRUCTOR
	public MarkdownHtmlSanitizerTests()
	{
	}
	#endregion CONSTRUCTOR

	#region METHODS
	#region PUBLIC
	[Fact]
	public void RenderHtmlPreservesDocumentationFormatting()
	{
		MarkdigMarkdownRenderer renderer = new();
		string html = renderer.RenderHtml(
			"""
			# Keyboard shortcuts

			Press <kbd>Ctrl</kbd> and **Enter**. See [guide](Docs/PatternLanguage.md#functions).

			| Name | Value |
			| ---- | ----- |
			| key  | code  |

			- [x] Completed
			- [ ] Pending

			```sql
			SELECT '<script>alert(1)</script>';
			```
			"""
		);
		IDocument document = Parse(html);

		Assert.Equal("Keyboard shortcuts", document.QuerySelector("h1#keyboard-shortcuts")?.TextContent);
		Assert.Equal("Ctrl", document.QuerySelector("kbd")?.TextContent);
		Assert.Equal("Enter", document.QuerySelector("strong")?.TextContent);
		Assert.Equal("Docs/PatternLanguage.md#functions", document.QuerySelector("a")?.GetAttribute("href"));
		Assert.Equal(2, document.QuerySelectorAll("table tr").Length);
		Assert.NotNull(document.QuerySelector("input[type=checkbox][disabled][checked]"));
		Assert.Equal(2, document.QuerySelectorAll("input[type=checkbox][disabled]").Length);
		Assert.Single(document.QuerySelectorAll("input[type=checkbox][disabled]:not([checked])"));
		Assert.Contains("<script>alert(1)</script>", document.QuerySelector("pre code")?.TextContent);
		Assert.Empty(document.QuerySelectorAll("script"));
	}

	[Fact]
	public void RenderHtmlSanitizesConvertedMarkdownAndRawHtml()
	{
		MarkdigMarkdownRenderer renderer = new();
		string html = renderer.RenderHtml(
			"""
			# Safe heading

			<script>alert(1)</script>
			<style>body { background: url(https://attacker.invalid/tracker); }</style>
			<p onclick="alert(1)" style="background:url(https://attacker.invalid/tracker)">Safe <kbd onmouseover="alert(2)">Ctrl</kbd></p>

			[unsafe](javascript:alert%281%29)
			![tracker](https://attacker.invalid/tracker)
			"""
		);
		IDocument document = Parse(html);

		Assert.Equal("Safe heading", document.QuerySelector("h1")?.TextContent);
		Assert.Equal("Ctrl", document.QuerySelector("kbd")?.TextContent);
		Assert.Empty(document.QuerySelectorAll("script, style, img, [onclick], [onmouseover], [style], [src]"));
		Assert.Null(document.QuerySelector("a")?.GetAttribute("href"));
	}

	[Theory]
	[InlineData("javascript:alert(1)")]
	[InlineData("JaVaScRiPt:alert(1)")]
	[InlineData("jav&#x61;script:alert(1)")]
	[InlineData("java&#x09;script:alert(1)")]
	[InlineData("&#x0A;javascript:alert(1)")]
	[InlineData("vbscript:msgbox(1)")]
	[InlineData("data:text/html;base64,PHNjcmlwdD4=")]
	[InlineData("file:///C:/Windows/win.ini")]
	public void SanitizeRemovesUnsafeLinkSchemes(string href)
	{
		IDocument document = Parse(MarkdownHtmlSanitizer.Sanitize($"<a href=\"{href}\">link</a>"));

		Assert.Equal("link", document.QuerySelector("a")?.TextContent);
		Assert.Null(document.QuerySelector("a")?.GetAttribute("href"));
	}

	[Theory]
	[InlineData("#heading")]
	[InlineData("Docs/PatternLanguage.md#functions")]
	[InlineData("../README.md")]
	[InlineData("https://example.com/guide")]
	[InlineData("http://example.com/guide")]
	[InlineData("mailto:reader@example.com")]
	public void SanitizePreservesSafeLinkTargets(string href)
	{
		IDocument document = Parse(MarkdownHtmlSanitizer.Sanitize($"<a href=\"{href}\">link</a>"));

		Assert.Equal(href, document.QuerySelector("a")?.GetAttribute("href"));
	}

	[Fact]
	public void SanitizeRemovesEmbeddedResourcesAndActiveMarkup()
	{
		string html = MarkdownHtmlSanitizer.Sanitize(
			"""
			<link rel="stylesheet" href="https://attacker.invalid/style">
			<meta http-equiv="refresh" content="0;url=https://attacker.invalid">
			<iframe srcdoc="<script>alert(1)</script>"></iframe>
			<object data="https://attacker.invalid/object"></object>
			<embed src="https://attacker.invalid/embed">
			<svg onload="alert(1)"><a href="javascript:alert(1)">SVG</a></svg>
			<math><mtext>math</mtext></math>
			<video poster="https://attacker.invalid/poster"><source src="https://attacker.invalid/video"></video>
			<img src="data:image/svg+xml,%3Csvg%20onload='alert(1)'%3E" srcset="https://attacker.invalid/tracker 2x">
			<div style="background:url(https://attacker.invalid/tracker)" background="https://attacker.invalid/tracker" onpointerover="alert(1)" data-url="https://attacker.invalid">Safe</div>
			<input type="checkbox" disabled checked src="https://attacker.invalid/tracker" formaction="https://attacker.invalid" autofocus onfocus="alert(1)">
			"""
		);
		IDocument document = Parse(html);

		Assert.Empty(document.QuerySelectorAll("link, meta, iframe, object, embed, svg, math, video, source, img"));
		Assert.Empty(document.QuerySelectorAll("[style], [background], [src], [srcset], [poster], [data], [data-url], [srcdoc], [formaction], [autofocus], [onpointerover], [onfocus]"));
		Assert.Equal("Safe", document.QuerySelector("div")?.TextContent);
		Assert.NotNull(document.QuerySelector("input[type=checkbox][disabled][checked]"));
	}

	[Theory]
	[InlineData("<input>")]
	[InlineData("<input disabled>")]
	[InlineData("<input type=\"text\" disabled>")]
	[InlineData("<input type=\"password\" disabled>")]
	[InlineData("<input type=\"submit\" disabled>")]
	[InlineData("<input type=\"radio\" disabled>")]
	[InlineData("<input type=\"image\" disabled>")]
	[InlineData("<input type=\"hidden\" disabled>")]
	[InlineData("<input type=\"checkbox\">")]
	[InlineData("<input type=\"checkbox\" checked>")]
	public void SanitizeRemovesNonCheckboxAndEnabledInputs(string inputHtml)
	{
		IDocument document = Parse(MarkdownHtmlSanitizer.Sanitize($"<p>Before{inputHtml}After</p>"));

		Assert.Empty(document.QuerySelectorAll("input"));
		Assert.Equal("BeforeAfter", document.QuerySelector("p")?.TextContent);
	}

	[Theory]
	[InlineData("<input type=\"checkbox\" disabled>")]
	[InlineData("<input type=\"CHECKBOX\" disabled=\"false\" checked>")]
	public void SanitizePreservesOnlyDisabledCheckboxInputs(string inputHtml)
	{
		string html = MarkdownHtmlSanitizer.Sanitize(inputHtml);
		IDocument document = Parse(html);
		IElement input = Assert.Single(document.QuerySelectorAll("input"));

		Assert.Equal("checkbox", input.GetAttribute("type"), ignoreCase: true);
		Assert.True(input.HasAttribute("disabled"));
		Assert.Equal(html, MarkdownHtmlSanitizer.Sanitize(html));
	}

	[Fact]
	public void BuildPageSanitizesDirectHtmlAndPreservesTrustedPageProtections()
	{
		string page = MarkdownDocumentView.BuildPage(
			"<kbd onclick=\"alert(1)\">Ctrl</kbd><input type=\"checkbox\"><input type=\"password\" disabled></body><script nonce=\"test-nonce\">alert(1)</script><img src=\"https://attacker.invalid/tracker\">",
			":root { --bg: #ffffff; }",
			"test-nonce"
		);
		IDocument document = Parse(page);
		IElement? policy = document.QuerySelector("meta[http-equiv=Content-Security-Policy]");

		Assert.Equal("Ctrl", document.QuerySelector("kbd")?.TextContent);
		Assert.Empty(document.QuerySelectorAll("[onclick], img, input"));
		IElement script = Assert.Single(document.QuerySelectorAll("script"));
		Assert.Equal("test-nonce", script.GetAttribute("nonce"));
		Assert.Contains("window.chrome.webview.postMessage", script.TextContent);
		Assert.DoesNotContain("alert(1)", script.TextContent);
		Assert.Contains("default-src 'none'", policy?.GetAttribute("content"));
		Assert.Contains("script-src 'nonce-test-nonce'", policy?.GetAttribute("content"));
		Assert.Equal(2, document.QuerySelectorAll("style").Length);
		Assert.Equal(":root { --bg: #ffffff; }", document.QuerySelector("style#theme")?.TextContent);
	}

	[Fact]
	public void SanitizeIsIdempotentAndRejectsNull()
	{
		string sanitized = MarkdownHtmlSanitizer.Sanitize("<h2 id=\"heading\">Safe <kbd>Ctrl</kbd></h2><script>alert(1)</script>");

		Assert.Equal(sanitized, MarkdownHtmlSanitizer.Sanitize(sanitized));
		Assert.Equal(string.Empty, MarkdownHtmlSanitizer.Sanitize(string.Empty));
		Assert.Throws<ArgumentNullException>(() => MarkdownHtmlSanitizer.Sanitize(null!));
	}

	#endregion PUBLIC

	#region PRIVATE
	private static IDocument Parse(string html)
	{
		HtmlParser parser = new();

		return parser.ParseDocument(html);
	}
	#endregion PRIVATE
	#endregion METHODS
}
