using System.Text;

namespace DataGenerator.Services.Patterns;

internal sealed class PatternContext
{
	public const int MAXIMUM_OUTPUT_LENGTH = 100_000;

	private readonly Func<string, string>? _columnValues;

	/// <summary>
	///	Creates the row-time services and counters used while a pattern value is generated.
	/// </summary>
	/// <param name="rowIndex">
	///	The zero-based index of the row within its row set.
	/// </param>
	/// <param name="rowCount">
	///	The number of rows in the row set, or <see langword="null"/> when it is not known.
	/// </param>
	/// <param name="random">
	///	The random source used by random pattern nodes.
	/// </param>
	/// <param name="timeProvider">
	///	The clock used by TODAY nodes.
	/// </param>
	/// <param name="columnValues">
	///	The optional lookup for values of other columns in the same row.
	/// </param>
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
	///	The number of rows in the row set, or <see langword="null"/> when unknown (e.g. in the pattern reference window).
	///	LAST(...) only knows which rows are the last ones when the count is known.
	/// </summary>
	public long?        RowCount     { get; }
	public Random       Random       { get; }
	public TimeProvider TimeProvider { get; }

	/// <summary>
	///	The local date and time at the moment a value is generated.
	/// </summary>
	public DateTime Now => TimeProvider.GetLocalNow().DateTime;

	/// <summary>
	///	Gets the value of another column of the row being generated, as text, for COL nodes.
	/// </summary>
	/// <param name="columnName">
	///	The name of the column to read.
	/// </param>
	/// <returns>
	///	The referenced column's value as pattern text.
	/// </returns>
	/// <exception cref="InvalidOperationException">
	///	Thrown when the pattern is not being generated in a column-rule context with column values available.
	/// </exception>
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

	/// <summary>
	///	Checks that the generated output has not exceeded the pattern language length limit.
	/// </summary>
	/// <param name="builder">
	///	The builder containing the output generated so far.
	/// </param>
	/// <exception cref="InvalidOperationException">
	///	Thrown when the output is longer than the maximum allowed pattern result.
	/// </exception>
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
	///	The function that created the node, e.g. "RAND_DATE", used in error messages.
	/// </summary>
	public string FunctionName { get; set; } = string.Empty;

	/// <summary>
	///	Appends this node's generated text for a single row.
	/// </summary>
	/// <param name="builder">
	///	The builder receiving generated text.
	/// </param>
	/// <param name="context">
	///	The row context used to generate the value.
	/// </param>
	public abstract void Append(StringBuilder builder, PatternContext context);

	/// <summary>
	///	Builds every recognisable shape of value this node can produce, used to match existing table values in SQL.
	/// </summary>
	/// <returns>
	///	The set of SQL-matchable templates for this node.
	/// </returns>
	/// <exception cref="PatternSyntaxException">
	///	Thrown by the base implementation when the node's values cannot be recognised, such as random dates.
	/// </exception>
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
	///	Adds the names of columns this node reads through COL nodes. The base node has no references to add.
	/// </summary>
	/// <param name="columnNames">
	///	The collection receiving referenced column names.
	/// </param>
	public virtual void CollectColumnReferences(ICollection<string> columnNames)
	{
	}
}

internal sealed class LiteralPatternNode : PatternNode
{
	private readonly string _text;

	/// <summary>
	///	Creates a node that always appends the same literal text.
	/// </summary>
	/// <param name="text">
	///	The text to append.
	/// </param>
	public LiteralPatternNode(string text)
	{
		_text = text;
	}

	/// <summary>
	///	Appends the literal text exactly as it was parsed or computed.
	/// </summary>
	/// <param name="builder">
	///	The builder receiving the literal text.
	/// </param>
	/// <param name="context">
	///	The row context; this node does not read values from it.
	/// </param>
	public override void Append(StringBuilder builder, PatternContext context) => _ = builder.Append(_text);

	/// <summary>
	///	Expands the literal text to one literal template.
	/// </summary>
	/// <returns>
	///	A template set that matches the literal text, or the empty-text template for an empty literal.
	/// </returns>
	public override PatternTemplateSet ExpandTemplates() => PatternTemplateSet.FromLiteral(_text);
}

internal sealed class ConcatenationPatternNode : PatternNode
{
	private readonly IReadOnlyList<PatternNode> _parts;

	/// <summary>
	///	Creates a node that appends child nodes in order for FOLLOWED BY, THEN or +.
	/// </summary>
	/// <param name="parts">
	///	The ordered child nodes to concatenate.
	/// </param>
	public ConcatenationPatternNode(IReadOnlyList<PatternNode> parts)
	{
		_parts = parts;
	}

	/// <summary>
	///	Appends each concatenated child node in source order.
	/// </summary>
	/// <param name="builder">
	///	The builder receiving the combined text.
	/// </param>
	/// <param name="context">
	///	The row context passed to each child node.
	/// </param>
	public override void Append(StringBuilder builder, PatternContext context)
	{
		foreach (PatternNode part in _parts)
		{
			part.Append(builder, context);
		}
	}

	/// <summary>
	///	Collects column references from each concatenated child node.
	/// </summary>
	/// <param name="columnNames">
	///	The collection receiving referenced column names.
	/// </param>
	public override void CollectColumnReferences(ICollection<string> columnNames)
	{
		foreach (PatternNode part in _parts)
		{
			part.CollectColumnReferences(columnNames);
		}
	}

	/// <summary>
	///	Combines child template sets in concatenation order.
	/// </summary>
	/// <returns>
	///	A template set representing every combined shape of the child nodes.
	/// </returns>
	/// <exception cref="PatternSyntaxException">
	///	Thrown when a child cannot be translated, or when concatenation creates too many shapes.
	/// </exception>
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

	/// <summary>
	///	Creates a node that randomly chooses one child option for OR or |.
	/// </summary>
	/// <param name="options">
	///	The options to choose from with equal probability.
	/// </param>
	public ChoicePatternNode(IReadOnlyList<PatternNode> options)
	{
		_options = options;
	}

	/// <summary>
	///	Appends one randomly selected option.
	/// </summary>
	/// <param name="builder">
	///	The builder receiving the selected option's text.
	/// </param>
	/// <param name="context">
	///	The row context that supplies the random source and is passed to the selected option.
	/// </param>
	public override void Append(StringBuilder builder, PatternContext context)
		=> _options[context.Random.Next(_options.Count)].Append(builder, context);

	/// <summary>
	///	Collects column references from all choice options.
	/// </summary>
	/// <param name="columnNames">
	///	The collection receiving referenced column names.
	/// </param>
	public override void CollectColumnReferences(ICollection<string> columnNames)
	{
		foreach (PatternNode option in _options)
		{
			option.CollectColumnReferences(columnNames);
		}
	}

	/// <summary>
	///	Unions the template sets from all choice options.
	/// </summary>
	/// <returns>
	///	A template set matching any option's possible output.
	/// </returns>
	/// <exception cref="PatternSyntaxException">
	///	Thrown when an option cannot be translated, or when there are too many shapes.
	/// </exception>
	public override PatternTemplateSet ExpandTemplates()
		=> PatternTemplateSet.Union([.. _options.Select(option => option.ExpandTemplates())]);
}

internal sealed class RepetitionPatternNode : PatternNode
{
	private readonly PatternNode _node;
	private readonly int         _minimumCount;
	private readonly int         _maximumCount;

	/// <summary>
	///	Creates a node that repeats another node a fixed or random number of times.
	/// </summary>
	/// <param name="node">
	///	The child node to repeat.
	/// </param>
	/// <param name="minimumCount">
	///	The smallest number of repetitions.
	/// </param>
	/// <param name="maximumCount">
	///	The largest number of repetitions.
	/// </param>
	public RepetitionPatternNode(PatternNode node, int minimumCount, int maximumCount)
	{
		_node         = node;
		_minimumCount = minimumCount;
		_maximumCount = maximumCount;
	}

	/// <summary>
	///	Chooses a repetition count and appends the child node that many times.
	/// </summary>
	/// <param name="builder">
	///	The builder receiving the repeated text.
	/// </param>
	/// <param name="context">
	///	The row context that supplies the random source and is passed to the child node.
	/// </param>
	/// <exception cref="InvalidOperationException">
	///	Thrown when the repeated output exceeds the maximum allowed length.
	/// </exception>
	public override void Append(StringBuilder builder, PatternContext context)
	{
		int count = context.Random.Next(_minimumCount, _maximumCount + 1);

		for (int repetition = 0; repetition < count; ++repetition)
		{
			_node.Append(builder, context);
			PatternContext.EnsureLength(builder);
		}
	}

	/// <summary>
	///	Collects column references from the repeated child node.
	/// </summary>
	/// <param name="columnNames">
	///	The collection receiving referenced column names.
	/// </param>
	public override void CollectColumnReferences(ICollection<string> columnNames) => _node.CollectColumnReferences(columnNames);

	/// <summary>
	///	Builds templates for every allowed repetition count of the child node.
	/// </summary>
	/// <returns>
	///	A template set matching every repeated shape from the minimum count to the maximum count.
	/// </returns>
	/// <exception cref="PatternSyntaxException">
	///	Thrown when the child cannot be translated, or when repetition creates too many shapes.
	/// </exception>
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