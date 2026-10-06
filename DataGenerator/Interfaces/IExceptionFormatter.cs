using DataGenerator.Models;

namespace DataGenerator.Interfaces;

public interface IExceptionFormatter
{
	/// <summary>
	///	Builds a user-facing report containing the problem, where it occurred (data, SQL and code location) and full details.
	/// </summary>
	/// <param name="exception">
	///	The exception to unwrap and describe.
	/// </param>
	/// <returns>
	///	The report shown in the error panel, including summary, location and detailed stack trace text.
	/// </returns>
	/// <exception cref="ArgumentNullException">
	///	Thrown when <paramref name="exception"/> is <see langword="null"/>.
	/// </exception>
	ErrorReport Format(Exception exception);
}