using System.IO;
using DataGenerator.Interfaces;
using DataGenerator.Models;

namespace DataGenerator.Services;

public sealed class JsonExpressionLibraryStore : IExpressionLibraryStore
{
	#region FIELDS
	private readonly string _filePath;
	#endregion FIELDS

	#region CONSTRUCTOR
	public JsonExpressionLibraryStore(string filePath)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
		_filePath = filePath;
	}
	#endregion CONSTRUCTOR

	#region METHODS
	#region PUBLIC
	/// <summary>
	///	Reads a versioned library and rejects incomplete entries without changing the file.
	/// </summary>
	public IReadOnlyList<BuiltExpression> Load()
	{
		if (!File.Exists(_filePath))
		{
			return [];
		}

		ExpressionLibraryDocument? document = JsonDocumentFile.Read<ExpressionLibraryDocument>(_filePath, "expression library");

		if (document is null || document.FormatVersion != 1 || document.Expressions is null)
		{
			throw new InvalidDataException("The expression library is invalid or uses an unsupported format. The file has not been changed.");
		}

		Validate(document.Expressions);
		return document.Expressions;
	}

	/// <summary>
	///	Validates all entries before atomically replacing the library.
	/// </summary>
	public void                              Save(IReadOnlyList<BuiltExpression> expressions)
	{
		ArgumentNullException.ThrowIfNull(expressions);
		Validate(expressions);
		JsonDocumentFile.Write(_filePath, new ExpressionLibraryDocument { FormatVersion = 1, Expressions = expressions.ToList() });
	}
	#endregion PUBLIC

	#region PRIVATE
	private static void Validate(IReadOnlyList<BuiltExpression> expressions)
	{
		if (expressions.Any(entry => entry is null || string.IsNullOrWhiteSpace(entry.Name)
				|| string.IsNullOrWhiteSpace(entry.Feed) || string.IsNullOrEmpty(entry.ColumnName)
				|| string.IsNullOrEmpty(entry.Pattern) || string.IsNullOrEmpty(entry.Regex)
				|| string.IsNullOrEmpty(entry.Sql) || string.IsNullOrEmpty(entry.Description))
			|| expressions.Select(entry => entry.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() != expressions.Count)
		{
			throw new InvalidDataException("The expression library contains an incomplete entry or duplicate name. The file has not been changed.");
		}
	}
	#endregion PRIVATE
	#endregion METHODS

	#region TYPES
	private sealed class ExpressionLibraryDocument
	{
		#region PROPERTIES
		public int FormatVersion { get; set; }
		public List<BuiltExpression>? Expressions { get; set; }
		#endregion PROPERTIES

		#region CONSTRUCTOR
		public ExpressionLibraryDocument()
		{
			FormatVersion = 0;
			Expressions = null;
		}
		#endregion CONSTRUCTOR
	}
	#endregion TYPES
}
