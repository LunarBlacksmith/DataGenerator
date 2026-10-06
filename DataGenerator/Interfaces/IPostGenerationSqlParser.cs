using DataGenerator.Models;

namespace DataGenerator.Interfaces;

public interface IPostGenerationSqlParser
{
	/// <summary>
	/// Splits the post-generation text into statements. A line holding only a (schema-qualified) name becomes
	/// <c>EXEC name;</c>; anything else is run as typed. Returns false with a message that names the line when the text
	/// cannot be run inside the generation transaction.
	/// </summary>
	bool TryParse(string? text, out IReadOnlyList<PostGenerationStatement> statements, out string? error);
}