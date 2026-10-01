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
		object?[] values           = new object?[rowSet.Sources.Count];
		int[]     generatedChoices = CreateChoices(rowSet.GeneratedKeyGroupCount);
		int[]     existingChoices  = CreateChoices(rowSet.ExistingKeyGroupCount);

		for (int index = 0; index < rowSet.Sources.Count; ++index)
		{
			ValueSource source = rowSet.Sources[index];

			try
			{
				values[index] = source.Kind switch
				{
					ValueSourceKind.GeneratedKey => GetGeneratedKey(source, generatedChoices),
					ValueSourceKind.ExistingKey  => GetExistingKey(source, existingChoices),
					_                            => _valueGenerator.Generate(source.Rule, rowIndex)
				};
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