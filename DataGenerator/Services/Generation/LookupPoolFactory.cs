using DataGenerator.Interfaces;
using DataGenerator.Models;
using DataGenerator.Services.Patterns;

namespace DataGenerator.Services.Generation;

/// <summary>
///	Turns the "Value from table" rules of a row set into <see cref="LookupPool"/>s with the T-SQL that reads their values.
/// </summary>
internal sealed class LookupPoolFactory
{
	#region FIELDS
	#region PUBLIC
	public const int MAXIMUM_SAMPLE_SIZE = ExistingKeyPool.MAXIMUM_SAMPLE_SIZE;

	/// <summary>
	///	The source row alias that SQL filters (WHERE SQL …) use, e.g. s.[Colour] = 'Red'.
	/// </summary>
	public const string SOURCE_ALIAS = "s";
	#endregion PUBLIC

	#region PRIVATE
	private const string TARGET_ALIAS      = "t";
	private const string VALUE_ALIAS       = "[d]";
	private const string FILTER_TEXT_TYPE  = "nvarchar(4000)";
	private const int    DATE_TEXT_STYLE   = 121;
	private const int    BINARY_TEXT_STYLE = 1;

	private readonly ISqlValueConverter    _converter;
	private readonly IPatternSqlTranslator _translator;
	private readonly RowSnapshotSet        _snapshots;
	private readonly bool                  _usesScriptVariables;
	private int                            _poolCount;
	#endregion PRIVATE
	#endregion FIELDS

	#region CONSTRUCTOR
	/// <summary>
	///	Creates the factory that turns value-from-table rules into lookup pools.
	/// </summary>
	/// <param name="converter">
	///	The converter used to describe and compare SQL Server values.
	/// </param>
	/// <param name="translator">
	///	The translator used for pattern filters.
	/// </param>
	/// <param name="snapshots">
	///	The snapshot set used for generated-row and existing-row scopes.
	/// </param>
	/// <param name="usesScriptVariables">
	///	Whether generated SQL scripts will choose values from table variables.
	/// </param>
	/// <exception cref="ArgumentNullException">
	///	Thrown when <paramref name="converter"/>, <paramref name="translator"/> or <paramref name="snapshots"/> is
	///	<see langword="null"/>.
	/// </exception>
	public LookupPoolFactory(ISqlValueConverter converter, IPatternSqlTranslator translator, RowSnapshotSet snapshots, bool usesScriptVariables)
	{
		_poolCount = 0;

		_converter           = converter  ?? throw new ArgumentNullException(nameof(converter));
		_translator          = translator ?? throw new ArgumentNullException(nameof(translator));
		_snapshots           = snapshots  ?? throw new ArgumentNullException(nameof(snapshots));
		_usesScriptVariables = usesScriptVariables;
	}
	#endregion CONSTRUCTOR

	#region METHODS
	#region PUBLIC
	/// <summary>
	///	Creates the lookup pool for one value-from-table column rule.
	/// </summary>
	/// <param name="targetTable">
	///	The table whose row set receives the lookup value.
	/// </param>
	/// <param name="rowSet">
	///	The row set that contains the rule.
	/// </param>
	/// <param name="rule">
	///	The column rule with a resolved lookup source.
	/// </param>
	/// <returns>
	///	A lookup pool with the SQL needed to read its values.
	/// </returns>
	/// <exception cref="InvalidOperationException">
	///	Thrown when the rule has no resolved lookup source.
	/// </exception>
	public LookupPool Create(TableModel targetTable, RowSetPlan rowSet, ColumnRule rule)
	{
		ColumnLookup lookup   = rule.Lookup
			?? throw new InvalidOperationException($"Column [{rule.Column.Name}] has no resolved 'Value from table' source.");
		string       location = GenerationBlueprintBuilder.DescribeLocation(targetTable, rowSet, null, rule.Column);
		int          count    = lookup.IsUnique ? rowSet.RowCount : 1;

		++_poolCount;

		return new LookupPool(
			_poolCount,
			lookup,
			rule.Column,
			count,
			BuildSelectStatement(lookup, targetTable, rule.Column, lookup.IsUnique ? rowSet.RowCount : MAXIMUM_SAMPLE_SIZE, location),
			location,
			_usesScriptVariables
		);
	}
	#endregion PUBLIC

	#region PRIVATE
	/// <summary>
	///	Builds the randomised SQL statement that reads lookup values from the source table.
	/// </summary>
	/// <param name="lookup">
	///	The resolved lookup source and filter.
	/// </param>
	/// <param name="targetTable">
	///	The table whose column receives the values.
	/// </param>
	/// <param name="targetColumn">
	///	The target column used for conversion and uniqueness checks.
	/// </param>
	/// <param name="topCount">
	///	The maximum number of values to read.
	/// </param>
	/// <param name="location">
	///	The user-facing location used in filter errors.
	/// </param>
	/// <returns>
	///	A SELECT statement that returns converted, non-null values in random order.
	/// </returns>
	/// <exception cref="DataGenerationException">
	///	Thrown when a pattern filter cannot be translated to SQL.
	/// </exception>
	private string BuildSelectStatement(ColumnLookup lookup, TableModel targetTable, ColumnModel targetColumn, int topCount, string location)
	{
		string       sourceColumn = $"{SOURCE_ALIAS}.{SqlSyntax.QuoteIdentifier(lookup.SourceColumn.Name)}";
		string       valueColumn  = SqlSyntax.QuoteIdentifier(GeneratedKeyTable.GetValueColumnName(0));
		List<string> conditions   = [$"{sourceColumn} IS NOT NULL"];

		if (lookup.Scope != RowScope.Any)
		{
			// Rows are told apart by their primary key, so a generated row counts even when an older row has the same value.
			// Without a primary key only the value itself can be compared.
			List<string> keyColumns = [..
				lookup
					.SourceTable
					.Columns
					.Where(column => column.IsPrimaryKey)
					.Select(column => column.Name)
			];
			RowSnapshot  snapshot   = _snapshots.Get(lookup.SourceTable, keyColumns.Count > 0 ? keyColumns : [lookup.SourceColumn.Name]);

			conditions.Add(snapshot.BuildScopeCondition(lookup.Scope, SOURCE_ALIAS)!);
		}

		if (_converter.GetMaximumTextLength(targetColumn) is int maximumLength)
		{
			// Longer text would be cut off by the conversion and no longer be a value of the source column.
			conditions.Add($"LEN(CONVERT(nvarchar(max), {sourceColumn})) <= {maximumLength}");
		}

		if (BuildFilter(lookup, sourceColumn, location) is string filter)
		{
			conditions.Add(filter);
		}

		List<string> valueConditions = [$"{VALUE_ALIAS}.{valueColumn} IS NOT NULL"];

		if (lookup.IsUnique)
		{
			valueConditions.Add(
				$"NOT EXISTS (SELECT 1 FROM {targetTable.FullyQualifiedName} AS {TARGET_ALIAS} "
					+ $"WHERE {TARGET_ALIAS}.{SqlSyntax.QuoteIdentifier(targetColumn.Name)} = {VALUE_ALIAS}.{valueColumn})"
			);
		}

		return $"SELECT TOP ({topCount}) {VALUE_ALIAS}.{valueColumn} "
			+ $"FROM (SELECT DISTINCT TRY_CONVERT({_converter.GetTypeDeclaration(targetColumn)}, {sourceColumn}) AS {valueColumn} "
			+ $"FROM {lookup.SourceTable.FullyQualifiedName} AS {SOURCE_ALIAS} "
			+ $"WHERE {string.Join(" AND ", conditions)}) AS {VALUE_ALIAS} "
			+ $"WHERE {string.Join(" AND ", valueConditions)} "
			+ "ORDER BY NEWID()";
	}

	/// <summary>
	///	Builds the optional WHERE condition for a lookup filter.
	/// </summary>
	/// <param name="lookup">
	///	The lookup whose filter should be converted.
	/// </param>
	/// <param name="sourceColumn">
	///	The SQL expression for the source column.
	/// </param>
	/// <param name="location">
	///	The user-facing location used in pattern errors.
	/// </param>
	/// <returns>
	///	The SQL condition, or <see langword="null"/> when there is no filter.
	/// </returns>
	/// <exception cref="DataGenerationException">
	///	Thrown when a pattern filter cannot be translated to SQL.
	/// </exception>
	private string? BuildFilter(ColumnLookup lookup, string sourceColumn, string location)
	{
		if (lookup.FilterKind == LookupFilterKind.None || string.IsNullOrWhiteSpace(lookup.FilterText))
		{
			return null;
		}

		if (lookup.FilterKind == LookupFilterKind.Sql)
		{
			return SqlSyntax.WrapCondition(lookup.FilterText.Trim());
		}

		string text = DescribeAsText(lookup.SourceColumn, sourceColumn);

		if (lookup.FilterKind == LookupFilterKind.Regex)
		{
			return $"REGEXP_LIKE({text}, {SqlSyntax.QuoteUnicodeText(lookup.FilterText)})";
		}

		try
		{
			return $"({_translator.ToSqlCondition(lookup.FilterText, text)})";
		}
		catch (PatternSyntaxException exception)
		{
			throw new DataGenerationException($"The WHERE pattern cannot be used: {exception.Message}", location, exception);
		}
	}

	/// <summary>
	///	The source value as text, the way filters see it: dates as yyyy-mm-dd hh:mi:ss.mmm and binary values as 0x….
	/// </summary>
	/// <param name="column">
	///	The source column whose category controls the conversion.
	/// </param>
	/// <param name="sourceColumn">
	///	The SQL expression for the source column.
	/// </param>
	/// <returns>
	///	A SQL expression that converts the source value to filter text.
	/// </returns>
	private string DescribeAsText(ColumnModel column, string sourceColumn)
		=> _converter.GetCategory(column) switch
		{
			SqlTypeCategory.DateTime => $"CONVERT({FILTER_TEXT_TYPE}, {sourceColumn}, {DATE_TEXT_STYLE})",
			SqlTypeCategory.Binary   => $"CONVERT({FILTER_TEXT_TYPE}, {sourceColumn}, {BINARY_TEXT_STYLE})",
			_                        => $"CONVERT({FILTER_TEXT_TYPE}, {sourceColumn})"
		};
	#endregion PRIVATE
	#endregion METHODS
}
