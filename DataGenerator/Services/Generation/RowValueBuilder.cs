using DataGenerator.Interfaces;
using DataGenerator.Models;

namespace DataGenerator.Services.Generation;

/// <summary>
/// Produces the values of one inserted row from a <see cref="RowSetBlueprint"/>.
/// </summary>
internal sealed class RowValueBuilder
{
	private readonly IColumnValueGenerator _valueGenerator;

	public RowValueBuilder(IColumnValueGenerator valueGenerator)
	{
		_valueGenerator = valueGenerator ?? throw new ArgumentNullException(nameof(valueGenerator));
	}

	/// <param name="rowIndex">Zero-based index of the row within its row set.</param>
	public object?[] Build(TableBlueprint table, RowSetBlueprint rowSet, long rowIndex)
	{
		object?[]      values           = new object?[rowSet.Sources.Count];
		int[]          generatedChoices = CreateChoices(rowSet.GeneratedKeyGroupCount);
		int[]          existingChoices  = CreateChoices(rowSet.ExistingKeyGroupCount);
		RowValueLookup lookup           = new(rowSet, values);

		foreach (int index in rowSet.EvaluationOrder)
		{
			ValueSource source = rowSet.Sources[index];

			try
			{
				values[index] = source.Kind switch
				{
					ValueSourceKind.GeneratedKey => GetGeneratedKey(source, generatedChoices),
					ValueSourceKind.ExistingKey  => GetExistingKey(source, existingChoices),
					ValueSourceKind.Lookup       => source.Lookup!.GetValue(rowIndex, Random.Shared),
					_                            => _valueGenerator.Generate(source.Rule, rowIndex, rowSet.Plan.RowCount, lookup)
				};

				lookup.MarkGenerated(index);
			}
			catch (Exception exception) when (exception is not OperationCanceledException and not DataGenerationException)
			{
				throw new DataGenerationException(
					exception.Message,
					GenerationBlueprintBuilder.DescribeLocation(table.Table, rowSet.Plan, rowIndex, source.Rule.Column),
					exception
				);
			}
		}

		return values;
	}

	/// <summary>
	/// Returns the captured key values of a row (null entries are produced by SQL Server).
	/// </summary>
	public static object?[] GetKeyValues(RowSetBlueprint rowSet, object?[] values)
	{
		object?[] keyValues = new object?[rowSet.KeySourceIndexes.Count];

		for (int index = 0; index < keyValues.Length; ++index)
		{
			int sourceIndex = rowSet.KeySourceIndexes[index];

			keyValues[index] = sourceIndex < 0 ? null : values[sourceIndex];
		}

		return keyValues;
	}

	private static int[] CreateChoices(int count)
	{
		int[] choices = new int[count];

		Array.Fill(choices, -1);

		return choices;
	}

	private static object? GetGeneratedKey(ValueSource source, int[] choices)
	{
		GeneratedKeyTable keys = source.KeyTable!;

		if (choices[source.GroupIndex] < 0)
		{
			if (keys.RowCount == 0)
			{
				throw new InvalidOperationException($"No rows were generated for {keys.Table.DisplayName}, so there are no keys to reference.");
			}

			choices[source.GroupIndex] = Random.Shared.Next(keys.RowCount);
		}

		return keys.GetValue(choices[source.GroupIndex], source.ColumnIndex);
	}

	/// <summary>
	/// The values generated so far for the row, for rules that use the value of another column.
	/// </summary>
	private sealed class RowValueLookup : IRowValueLookup
	{
		private readonly RowSetBlueprint _rowSet;
		private readonly object?[]       _values;
		private readonly bool[]          _isGenerated;

		public RowValueLookup(RowSetBlueprint rowSet, object?[] values)
		{
			_rowSet      = rowSet;
			_values      = values;
			_isGenerated = new bool[values.Length];
		}

		public void MarkGenerated(int index) => _isGenerated[index] = true;

		public object? GetValue(string columnName)
			=> !_rowSet.SourceIndexesByName.TryGetValue(columnName, out int index)
					? throw new InvalidOperationException($"Column [{columnName}] is not inserted by this row set, so its value cannot be used.")
					: _isGenerated[index]
						? _values[index]
						: throw new InvalidOperationException($"The value of column [{columnName}] has not been generated yet.");
	}

	private static object? GetExistingKey(ValueSource source, int[] choices)
	{
		ExistingKeyPool pool = source.Pool!;

		if (choices[source.GroupIndex] < 0)
		{
			choices[source.GroupIndex] = pool.Choose(Random.Shared);
		}

		return pool.GetValue(choices[source.GroupIndex], source.ColumnIndex);
	}
}