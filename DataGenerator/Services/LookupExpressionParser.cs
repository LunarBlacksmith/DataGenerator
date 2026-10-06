using System.Text;
using System.Text.RegularExpressions;
using DataGenerator.Interfaces;
using DataGenerator.Models;

namespace DataGenerator.Services;

public sealed class LookupExpressionParser : ILookupExpressionParser
{
	private const string KEYWORD_UNIQUE    = "UNIQUE";
	private const string KEYWORD_FROM      = "FROM";
	private const string KEYWORD_WHERE     = "WHERE";
	private const string KEYWORD_REGEX     = "REGEX";
	private const string KEYWORD_SQL       = "SQL";
	private const string DEFAULT_SCHEMA    = "dbo";
	private const string EXAMPLE           = "dbo.Shirt.ShirtID UNIQUE";

	private static readonly TimeSpan REGEX_CHECK_TIMEOUT = TimeSpan.FromSeconds(1);

	private readonly ITableCatalog         _catalog;
	private readonly IPatternSqlTranslator _patternTranslator;

	/// <summary>
	///	Creates a parser that resolves lookup expressions against the loaded table catalog and validates pattern filters.
	/// </summary>
	/// <param name="catalog">
	///	The catalog used to find source tables named by lookup expressions.
	/// </param>
	/// <param name="patternTranslator">
	///	The translator used to validate pattern-based WHERE filters.
	/// </param>
	/// <exception cref="ArgumentNullException">
	///	Thrown when <paramref name="catalog"/> or <paramref name="patternTranslator"/> is <see langword="null"/>.
	/// </exception>
	public LookupExpressionParser(ITableCatalog catalog, IPatternSqlTranslator patternTranslator)
	{
		_catalog           = catalog ?? throw new ArgumentNullException(nameof(catalog));
		_patternTranslator = patternTranslator ?? throw new ArgumentNullException(nameof(patternTranslator));
	}

	/// <summary>
	///	Parses a lookup expression into the source table, source column, uniqueness, row scope and optional filter used
	///	when a column copies values from another table.
	/// </summary>
	/// <param name="expression">
	///	The expression typed by the user, such as <c>dbo.Shirt.ShirtID UNIQUE</c>.
	/// </param>
	/// <param name="targetTable">
	///	The table whose column owns the lookup, used to resolve omitted database and schema names.
	/// </param>
	/// <param name="lookup">
	///	The parsed lookup when parsing succeeds; otherwise <see langword="null"/>.
	/// </param>
	/// <param name="errorMessage">
	///	An empty string when parsing succeeds; otherwise the user-facing reason the expression is invalid.
	/// </param>
	/// <returns>
	///	<see langword="true"/> when <paramref name="expression"/> names a valid source column and options; otherwise
	///	<see langword="false"/>.
	/// </returns>
	/// <exception cref="ArgumentNullException">
	///	Thrown when <paramref name="targetTable"/> is <see langword="null"/>.
	/// </exception>
	public bool TryParse(string expression, TableModel targetTable, out ColumnLookup? lookup, out string errorMessage)
	{
		ArgumentNullException.ThrowIfNull(targetTable);

		lookup = null;

		string text = expression?.Trim() ?? string.Empty;

		if (text.Length == 0)
		{
			errorMessage = $"Enter the column whose values are used, e.g. {EXAMPLE}. Click ▾ to pick it from a list.";
			return false;
		}

		int position = 0;

		if (	!TryReadNameParts(text, ref position, out List<string> parts, out errorMessage)
				|| !TryResolveColumn(parts, targetTable, out TableModel? sourceTable, out ColumnModel? sourceColumn, out errorMessage)
				|| !TryReadOptions(text, ref position, out bool isUnique, out RowScope scope, out LookupFilterKind filterKind, out string filterText, out errorMessage)
				|| !TryValidateFilter(filterKind, filterText, out errorMessage)
		)
		{
			return false;
		}

		lookup = new ColumnLookup
		{
			SourceTable  = sourceTable!,
			SourceColumn = sourceColumn!,
			IsUnique     = isUnique,
			Scope        = scope,
			FilterKind   = filterKind,
			FilterText   = filterText
		};
		errorMessage = string.Empty;
		return true;
	}

	/// <summary>
	///	Builds the editable lookup expression for a saved lookup, quoting names only when SQL-style brackets are needed.
	/// </summary>
	/// <param name="lookup">
	///	The lookup to write as text.
	/// </param>
	/// <param name="targetTable">
	///	The table that owns the lookup, used to omit the database name when it matches the source table.
	/// </param>
	/// <returns>
	///	The lookup expression shown to the user.
	/// </returns>
	/// <exception cref="ArgumentNullException">
	///	Thrown when <paramref name="lookup"/> or <paramref name="targetTable"/> is <see langword="null"/>.
	/// </exception>
	public string Format(ColumnLookup lookup, TableModel targetTable)
	{
		ArgumentNullException.ThrowIfNull(lookup);
		ArgumentNullException.ThrowIfNull(targetTable);

		StringBuilder builder = new();

		if (!string.Equals(lookup.SourceTable.DatabaseName, targetTable.DatabaseName, StringComparison.OrdinalIgnoreCase))
		{
			_ = builder.Append(QuoteName(lookup.SourceTable.DatabaseName)).Append('.');
		}

		_ = builder
			.Append(QuoteName(lookup.SourceTable.SchemaName))
			.Append('.')
			.Append(QuoteName(lookup.SourceTable.Name))
			.Append('.')
			.Append(QuoteName(lookup.SourceColumn.Name));

		if (lookup.IsUnique)
		{
			_ = builder.Append(' ').Append(KEYWORD_UNIQUE);
		}

		if (lookup.Scope != RowScope.Any)
		{
			_ = builder.Append(' ').Append(KEYWORD_FROM).Append(' ').Append(lookup.Scope.ToString().ToUpperInvariant());
		}

		string filterText = lookup.FilterText.Trim();

		if (lookup.FilterKind == LookupFilterKind.None || filterText.Length == 0)
		{
			return builder.ToString();
		}

		_ = builder.Append(' ').Append(KEYWORD_WHERE).Append(' ');

		_ = lookup.FilterKind switch
		{
			LookupFilterKind.Regex => builder.Append(KEYWORD_REGEX).Append(' '),
			LookupFilterKind.Sql   => builder.Append(KEYWORD_SQL).Append(' '),
			_                      => builder
		};

		return builder.Append(filterText).ToString();
	}

	/// <summary>
	///	Reads database, schema, table and column name parts, where each part may be bracketed and escaped with doubled
	///	closing brackets.
	/// </summary>
	/// <param name="text">
	///	The complete expression being parsed.
	/// </param>
	/// <param name="position">
	///	The current character index; advanced to the first character after the name.
	/// </param>
	/// <param name="parts">
	///	The two to four name parts read from <paramref name="text"/> when successful; otherwise an empty or partial list.
	/// </param>
	/// <param name="errorMessage">
	///	An empty string when the name is valid; otherwise the user-facing reason it is invalid.
	/// </param>
	/// <returns>
	///	<see langword="true"/> when a valid table and column name was read; otherwise <see langword="false"/>.
	/// </returns>
	private static bool TryReadNameParts(string text, ref int position, out List<string> parts, out string errorMessage)
	{
		parts = [];

		while (true)
		{
			StringBuilder part = new();

			if (position < text.Length && text[position] == '[')
			{
				++position;

				while (true)
				{
					if (position >= text.Length)
					{
						errorMessage = "A name that starts with [ must end with ].";
						return false;
					}

					if (text[position] == ']')
					{
						if (position + 1 < text.Length && text[position + 1] == ']')
						{
							_ = part.Append(']');
							position += 2;
							continue;
						}

						++position;
						break;
					}

					_ = part.Append(text[position]);
					++position;
				}
			}
			else
			{
				while (position < text.Length && text[position] != '.' && !char.IsWhiteSpace(text[position]))
				{
					_ = part.Append(text[position]);
					++position;
				}
			}

			if (part.Length == 0)
			{
				errorMessage = $"Enter the table and column as Table.Column or schema.Table.Column, e.g. {EXAMPLE}.";
				return false;
			}

			parts.Add(part.ToString());

			if (position < text.Length && text[position] == '.')
			{
				++position;
				continue;
			}

			break;
		}

		if (parts.Count is < 2 or > 4)
		{
			errorMessage = $"Enter the column as Table.Column, schema.Table.Column or database.schema.Table.Column, e.g. {EXAMPLE}.";
			return false;
		}

		errorMessage = string.Empty;
		return true;
	}

	/// <summary>
	///	Resolves the parsed name parts to a source table and column in the loaded catalog.
	/// </summary>
	/// <param name="parts">
	///	The parsed name parts, ending with table and column names and optionally starting with database and schema names.
	/// </param>
	/// <param name="targetTable">
	///	The table that owns the lookup, used to prefer its database when the expression omits a database name.
	/// </param>
	/// <param name="sourceTable">
	///	The resolved source table when successful; otherwise <see langword="null"/>.
	/// </param>
	/// <param name="sourceColumn">
	///	The resolved source column when successful; otherwise <see langword="null"/>.
	/// </param>
	/// <param name="errorMessage">
	///	An empty string when resolution succeeds; otherwise the user-facing reason the table or column cannot be found.
	/// </param>
	/// <returns>
	///	<see langword="true"/> when the table and column were found; otherwise <see langword="false"/>.
	/// </returns>
	private bool TryResolveColumn(
		List<string>     parts,
		TableModel       targetTable,
		out TableModel?  sourceTable,
		out ColumnModel? sourceColumn,
		out string       errorMessage
	)
	{
		string  columnName   = parts[^1];
		string  tableName    = parts[^2];
		string? schemaName   = parts.Count >= 3 ? parts[^3] : null;
		string? databaseName = parts.Count == 4 ? parts[0] : null;

		sourceColumn = null;
		sourceTable  = FindTable(databaseName, schemaName, tableName, targetTable, out errorMessage);

		if (sourceTable is null)
		{
			return false;
		}

		sourceColumn = sourceTable.Columns.FirstOrDefault(column => string.Equals(column.Name, columnName, StringComparison.OrdinalIgnoreCase));

		if (sourceColumn is null)
		{
			errorMessage = $"{sourceTable.DisplayName} has no column named [{columnName}].";
			return false;
		}

		errorMessage = string.Empty;
		return true;
	}

	/// <summary>
	///	Finds the table named by a lookup expression and reports ambiguity or missing metadata in user-facing text.
	/// </summary>
	/// <param name="databaseName">
	///	The optional database name supplied in the expression, or <see langword="null"/> to infer one.
	/// </param>
	/// <param name="schemaName">
	///	The optional schema name supplied in the expression, or <see langword="null"/> to infer one.
	/// </param>
	/// <param name="tableName">
	///	The table name supplied in the expression.
	/// </param>
	/// <param name="targetTable">
	///	The table that owns the lookup, included in the search even if it has not yet been added to the catalog.
	/// </param>
	/// <param name="errorMessage">
	///	An empty string when exactly one table is found; otherwise the user-facing reason no table is returned.
	/// </param>
	/// <returns>
	///	The matching table when exactly one can be chosen; otherwise <see langword="null"/>.
	/// </returns>
	/// <remarks>
	///	Without a database name the database of the target table is searched first; without a schema name, dbo wins when
	///	several schemas have a table with the name.
	/// </remarks>
	private TableModel? FindTable(string? databaseName, string? schemaName, string tableName, TableModel targetTable, out string errorMessage)
	{
		IEnumerable<TableModel> tables     = _catalog.Tables.Contains(targetTable) ? _catalog.Tables : _catalog.Tables.Append(targetTable);
		List<TableModel>        candidates = [..
			tables.Where(
				table => string.Equals(table.Name, tableName, StringComparison.OrdinalIgnoreCase)
					&& (schemaName is null || string.Equals(table.SchemaName, schemaName, StringComparison.OrdinalIgnoreCase))
					&& (databaseName is null || string.Equals(table.DatabaseName, databaseName, StringComparison.OrdinalIgnoreCase))
			)
		];

		if (databaseName is null && candidates.Count > 1)
		{
			List<TableModel> sameDatabase = [.. candidates.Where(table => string.Equals(table.DatabaseName, targetTable.DatabaseName, StringComparison.OrdinalIgnoreCase))];

			if (sameDatabase.Count > 0)
			{
				candidates = sameDatabase;
			}
		}

		if (schemaName is null && candidates.Count > 1)
		{
			List<TableModel> defaultSchema = [.. candidates.Where(table => string.Equals(table.SchemaName, DEFAULT_SCHEMA, StringComparison.OrdinalIgnoreCase))];

			if (defaultSchema.Count == 1)
			{
				candidates = defaultSchema;
			}
		}

		switch (candidates.Count)
		{
			case 0:
			{
				string name = string.Join(".", new[] { databaseName, schemaName, tableName }.Where(part => part is not null));

				errorMessage = $"No loaded table is named {name}. Check the spelling, or load the metadata of its database.";
				return null;
			}

			case 1:
			{
				errorMessage = string.Empty;
				return candidates[0];
			}

			default:
			{
				errorMessage = $"Several tables are named {tableName} ({string.Join(", ", candidates.Select(table => $"{table.DatabaseName}.{table.DisplayName}"))}). "
					+ "Add the schema, or the database and schema, e.g. dbo.Shirt.ShirtID.";
				return null;
			}
		}
	}

	/// <summary>
	///	Reads the optional UNIQUE, FROM and WHERE clauses that follow the source column name.
	/// </summary>
	/// <param name="text">
	///	The complete lookup expression being parsed.
	/// </param>
	/// <param name="position">
	///	The current character index; advanced as option words are consumed.
	/// </param>
	/// <param name="isUnique">
	///	Whether the UNIQUE option was present.
	/// </param>
	/// <param name="scope">
	///	The parsed row scope, or <see cref="RowScope.Any"/> when no FROM clause is present.
	/// </param>
	/// <param name="filterKind">
	///	The parsed filter kind, or <see cref="LookupFilterKind.None"/> when no WHERE clause is present.
	/// </param>
	/// <param name="filterText">
	///	The parsed filter text, or an empty string when no WHERE clause is present.
	/// </param>
	/// <param name="errorMessage">
	///	An empty string when the options are valid; otherwise the user-facing reason parsing failed.
	/// </param>
	/// <returns>
	///	<see langword="true"/> when all remaining options are valid; otherwise <see langword="false"/>.
	/// </returns>
	private static bool TryReadOptions(
		string               text,
		ref int              position,
		out bool             isUnique,
		out RowScope         scope,
		out LookupFilterKind filterKind,
		out string           filterText,
		out string           errorMessage
	)
	{
		bool hasScope = false;

		isUnique   = false;
		scope      = RowScope.Any;
		filterKind = LookupFilterKind.None;
		filterText = string.Empty;

		while (true)
		{
			SkipWhiteSpace(text, ref position);

			if (position >= text.Length)
			{
				errorMessage = string.Empty;
				return true;
			}

			string word = ReadWord(text, ref position);

			if (word.Equals(KEYWORD_UNIQUE, StringComparison.OrdinalIgnoreCase) && !isUnique)
			{
				isUnique = true;
				continue;
			}

			if (word.Equals(KEYWORD_FROM, StringComparison.OrdinalIgnoreCase) && !hasScope)
			{
				SkipWhiteSpace(text, ref position);

				string scopeName = ReadWord(text, ref position);

				if (	!Enum.TryParse(scopeName, true, out scope)
						|| !Enum.IsDefined(scope)
						|| scopeName.Any(char.IsDigit)
				)
				{
					errorMessage = "FROM must be followed by ANY (every row), GENERATED (rows generated by this run) or EXISTING (rows that were already there).";
					return false;
				}

				hasScope = true;
				continue;
			}

			if (word.Equals(KEYWORD_WHERE, StringComparison.OrdinalIgnoreCase))
			{
				return TryReadFilter(text[position..], out filterKind, out filterText, out errorMessage);
			}

			errorMessage =
				word.Length == 0
					? $"Unexpected '{text[position]}' after the column name. Expected UNIQUE, FROM or WHERE."
					: $"Unexpected '{word}' after the column name. Expected UNIQUE, FROM ANY|GENERATED|EXISTING or WHERE (each at most once, WHERE last).";
			return false;
		}
	}

	/// <summary>
	///	Reads the body of a WHERE clause and decides whether it is a pattern, regular expression or SQL condition.
	/// </summary>
	/// <param name="text">
	///	The text after the WHERE keyword.
	/// </param>
	/// <param name="filterKind">
	///	The detected filter kind when a non-empty filter is found.
	/// </param>
	/// <param name="filterText">
	///	The filter text without the optional REGEX or SQL prefix.
	/// </param>
	/// <param name="errorMessage">
	///	An empty string when a filter was read; otherwise the user-facing reason the filter is incomplete.
	/// </param>
	/// <returns>
	///	<see langword="true"/> when a non-empty filter was read; otherwise <see langword="false"/>.
	/// </returns>
	private static bool TryReadFilter(string text, out LookupFilterKind filterKind, out string filterText, out string errorMessage)
	{
		int    position = 0;
		string trimmed  = text.Trim();

		SkipWhiteSpace(trimmed, ref position);

		string word = ReadWord(trimmed, ref position);

		(filterKind, filterText) = word.ToUpperInvariant() switch
		{
			KEYWORD_REGEX when position < trimmed.Length && char.IsWhiteSpace(trimmed[position]) => (LookupFilterKind.Regex, trimmed[position..].Trim()),
			KEYWORD_SQL when position < trimmed.Length && char.IsWhiteSpace(trimmed[position])   => (LookupFilterKind.Sql, trimmed[position..].Trim()),
			KEYWORD_REGEX or KEYWORD_SQL when position >= trimmed.Length                       => (word.Equals(KEYWORD_SQL, StringComparison.OrdinalIgnoreCase) ? LookupFilterKind.Sql : LookupFilterKind.Regex, string.Empty),
			_                                                                                  => (LookupFilterKind.Pattern, trimmed)
		};

		if (filterText.Length == 0)
		{
			errorMessage = filterKind switch
			{
				LookupFilterKind.Regex => "Enter a regular expression after WHERE REGEX, e.g. WHERE REGEX ^S[0-9]{5}$.",
				LookupFilterKind.Sql   => "Enter a SQL condition after WHERE SQL, e.g. WHERE SQL s.[Size] = 'XL'.",
				_                      => "Enter a pattern after WHERE, e.g. WHERE 'S' THEN NUM(digits=5)."
			};
			return false;
		}

		errorMessage = string.Empty;
		return true;
	}

	/// <summary>
	///	Validates pattern and regular-expression filters before the lookup is accepted.
	/// </summary>
	/// <param name="filterKind">
	///	The kind of filter to validate.
	/// </param>
	/// <param name="filterText">
	///	The filter text supplied by the user.
	/// </param>
	/// <param name="errorMessage">
	///	An empty string when the filter is valid; otherwise the user-facing validation problem.
	/// </param>
	/// <returns>
	///	<see langword="true"/> when the filter is valid or does not need validation; otherwise <see langword="false"/>.
	/// </returns>
	private bool TryValidateFilter(LookupFilterKind filterKind, string filterText, out string errorMessage)
	{
		switch (filterKind)
		{
			case LookupFilterKind.Pattern:
			{
				if (!_patternTranslator.TryValidate(filterText, out string patternError))
				{
					errorMessage = $"WHERE pattern: {patternError}";
					return false;
				}

				break;
			}

			case LookupFilterKind.Regex:
			{
				try
				{
					_ = new Regex(filterText, RegexOptions.None, REGEX_CHECK_TIMEOUT);
				}
				catch (ArgumentException exception)
				{
					errorMessage = $"WHERE REGEX: the regular expression is not valid. {exception.Message}";
					return false;
				}

				break;
			}
		}

		errorMessage = string.Empty;
		return true;
	}

	/// <summary>
	///	Moves the parsing position past any whitespace characters.
	/// </summary>
	/// <param name="text">
	///	The text being parsed.
	/// </param>
	/// <param name="position">
	///	The current character index; advanced to the next non-whitespace character or the end of the text.
	/// </param>
	private static void SkipWhiteSpace(string text, ref int position)
	{
		while (position < text.Length && char.IsWhiteSpace(text[position]))
		{
			++position;
		}
	}

	/// <summary>
	///	Reads a keyword-style word made from letters, digits and underscores.
	/// </summary>
	/// <param name="text">
	///	The text being parsed.
	/// </param>
	/// <param name="position">
	///	The current character index; advanced to the first character after the word.
	/// </param>
	/// <returns>
	///	The word read from <paramref name="text"/>, or an empty string when no word starts at <paramref name="position"/>.
	/// </returns>
	private static string ReadWord(string text, ref int position)
	{
		int start = position;

		while (position < text.Length && (char.IsLetterOrDigit(text[position]) || text[position] == '_'))
		{
			++position;
		}

		return text[start..position];
	}

	/// <summary>
	///	Quotes a SQL identifier only when it is not a plain word that can be written without brackets.
	/// </summary>
	/// <param name="name">
	///	The identifier to quote.
	/// </param>
	/// <returns>
	///	The original identifier for plain names; otherwise a bracketed identifier with closing brackets escaped.
	/// </returns>
	private static string QuoteName(string name)
	{
		bool isPlain = name.Length > 0
			&& !char.IsDigit(name[0])
			&& name.All(character => char.IsLetterOrDigit(character) || character == '_');

		return isPlain ? name : $"[{name.Replace("]", "]]")}]";
	}
}
