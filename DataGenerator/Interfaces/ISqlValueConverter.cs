using System.Data;
using DataGenerator.Models;

namespace DataGenerator.Interfaces;

/// <summary>
///	Converts between user text, CLR values and SQL Server literals for a specific column type.
/// </summary>
public interface ISqlValueConverter
{
	/// <summary>
	///	Classifies the SQL Server type of a column into the converter category used by the generator.
	/// </summary>
	/// <param name="column">
	///	The column whose SQL type is classified.
	/// </param>
	/// <returns>
	///	The matching type category, or <see cref="SqlTypeCategory.Unsupported"/> when the type is not recognised.
	/// </returns>
	/// <exception cref="ArgumentNullException">
	///	Thrown when <paramref name="column"/> is <see langword="null"/>.
	/// </exception>
	SqlTypeCategory GetCategory(ColumnModel column);

	/// <summary>
	///	Converts user-entered text to a CLR value suitable for the column. Throws <see cref="FormatException"/>
	///	or <see cref="OverflowException"/> with a user-friendly message when the text is not valid for the column.
	/// </summary>
	/// <param name="column">
	///	The column that will store the value.
	/// </param>
	/// <param name="text">
	///	The user-entered value text.
	/// </param>
	/// <returns>
	///	The CLR value to send to SQL Server.
	/// </returns>
	/// <exception cref="ArgumentNullException">
	///	Thrown when <paramref name="column"/> or <paramref name="text"/> is <see langword="null"/>.
	/// </exception>
	/// <exception cref="FormatException">
	///	Thrown when the text is not valid for the column type or length.
	/// </exception>
	/// <exception cref="OverflowException">
	///	Thrown when the text is outside the numeric or date range accepted by the column.
	/// </exception>
	/// <exception cref="InvalidOperationException">
	///	Thrown when a value is requested for a rowversion column.
	/// </exception>
	object ConvertText(ColumnModel column, string text);

	/// <summary>
	///	Tries to convert user-entered text to a CLR value without throwing validation errors.
	/// </summary>
	/// <param name="column">
	///	The column that will store the value.
	/// </param>
	/// <param name="text">
	///	The user-entered value text.
	/// </param>
	/// <param name="value">
	///	The converted CLR value when conversion succeeds; otherwise <see langword="null"/>.
	/// </param>
	/// <param name="errorMessage">
	///	Empty text when conversion succeeds; otherwise the validation error message.
	/// </param>
	/// <returns>
	///	<see langword="true"/> when the text converts successfully; otherwise <see langword="false"/>.
	/// </returns>
	bool TryConvertText(ColumnModel column, string text, out object? value, out string errorMessage);

	/// <summary>
	///	Converts a numeric sequence value to the CLR type required by a column.
	/// </summary>
	/// <param name="column">
	///	The column that will store the sequence value.
	/// </param>
	/// <param name="value">
	///	The numeric sequence value before type conversion.
	/// </param>
	/// <returns>
	///	The CLR value to send to SQL Server.
	/// </returns>
	/// <exception cref="ArgumentNullException">
	///	Thrown when <paramref name="column"/> is <see langword="null"/>.
	/// </exception>
	/// <exception cref="FormatException">
	///	Thrown when the value has decimal places or scale that the target column cannot store.
	/// </exception>
	/// <exception cref="OverflowException">
	///	Thrown when the value is outside the range accepted by the column.
	/// </exception>
	/// <exception cref="InvalidOperationException">
	///	Thrown when sequences are not supported for the column type.
	/// </exception>
	object ConvertSequenceValue(ColumnModel column, decimal value);

	/// <summary>
	///	Converts text produced by a regex or pattern rule. Text columns are never silently truncated.
	/// </summary>
	/// <param name="column">
	///	The column that will store the generated text.
	/// </param>
	/// <param name="text">
	///	The generated text.
	/// </param>
	/// <returns>
	///	The CLR value to send to SQL Server.
	/// </returns>
	/// <exception cref="ArgumentNullException">
	///	Thrown when <paramref name="column"/> or <paramref name="text"/> is <see langword="null"/>.
	/// </exception>
	/// <exception cref="FormatException">
	///	Thrown when the generated text cannot be stored in the target column.
	/// </exception>
	object ConvertGeneratedText(ColumnModel column, string text);

	/// <summary>
	///	Formats a CLR value as a SQL literal for the target column.
	/// </summary>
	/// <param name="column">
	///	The column whose SQL type controls literal formatting.
	/// </param>
	/// <param name="value">
	///	The CLR value to format. <see langword="null"/> and <see cref="DBNull"/> become <c>NULL</c>.
	/// </param>
	/// <returns>
	///	A SQL literal or conversion expression suitable for the generated script.
	/// </returns>
	/// <exception cref="ArgumentNullException">
	///	Thrown when <paramref name="column"/> is <see langword="null"/>.
	/// </exception>
	string ToSqlLiteral(ColumnModel column, object? value);

	/// <summary>
	///	Formats a CLR value for display in the UI.
	/// </summary>
	/// <param name="column">
	///	The column whose SQL type controls display formatting.
	/// </param>
	/// <param name="value">
	///	The CLR value to display. <see langword="null"/> and <see cref="DBNull"/> display as <c>NULL</c>.
	/// </param>
	/// <returns>
	///	The display text, or empty text when the value's string representation is <see langword="null"/>.
	/// </returns>
	/// <exception cref="ArgumentNullException">
	///	Thrown when <paramref name="column"/> is <see langword="null"/>.
	/// </exception>
	string FormatForDisplay(ColumnModel column, object? value);

	/// <summary>
	///	Type declaration usable for a T-SQL variable or table variable column, e.g. nvarchar(50).
	/// </summary>
	/// <param name="column">
	///	The column whose SQL type is formatted.
	/// </param>
	/// <returns>
	///	A SQL Server type declaration suitable for generated T-SQL.
	/// </returns>
	/// <exception cref="ArgumentNullException">
	///	Thrown when <paramref name="column"/> is <see langword="null"/>.
	/// </exception>
	string GetTypeDeclaration(ColumnModel column);

	/// <summary>
	///	Type as shown to the user, e.g. nvarchar(50), decimal(18, 2) or ntext.
	/// </summary>
	/// <param name="column">
	///	The column whose SQL type is formatted.
	/// </param>
	/// <returns>
	///	The display type text.
	/// </returns>
	/// <exception cref="ArgumentNullException">
	///	Thrown when <paramref name="column"/> is <see langword="null"/>.
	/// </exception>
	string GetDisplayType(ColumnModel column);

	/// <summary>
	///	Gets the maximum number of characters accepted by a text column.
	/// </summary>
	/// <param name="column">
	///	The column whose length metadata is inspected.
	/// </param>
	/// <returns>
	///	The maximum character count for bounded text and sysname columns, or <see langword="null"/> for unbounded or
	///	non-text columns.
	/// </returns>
	/// <exception cref="ArgumentNullException">
	///	Thrown when <paramref name="column"/> is <see langword="null"/>.
	/// </exception>
	int? GetMaximumTextLength(ColumnModel column);

	/// <summary>
	///	Maps a column's SQL Server type to a <see cref="SqlDbType"/> for parameters.
	/// </summary>
	/// <param name="column">
	///	The column whose SQL type is mapped.
	/// </param>
	/// <returns>
	///	The matching <see cref="SqlDbType"/>, or <see cref="SqlDbType.NVarChar"/> for unrecognised types.
	/// </returns>
	/// <exception cref="ArgumentNullException">
	///	Thrown when <paramref name="column"/> is <see langword="null"/>.
	/// </exception>
	SqlDbType GetSqlDbType(ColumnModel column);

	/// <summary>
	///	Describes the value format a user can enter for a column.
	/// </summary>
	/// <param name="column">
	///	The column whose accepted input is described.
	/// </param>
	/// <returns>
	///	A user-facing description of the accepted input, including ranges or lengths where known.
	/// </returns>
	/// <exception cref="ArgumentNullException">
	///	Thrown when <paramref name="column"/> is <see langword="null"/>.
	/// </exception>
	string DescribeAcceptedInput(ColumnModel column);
}