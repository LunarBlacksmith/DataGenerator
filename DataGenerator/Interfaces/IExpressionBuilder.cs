using DataGenerator.Models;

namespace DataGenerator.Interfaces;

public interface IExpressionBuilder
{
	/// <summary>
	///	Builds whole-value expressions from explicit bracketed segments; invalid syntax raises FormatException.
	/// </summary>
	BuiltExpression Build(string feed, string columnName);
}
