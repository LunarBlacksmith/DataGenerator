using System.Collections.Concurrent;
using System.Text;

namespace DataGenerator.Services.Patterns;

public sealed class PatternValueGenerator : IPatternValueGenerator
{
	private const int MAXIMUM_CACHE_SIZE = 256;

	private readonly ConcurrentDictionary<string, ParsedPattern> _cache = new ConcurrentDictionary<string, ParsedPattern>(StringComparer.Ordinal);
	private readonly TimeProvider                                _timeProvider;

	public PatternValueGenerator()
		: this(TimeProvider.System)
	{
	}

	/// <param name="timeProvider">The clock of TODAY(); replaceable so the current date can be fixed in tests.</param>
	public PatternValueGenerator(TimeProvider timeProvider)
	{
		_timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
	}

	public string Generate(string expression, long rowIndex) => Generate(expression, rowIndex, null);

	public string Generate(string expression, long rowIndex, Func<string, string>? columnValues)
	{
		ParsedPattern pattern = GetOrParse(expression);
		StringBuilder builder = new StringBuilder();

		pattern.Node.Append(builder, new PatternContext(rowIndex, Random.Shared, _timeProvider, columnValues));
		return builder.ToString();
	}

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

		ParsedPattern pattern = new ParsedPattern(node, columnReferences);

		if (_cache.Count >= MAXIMUM_CACHE_SIZE)
		{
			_cache.Clear();
		}

		_cache[expression] = pattern;
		return pattern;
	}

	private sealed record ParsedPattern(PatternNode Node, IReadOnlyList<string> ColumnReferences);
}