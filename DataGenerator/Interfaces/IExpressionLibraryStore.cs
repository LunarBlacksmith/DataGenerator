using DataGenerator.Models;

namespace DataGenerator.Interfaces;

public interface IExpressionLibraryStore
{
	/// <summary>
	///	Loads saved feed text and all outputs, or an empty list when the library does not exist.
	/// </summary>
	IReadOnlyList<BuiltExpression> Load();

	/// <summary>
	///	Writes the complete reusable expression library through an atomic file replacement.
	/// </summary>
	void Save(IReadOnlyList<BuiltExpression> expressions);
}
