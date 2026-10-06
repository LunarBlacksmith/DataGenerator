using DataGenerator.Models;

namespace DataGenerator.Interfaces;

/// <summary>
/// Converts the value of one column into a value that another column can store, without failing on type differences,
/// e.g. 'AB12C3' copied into an int column becomes 123 and text without any digits becomes 0.
/// </summary>
public interface IColumnValueCaster
{
	/// <summary>
	/// Converts <paramref name="value"/> for <paramref name="target"/>. Values that cannot be converted become the type's
	/// default (0, empty text, 1900-01-01 and so on), or NULL when the target allows NULL and the value is NULL.
	/// </summary>
	object? Cast(ColumnModel target, object? value);

	/// <summary>
	/// The value as text, as used by COL(...) in patterns.
	/// </summary>
	string ToText(object? value);
}