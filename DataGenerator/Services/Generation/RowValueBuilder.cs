using DataGenerator.Interfaces;
using DataGenerator.Models;

namespace DataGenerator.Services.Generation;

/// <summary>
///	Produces the values of one inserted row from a <see cref="RowSetBlueprint"/>.
/// </summary>
internal sealed class RowValueBuilder
{
	#region FIELDS
	#region PRIVATE
	private readonly IColumnValueGenerator _valueGenerator;
	#endregion PRIVATE
	#endregion FIELDS

	#region CONSTRUCTORS
	#region PUBLIC
	/// <summary>
	///	Creates the service that produces row values from row-set blueprints.
	/// </summary>
	/// <param name="valueGenerator">
	///	The generator used for rule-based column values.
	/// </param>
	/// <exception cref="ArgumentNullException">
	///	Thrown when <paramref name="valueGenerator"/> is <see langword="null"/>.
	/// </exception>
	public RowValueBuilder(IColumnValueGenerator valueGenerator)
	{
		_valueGenerator = valueGenerator ?? throw new ArgumentNullException(nameof(valueGenerator));
	}
	#endregion PUBLIC
	#endregion CONSTRUCTORS

	#region METHODS
	#region PUBLIC
	/// <summary>
	///	Returns the captured key values of a row (null entries are produced by SQL Server).
	/// </summary>
	/// <param name="rowSet">
	///	The row set whose key-source indexes identify captured values.
	/// </param>
	/// <param name="values">
	///	The generated values for the row.
	/// </param>
	/// <returns>
	///	The key values in generated-key table column order.
	/// </returns>
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

	/// <summary>
	///	Builds the values of one inserted or staged update row in dependency order.
	/// </summary>
	/// <param name="table">
	///	The table blueprint that owns the row set.
	/// </param>
	/// <param name="rowSet">
	///	The row-set blueprint that describes value sources.
	/// </param>
	/// <param name="rowIndex">
	///	Zero-based index of the row within its row set.
	/// </param>
	/// <returns>
	///	The generated values in source-column order.
	/// </returns>
	/// <exception cref="DataGenerationException">
	///	Thrown when a value cannot be generated for the row.
	/// </exception>
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
	#endregion PUBLIC

	#region PRIVATE
	/// <summary>
	///	Creates a choice array initialised to show that no shared group has chosen a row yet.
	/// </summary>
	/// <param name="count">
	///	The number of shared choice groups.
	/// </param>
	/// <returns>
	///	An array of <c>-1</c> values, one per group.
	/// </returns>
	private static int[] CreateChoices(int count)
	{
		int[] choices = new int[count];

		Array.Fill(choices, -1);

		return choices;
	}

	/// <summary>
	///	Gets a value from a generated parent key table, choosing one parent row per shared group.
	/// </summary>
	/// <param name="source">
	///	The generated-key value source.
	/// </param>
	/// <param name="choices">
	///	The chosen parent row indexes for generated-key groups.
	/// </param>
	/// <returns>
	///	The generated key value for the source column.
	/// </returns>
	/// <exception cref="InvalidOperationException">
	///	Thrown when no rows were generated for the referenced table.
	/// </exception>
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
	///	Gets a value from an existing-key pool, choosing one referenced row per shared group.
	/// </summary>
	/// <param name="source">
	///	The existing-key value source.
	/// </param>
	/// <param name="choices">
	///	The chosen sampled row indexes for existing-key groups.
	/// </param>
	/// <returns>
	///	The existing key value for the source column.
	/// </returns>
	private static object? GetExistingKey(ValueSource source, int[] choices)
	{
		ExistingKeyPool pool = source.Pool!;

		if (choices[source.GroupIndex] < 0)
		{
			choices[source.GroupIndex] = pool.Choose(Random.Shared);
		}

		return pool.GetValue(choices[source.GroupIndex], source.ColumnIndex);
	}
	#endregion PRIVATE
	#endregion METHODS

	#region TYPES
	#region PRIVATE
	/// <summary>
	///	The values generated so far for the row, for rules that use the value of another column.
	/// </summary>
	private sealed class RowValueLookup : IRowValueLookup
	{
		#region FIELDS
		#region PRIVATE
		private readonly RowSetBlueprint _rowSet;
		private readonly object?[]       _values;
		private readonly bool[]          _isGenerated;
		#endregion PRIVATE
		#endregion FIELDS

		#region CONSTRUCTORS
		#region PUBLIC
		/// <summary>
		///	Creates a lookup over values generated so far for one row.
		/// </summary>
		/// <param name="rowSet">
		///	The row set whose source indexes map column names to value indexes.
		/// </param>
		/// <param name="values">
		///	The values being generated for the row.
		/// </param>
		public RowValueLookup(RowSetBlueprint rowSet, object?[] values)
		{
			_rowSet      = rowSet;
			_values      = values;
			_isGenerated = new bool[values.Length];
		}
		#endregion PUBLIC
		#endregion CONSTRUCTORS

		#region METHODS
		#region PUBLIC
		/// <summary>
		///	Marks a source value as available to later column rules.
		/// </summary>
		/// <param name="index">
		///	The source index that has just been generated.
		/// </param>
		public void MarkGenerated(int index) => _isGenerated[index] = true;

		/// <summary>
		///	Gets the value already generated for another column in the same row.
		/// </summary>
		/// <param name="columnName">
		///	The column name requested by the rule.
		/// </param>
		/// <returns>
		///	The generated column value, which may be <see langword="null"/>.
		/// </returns>
		/// <exception cref="InvalidOperationException">
		///	Thrown when the column is not inserted by the row set or has not been generated yet.
		/// </exception>
		public object? GetValue(string columnName)
			=>
				!_rowSet.SourceIndexesByName.TryGetValue(columnName, out int index)
					? throw new InvalidOperationException($"Column [{columnName}] is not inserted by this row set, so its value cannot be used.")
					: _isGenerated[index]
						? _values[index]
						: throw new InvalidOperationException($"The value of column [{columnName}] has not been generated yet.");
		#endregion PUBLIC
		#endregion METHODS
	}
	#endregion PRIVATE
	#endregion TYPES
}