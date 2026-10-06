using DataGenerator.Models;

namespace DataGenerator.Interfaces;

public interface IPostGenerationSqlParser
{
	/// <summary>
	///	Splits the post-generation text into statements. A line holding only a (schema-qualified) name becomes
	///	<c>EXEC name;</c>; anything else is run as typed. Returns false with a message that names the line when the text
	///	cannot be run inside the generation transaction.
	/// </summary>
	/// <param name="text">
	///	The post-generation text to parse. <see langword="null"/> is treated as empty text.
	/// </param>
	/// <param name="statements">
	///	The parsed statements. This is an empty list when there is no executable SQL or parsing fails.
	/// </param>
	/// <param name="error">
	///	<see langword="null"/> when parsing succeeds; otherwise a user-facing error message.
	/// </param>
	/// <returns>
	///	<see langword="true"/> when all text can run inside the generation transaction; otherwise
	///	<see langword="false"/>.
	/// </returns>
	bool TryParse(string? text, out IReadOnlyList<PostGenerationStatement> statements, out string? error);
}