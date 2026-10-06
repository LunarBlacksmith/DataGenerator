using DataGenerator.Interfaces;
using DataGenerator.Models;
using DataGenerator.Services.Patterns;

namespace DataGenerator.Services.Generation;

/// <summary>
/// Turns the "Value from table" rules of a row set into <see cref="LookupPool"/>s with the T-SQL that reads their values.
/// </summary>
internal sealed class LookupPoolFactory
{
	public const int MAXIMUM_SAMPLE_SIZE = ExistingKeyPool.MAXIMUM_SAMPLE_SIZE;

	/// <summary>
	/// The source row alias that SQL filters (WHERE SQL …) use, e.g. s.[Colour] = 'Red'.
	/// </summary>
	public const string SOURCE_ALIAS = "s";

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

	public LookupPoolFactory(ISqlValueConverter converter, IPatternSqlTranslator translator, RowSnapshotSet snapshots, bool usesScriptVariables)
	{
		_converter           = converter  ?? throw new ArgumentNullException(nameof(converter));
		_translator          = translator ?? throw new ArgumentNullException(nameof(translator));
		_snapshots           = snapshots  ?? throw new ArgumentNullException(nameof(snapshots));
		_usesScriptVariables = usesScriptVariables;
	}

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

	private string BuildSelectStatement(ColumnLookup lookup, TableModel targetTable, ColumnModel targetColumn, int topCount, string location)
	{
		string       sourceColumn = $"{SOURCE_ALIAS}.{SqlSyntax.QuoteIdentifier(lookup.SourceColumn.Name)}";
		string       valueColumn  = SqlSyntax.QuoteIdentifier(GeneratedKeyTable.GetValueColumnName(0));
		List<string> conditions   = [$"{sourceColumn} IS NOT NULL"];

		if (lookup.Scope != RowScope.Any)
		{
			// Rows are told apart by their primary key, so a generated row counts even when an older row has the same value.
			// Without a primary key only the value itself can be compared.
			List<string> keyColumns = [.. lookup.SourceTable.Columns.Where(column => column.IsPrimaryKey).Select(column => column.Name)];
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
	/// The source value as text, the way filters see it: dates as yyyy-mm-dd hh:mi:ss.mmm and binary values as 0x….
	/// </summary>
	private string DescribeAsText(ColumnModel column, string sourceColumn)
		=> _converter.GetCategory(column) switch
		{
			SqlTypeCategory.DateTime => $"CONVERT({FILTER_TEXT_TYPE}, {sourceColumn}, {DATE_TEXT_STYLE})",
			SqlTypeCategory.Binary   => $"CONVERT({FILTER_TEXT_TYPE}, {sourceColumn}, {BINARY_TEXT_STYLE})",
			_                        => $"CONVERT({FILTER_TEXT_TYPE}, {sourceColumn})"
		};
}
