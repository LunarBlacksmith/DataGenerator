using System.Data;
using DataGenerator.Models;

namespace DataGenerator.Interfaces;

/// <summary>
/// Converts between user text, CLR values and SQL Server literals for a specific column type.
/// </summary>
public interface ISqlValueConverter
{
	SqlTypeCategory GetCategory(ColumnModel column);

	/// <summary>
	/// Converts user-entered text to a CLR value suitable for the column. Throws <see cref="FormatException"/>
	/// or <see cref="OverflowException"/> with a user-friendly message when the text is not valid for the column.
	/// </summary>
	object ConvertText(ColumnModel column, string text);

	bool TryConvertText(ColumnModel column, string text, out object? value, out string errorMessage);

	object ConvertSequenceValue(ColumnModel column, decimal value);

	/// <summary>
	/// Converts text produced by a regex or pattern rule. Text columns are never silently truncated.
	/// </summary>
	object ConvertGeneratedText(ColumnModel column, string text);

	string ToSqlLiteral(ColumnModel column, object? value);

	string FormatForDisplay(ColumnModel column, object? value);

	/// <summary>
	/// Type declaration usable for a T-SQL variable or table variable column, e.g. nvarchar(50).
	/// </summary>
	string GetTypeDeclaration(ColumnModel column);

	/// <summary>
	/// Type as shown to the user, e.g. nvarchar(50), decimal(18, 2) or ntext.
	/// </summary>
	string GetDisplayType(ColumnModel column);

	int? GetMaximumTextLength(ColumnModel column);

	SqlDbType GetSqlDbType(ColumnModel column);

	string DescribeAcceptedInput(ColumnModel column);
}