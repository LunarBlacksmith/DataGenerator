using DataGenerator.Models;

namespace DataGenerator.Interfaces;

public interface IExceptionFormatter
{
	/// <summary>
	/// Builds a user-facing report containing the problem, where it occurred (data, SQL and code location) and full details.
	/// </summary>
	ErrorReport Format(Exception exception);
}