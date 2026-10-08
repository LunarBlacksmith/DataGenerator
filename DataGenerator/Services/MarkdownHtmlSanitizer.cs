using AngleSharp.Dom;
using Ganss.Xss;

namespace DataGenerator.Services;

/// <summary>
///	Restricts documentation fragments to inert formatting and links, without styles or resource-loading attributes.
/// </summary>
public static class MarkdownHtmlSanitizer
{
	#region CONSTRUCTOR
	/// <summary>
	///	Initializes the stateless sanitizer type.
	/// </summary>
	static MarkdownHtmlSanitizer()
	{
	}
	#endregion CONSTRUCTOR

	#region METHODS
	/// <summary>
	///	Sanitizes a converted Markdown fragment or HTML supplied directly to the documentation view.
	/// </summary>
	/// <param name="html">
	///	The untrusted HTML fragment.
	/// </param>
	/// <returns>
	///	Allowlisted HTML suitable for insertion into the documentation page.
	/// </returns>
	public static string Sanitize(string html)
	{
		ArgumentNullException.ThrowIfNull(html);

		HtmlSanitizer sanitizer = new();

		sanitizer.AllowedTags.Clear();
		sanitizer.AllowedTags.UnionWith(
			[
				"a", "abbr", "b", "blockquote", "br", "caption", "code", "dd", "del", "details", "div", "dl", "dt",
				"em", "h1", "h2", "h3", "h4", "h5", "h6", "hr", "i", "input", "ins", "kbd", "li", "mark",
				"ol", "p", "pre", "s", "samp", "small", "span", "strong", "sub", "summary", "sup", "table",
				"tbody", "td", "th", "thead", "tfoot", "tr", "ul"
			]
		);
		sanitizer.AllowedAttributes.Clear();
		sanitizer.AllowedAttributes.UnionWith(
			[
				"align", "checked", "class", "colspan", "disabled", "href", "id", "reversed", "rowspan", "scope",
				"start", "title", "type"
			]
		);
		sanitizer.AllowedSchemes.Clear();
		sanitizer.AllowedSchemes.UnionWith(["http", "https", "mailto"]);
		sanitizer.AllowedCssProperties.Clear();
		sanitizer.AllowDataAttributes = false;
		sanitizer.KeepChildNodes = false;
		sanitizer.PostProcessDom += RemoveInteractiveInputs;

		return sanitizer.Sanitize(html);
	}

	/// <summary>
	///	Retains only disabled checkbox inputs used to display task-list state.
	/// </summary>
	/// <param name="sender">
	///	The sanitizer.
	/// </param>
	/// <param name="e">
	///	The sanitized document before serialization.
	/// </param>
	private static void RemoveInteractiveInputs(object? sender, PostProcessDomEventArgs e)
	{
		foreach (IElement input in e.Document.QuerySelectorAll("input"))
		{
			if (!string.Equals(input.GetAttribute("type"), "checkbox", StringComparison.OrdinalIgnoreCase)
				|| !input.HasAttribute("disabled"))
			{
				input.Remove();
			}
		}
	}
	#endregion METHODS
}
