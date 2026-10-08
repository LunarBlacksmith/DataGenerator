using DataGenerator.Interfaces;
using Markdig;
using Markdig.Extensions.AutoIdentifiers;

namespace DataGenerator.Services;

/// <summary>
///	Renders Markdown with Markdig, supporting the GitHub features the bundled guides use: pipe tables, task lists,
///	automatic links, inline HTML such as <c>&lt;kbd&gt;</c> and heading identifiers that match GitHub's.
/// </summary>
public sealed class MarkdigMarkdownRenderer : IMarkdownRenderer
{
	#region FIELDS
	private readonly MarkdownPipeline _pipeline;
	#endregion FIELDS

	#region CONSTRUCTOR
	/// <summary>
	///	Builds the Markdown pipeline once for every document rendered by this instance.
	/// </summary>
	public MarkdigMarkdownRenderer()
	{
		// GitHub identifiers are registered first, so the advanced extensions keep them rather than adding their own.
		_pipeline =
			new MarkdownPipelineBuilder()
				.UseAutoIdentifiers(AutoIdentifierOptions.GitHub)
				.UseAdvancedExtensions()
				.Build();
	}
	#endregion CONSTRUCTOR

	#region METHODS
	/// <summary>
	///	Converts Markdown into an HTML fragment.
	/// </summary>
	/// <param name="markdown">
	///	The Markdown text to convert.
	/// </param>
	/// <returns>
	///	The sanitized HTML body content.
	/// </returns>
	/// <exception cref="ArgumentNullException">
	///	Thrown when <paramref name="markdown"/> is <see langword="null"/>.
	/// </exception>
	public string RenderHtml(string markdown)
	{
		ArgumentNullException.ThrowIfNull(markdown);

		return MarkdownHtmlSanitizer.Sanitize(Markdown.ToHtml(markdown, _pipeline));
	}
	#endregion METHODS
}
