namespace DataGenerator.Interfaces;

public interface IRegexValueGenerator
{
	/// <summary>
	///	Generates sample text from the supported subset of regular expressions.
	/// </summary>
	/// <param name="pattern">
	///	The regex pattern to generate from. Leading ^ and trailing $ anchors are ignored.
	/// </param>
	/// <param name="maximumLength">
	///	The maximum number of characters to return.
	/// </param>
	/// <returns>
	///	Random text that follows the supported parts of the pattern, truncated at <paramref name="maximumLength"/>.
	/// </returns>
	/// <exception cref="ArgumentException">
	///	Thrown when <paramref name="pattern"/> is <see langword="null"/>, empty or whitespace.
	/// </exception>
	/// <exception cref="ArgumentOutOfRangeException">
	///	Thrown when <paramref name="maximumLength"/> is less than one.
	/// </exception>
	/// <exception cref="InvalidOperationException">
	///	Thrown when the pattern contains malformed character classes, escapes or repetition blocks.
	/// </exception>
	/// <exception cref="NotSupportedException">
	///	Thrown when the pattern uses a regex construct that the built-in generator does not support.
	/// </exception>
	string Generate(string pattern, int maximumLength);
}