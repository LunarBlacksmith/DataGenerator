namespace DataGenerator.Interfaces;

/// <summary>
///	Converts Markdown documentation into HTML for display.
/// </summary>
public interface IMarkdownRenderer
{
	/// <summary>
	///	Converts Markdown into an HTML fragment, giving every heading a GitHub-style identifier so links such as
	///	<c>#writing-sql-conditions</c> can find it.
	/// </summary>
	/// <param name="markdown">
	///	The Markdown text to convert.
	/// </param>
	/// <returns>
	///	The HTML body content, without a surrounding document or styles.
	/// </returns>
	/// <exception cref="ArgumentNullException">
	///	Thrown when <paramref name="markdown"/> is <see langword="null"/>.
	/// </exception>
	string RenderHtml(string markdown);
}
