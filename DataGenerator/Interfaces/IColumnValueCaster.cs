using DataGenerator.Models;

namespace DataGenerator.Interfaces;

/// <summary>
///	Converts the value of one column into a value that another column can store, without failing on type differences,
///	e.g. 'AB12C3' copied into an int column becomes 123 and text without any digits becomes 0.
/// </summary>
public interface IColumnValueCaster
{
	/// <summary>
	///	Converts <paramref name="value"/> for <paramref name="target"/>. Values that cannot be converted become the type's
	///	default (0, empty text, 1900-01-01 and so on), or NULL when the target allows NULL and the value is NULL.
	/// </summary>
	/// <param name="target">
	///	The column that will receive the value.
	/// </param>
	/// <param name="value">
	///	The source value to convert. <see cref="DBNull"/> is treated as <see langword="null"/>.
	/// </param>
	/// <returns>
	///	The converted CLR value, a <see cref="SqlFragment"/> when the source is a SQL fragment, or <see langword="null"/>
	///	when the target is nullable and the source value is null.
	/// </returns>
	/// <exception cref="ArgumentNullException">
	///	Thrown when <paramref name="target"/> is <see langword="null"/>.
	/// </exception>
	/// <exception cref="InvalidOperationException">
	///	Thrown when values cannot be copied into the target column type.
	/// </exception>
	object? Cast(ColumnModel target, object? value);

	/// <summary>
	///	The value as text, as used by COL(...) in patterns.
	/// </summary>
	/// <param name="value">
	///	The value to format. <see langword="null"/> and <see cref="DBNull"/> become empty text.
	/// </param>
	/// <returns>
	///	The value formatted with invariant culture, or empty text for null values.
	/// </returns>
	/// <exception cref="InvalidOperationException">
	///	Thrown when <paramref name="value"/> is a <see cref="SqlFragment"/> that can only be resolved by SQL Server.
	/// </exception>
	string ToText(object? value);
}