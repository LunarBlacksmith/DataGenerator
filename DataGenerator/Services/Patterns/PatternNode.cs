using System.Text;

namespace DataGenerator.Services.Patterns;

internal sealed class PatternContext
{
	public const int MAXIMUM_OUTPUT_LENGTH = 100_000;

	private readonly Func<string, string>? _columnValues;

	public PatternContext(long rowIndex, long? rowCount, Random random, TimeProvider timeProvider, Func<string, string>? columnValues = null)
	{
		RowIndex      = rowIndex;
		RowCount      = rowCount;
		Random        = random;
		TimeProvider  = timeProvider;
		_columnValues = columnValues;
	}

	public long         RowIndex     { get; }

	/// <summary>
	/// The number of rows in the row set, or <see langword="null"/> when unknown (e.g. in the pattern reference window).
	/// LAST(...) only knows which rows are the last ones when the count is known.
	/// </summary>
	public long?        RowCount     { get; }
	public Random       Random       { get; }
	public TimeProvider TimeProvider { get; }

	/// <summary>
	/// The local date and time at the moment a value is generated.
	/// </summary>
	public DateTime Now => TimeProvider.GetLocalNow().DateTime;

	/// <summary>
	/// The value of another column of the row being generated, as text (used by COL).
	/// </summary>
	public string GetColumnValue(string columnName)
	{
		if (_columnValues is null)
		{
			throw new InvalidOperationException(
				$"COL({columnName}) uses the value of another column of the same row, so it only works in the Pattern mode of a column rule."
			);
		}

		return _columnValues(columnName);
	}

	public static void EnsureLength(StringBuilder builder)
	{
		if (builder.Length > MAXIMUM_OUTPUT_LENGTH)
		{
			throw new InvalidOperationException($"The pattern produced more than {MAXIMUM_OUTPUT_LENGTH:N0} characters. Reduce the REPEATED counts.");
		}
	}
}

internal abstract class PatternNode
{
	/// <summary>
	/// The function that created the node, e.g. "RAND_DATE", used in error messages.
	/// </summary>
	public string FunctionName { get; set; } = string.Empty;

	public abstract void Append(StringBuilder builder, PatternContext context);

	/// <summary>
	/// Every shape of value the node can produce, used to recognise matching values that already exist in a table
	/// (see <see cref="PatternSqlTranslator"/>). Throws <see cref="PatternSyntaxException"/> when the node's values
	/// cannot be recognised, e.g. random dates.
	/// </summary>
	public virtual PatternTemplateSet ExpandTemplates()
	{
		string description = FunctionName.Length == 0 ? "This part of the pattern" : $"{FunctionName}(...)";

		throw new PatternSyntaxException(
			$"{description} cannot be used to find existing values. Patterns that find values may use text, OR, REPEATED, "
			+ $"SEQ, NUM, RAND_NUM, RAND_DIGITS, RAND_LETTERS, RAND_ALPHANUM, ONE_OF, CYCLE, FIRST, LAST and GUID.",
			1
		);
	}

	/// <summary>
	/// Adds the names of the columns the node takes values from with COL(...).
	/// </summary>
	public virtual void CollectColumnReferences(ICollection<string> columnNames)
	{
	}
}

internal sealed class LiteralPatternNode : PatternNode
{
	private readonly string _text;

	public LiteralPatternNode(string text)
	{
		_text = text;
	}

	public override void Append(StringBuilder builder, PatternContext context) => _ = builder.Append(_text);

	public override PatternTemplateSet ExpandTemplates() => PatternTemplateSet.FromLiteral(_text);
}

internal sealed class ConcatenationPatternNode : PatternNode
{
	private readonly IReadOnlyList<PatternNode> _parts;

	public ConcatenationPatternNode(IReadOnlyList<PatternNode> parts)
	{
		_parts = parts;
	}

	public override void Append(StringBuilder builder, PatternContext context)
	{
		foreach (PatternNode part in _parts)
		{
			part.Append(builder, context);
		}
	}

	public override void CollectColumnReferences(ICollection<string> columnNames)
	{
		foreach (PatternNode part in _parts)
		{
			part.CollectColumnReferences(columnNames);
		}
	}

	public override PatternTemplateSet ExpandTemplates()
	{
		PatternTemplateSet result = PatternTemplateSet.EMPTY_TEXT;

		foreach (PatternNode part in _parts)
		{
			result = result.Then(part.ExpandTemplates());
		}

		return result;
	}
}

internal sealed class ChoicePatternNode : PatternNode
{
	private readonly IReadOnlyList<PatternNode> _options;

	public ChoicePatternNode(IReadOnlyList<PatternNode> options)
	{
		_options = options;
	}

	public override void Append(StringBuilder builder, PatternContext context)
		=> _options[context.Random.Next(_options.Count)].Append(builder, context);

	public override void CollectColumnReferences(ICollection<string> columnNames)
	{
		foreach (PatternNode option in _options)
		{
			option.CollectColumnReferences(columnNames);
		}
	}

	public override PatternTemplateSet ExpandTemplates()
		=> PatternTemplateSet.Union([.. _options.Select(option => option.ExpandTemplates())]);
}

internal sealed class RepetitionPatternNode : PatternNode
{
	private readonly PatternNode _node;
	private readonly int         _minimumCount;
	private readonly int         _maximumCount;

	public RepetitionPatternNode(PatternNode node, int minimumCount, int maximumCount)
	{
		_node         = node;
		_minimumCount = minimumCount;
		_maximumCount = maximumCount;
	}

	public override void Append(StringBuilder builder, PatternContext context)
	{
		int count = context.Random.Next(_minimumCount, _maximumCount + 1);

		for (int repetition = 0; repetition < count; ++repetition)
		{
			_node.Append(builder, context);
			PatternContext.EnsureLength(builder);
		}
	}

	public override void CollectColumnReferences(ICollection<string> columnNames) => _node.CollectColumnReferences(columnNames);

	public override PatternTemplateSet ExpandTemplates()
	{
		PatternTemplateSet       single   = _node.ExpandTemplates();
		PatternTemplateSet       repeated = PatternTemplateSet.EMPTY_TEXT;
		List<PatternTemplateSet> counts   = [];

		for (int count = 0; count <= _maximumCount; ++count)
		{
			if (count >= _minimumCount)
			{
				counts.Add(repeated);
			}

			if (count < _maximumCount)
			{
				repeated = repeated.Then(single);
			}
		}

		return PatternTemplateSet.Union(counts);
	}
}