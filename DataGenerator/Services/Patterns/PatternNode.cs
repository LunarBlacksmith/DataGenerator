using System.Text;

namespace DataGenerator.Services.Patterns;

internal sealed class PatternContext
{
	public const int MAXIMUM_OUTPUT_LENGTH = 100_000;

	private readonly Func<string, string>? _columnValues;

	public PatternContext(long rowIndex, Random random, TimeProvider timeProvider, Func<string, string>? columnValues = null)
	{
		RowIndex      = rowIndex;
		Random        = random;
		TimeProvider  = timeProvider;
		_columnValues = columnValues;
	}

	public long         RowIndex     { get; }
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
	public abstract void Append(StringBuilder builder, PatternContext context);

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
}