using System.Collections.Concurrent;
using System.Text;

namespace DataGenerator.Services.Patterns;

public sealed class PatternValueGenerator : IPatternValueGenerator
{
	private const int MAXIMUM_CACHE_SIZE = 256;

	private readonly ConcurrentDictionary<string, PatternNode> _cache = new ConcurrentDictionary<string, PatternNode>(StringComparer.Ordinal);

	public string Generate(string expression, long rowIndex)
	{
		PatternNode   node    = GetOrParse(expression);
		StringBuilder builder = new StringBuilder();

		node.Append(builder, new PatternContext(rowIndex, Random.Shared));
		return builder.ToString();
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

	private PatternNode GetOrParse(string expression)
	{
		ArgumentNullException.ThrowIfNull(expression);

		if (_cache.TryGetValue(expression, out PatternNode? cachedNode))
		{
			return cachedNode;
		}

		PatternNode node = PatternParser.Parse(expression);

		if (_cache.Count >= MAXIMUM_CACHE_SIZE)
		{
			_cache.Clear();
		}

		_cache[expression] = node;
		return node;
	}
}