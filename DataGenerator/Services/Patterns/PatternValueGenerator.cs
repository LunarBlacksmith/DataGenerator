using System.Collections.Concurrent;
using System.Text;
using DataGenerator.Interfaces;

namespace DataGenerator.Services.Patterns;

public sealed class PatternValueGenerator : IPatternValueGenerator
{
	private const int MAXIMUM_CACHE_SIZE = 256;

	private readonly ConcurrentDictionary<string, ParsedPattern> _cache = new(StringComparer.Ordinal);
	private readonly TimeProvider                                _timeProvider;

	/// <summary>
	///	Creates a pattern value generator that uses the system clock for TODAY.
	/// </summary>
	public PatternValueGenerator()
		: this(TimeProvider.System)
	{
	}

	/// <summary>
	///	Creates a pattern value generator with a replaceable clock for TODAY.
	/// </summary>
	/// <param name="timeProvider">
	///	The clock used by TODAY; replaceable so the current date can be fixed in tests.
	/// </param>
	/// <exception cref="ArgumentNullException">
	///	Thrown when <paramref name="timeProvider"/> is <see langword="null"/>.
	/// </exception>
	public PatternValueGenerator(TimeProvider timeProvider)
	{
		_timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
	}

	/// <summary>
	///	Generates one pattern value for a row when row count and other-column values are not available.
	/// </summary>
	/// <param name="expression">
	///	The pattern expression to generate from.
	/// </param>
	/// <param name="rowIndex">
	///	The zero-based row index within the row set.
	/// </param>
	/// <returns>
	///	The generated value text.
	/// </returns>
	/// <exception cref="ArgumentNullException">
	///	Thrown when <paramref name="expression"/> is <see langword="null"/>.
	/// </exception>
	/// <exception cref="PatternSyntaxException">
	///	Thrown when the pattern expression is invalid.
	/// </exception>
	public string Generate(string expression, long rowIndex) => Generate(expression, rowIndex, null, null);

	/// <summary>
	///	Generates one pattern value for a row, optionally with row-set size and same-row column values.
	/// </summary>
	/// <param name="expression">
	///	The pattern expression to generate from.
	/// </param>
	/// <param name="rowIndex">
	///	The zero-based row index within the row set.
	/// </param>
	/// <param name="rowCount">
	///	The total number of rows in the row set, or <see langword="null"/> when it is unknown.
	/// </param>
	/// <param name="columnValues">
	///	The optional lookup for values of other columns in the same row.
	/// </param>
	/// <returns>
	///	The generated value text.
	/// </returns>
	/// <exception cref="ArgumentNullException">
	///	Thrown when <paramref name="expression"/> is <see langword="null"/>.
	/// </exception>
	/// <exception cref="PatternSyntaxException">
	///	Thrown when the pattern expression is invalid.
	/// </exception>
	/// <exception cref="InvalidOperationException">
	///	Thrown when generation needs unavailable column values or exceeds the maximum output length.
	/// </exception>
	public string Generate(string expression, long rowIndex, long? rowCount, Func<string, string>? columnValues)
	{
		ParsedPattern pattern = GetOrParse(expression);
		StringBuilder builder = new();

		pattern.Node.Append(builder, new PatternContext(rowIndex, rowCount, Random.Shared, _timeProvider, columnValues));
		return builder.ToString();
	}

	/// <summary>
	///	Finds the column names referenced by COL or COLUMN calls in a pattern.
	/// </summary>
	/// <param name="expression">
	///	The pattern expression to inspect.
	/// </param>
	/// <returns>
	///	The referenced column names, or an empty list when the pattern cannot be parsed.
	/// </returns>
	/// <exception cref="ArgumentNullException">
	///	Thrown when <paramref name="expression"/> is <see langword="null"/>.
	/// </exception>
	public IReadOnlyList<string> GetColumnReferences(string expression)
	{
		try
		{
			return GetOrParse(expression).ColumnReferences;
		}
		catch (PatternSyntaxException)
		{
			return [];
		}
	}

	/// <summary>
	///	Checks whether a pattern expression can be parsed and generated.
	/// </summary>
	/// <param name="expression">
	///	The pattern expression to validate.
	/// </param>
	/// <param name="errorMessage">
	///	The parse error when validation fails; otherwise an empty string.
	/// </param>
	/// <returns>
	///	<see langword="true"/> when the pattern parses successfully; otherwise <see langword="false"/>.
	/// </returns>
	/// <exception cref="ArgumentNullException">
	///	Thrown when <paramref name="expression"/> is <see langword="null"/>.
	/// </exception>
	public bool TryValidate(string expression, out string errorMessage)
	{
		try
		{
			_            = GetOrParse(expression);
			errorMessage = string.Empty;
			return true;
		}
		catch (PatternSyntaxException exception)
		{
			errorMessage = exception.Message;
			return false;
		}
	}

	/// <summary>
	///	Gets a parsed pattern from the cache, or parses it and records its column references.
	/// </summary>
	/// <param name="expression">
	///	The pattern expression to parse or find in the cache.
	/// </param>
	/// <returns>
	///	The cached or newly parsed pattern.
	/// </returns>
	/// <exception cref="ArgumentNullException">
	///	Thrown when <paramref name="expression"/> is <see langword="null"/>.
	/// </exception>
	/// <exception cref="PatternSyntaxException">
	///	Thrown when the expression cannot be parsed.
	/// </exception>
	private ParsedPattern GetOrParse(string expression)
	{
		ArgumentNullException.ThrowIfNull(expression);

		if (_cache.TryGetValue(expression, out ParsedPattern? cachedPattern))
		{
			return cachedPattern;
		}

		PatternNode  node             = PatternParser.Parse(expression);
		List<string> columnReferences = [];

		node.CollectColumnReferences(columnReferences);

		ParsedPattern pattern = new(node, columnReferences);

		if (_cache.Count >= MAXIMUM_CACHE_SIZE)
		{
			_cache.Clear();
		}

		_cache[expression] = pattern;
		return pattern;
	}

	private sealed record ParsedPattern(PatternNode Node, IReadOnlyList<string> ColumnReferences);
}