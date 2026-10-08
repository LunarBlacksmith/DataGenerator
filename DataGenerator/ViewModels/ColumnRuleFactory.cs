using DataGenerator.Models;

namespace DataGenerator.ViewModels;

/// <summary>
///	Creates the column rules of a new row set, linking each column to the table it references (declared or inferred) and
///	applying the saved settings that are marked to be used automatically.
/// </summary>
public sealed class ColumnRuleFactory
{
	#region FIELDS
	private readonly ColumnRuleServices _services;
	#endregion FIELDS

	#region CONSTRUCTOR
	/// <summary>
	///	Creates a rule factory that uses the shared column-rule services.
	/// </summary>
	/// <param name="services">
	///	The services passed to each created column rule.
	/// </param>
	/// <exception cref="ArgumentNullException">
	///	Thrown when <paramref name="services"/> is <see langword="null"/>.
	/// </exception>
	public ColumnRuleFactory(ColumnRuleServices services)
	{
		_services = services ?? throw new ArgumentNullException(nameof(services));
	}
	#endregion CONSTRUCTOR

	#region METHODS
	#region PUBLIC
	/// <summary>
	///	Creates one rule for each column of a table and attaches its declared or inferred reference.
	/// </summary>
	/// <param name="table">
	///	The table whose columns need generation rules.
	/// </param>
	/// <param name="isUpdate">
	///	Whether the rules are for an update set; their columns keep their current values until changed.
	/// </param>
	/// <returns>
	///	The created rules, in the same order as the table columns.
	/// </returns>
	/// <exception cref="ArgumentNullException">
	///	Thrown when <paramref name="table"/> is <see langword="null"/>.
	/// </exception>
	public IReadOnlyList<ColumnRuleViewModel> CreateRules(TableModel table, bool isUpdate = false)
	{
		ArgumentNullException.ThrowIfNull(table);

		List<ColumnRuleViewModel> rules = new(table.Columns.Count);

		foreach (ColumnModel column in table.Columns)
		{
			ForeignKeyModel? reference       = FindReference(table, column);
			bool             isSelfReference =
				reference is not null
				&& string.Equals(reference.ReferencedTableKey, table.Key, StringComparison.OrdinalIgnoreCase);

			ColumnRuleViewModel rule = new(table, column, reference, isSelfReference, isUpdate, _services);

			// Update sets only change the columns the user chooses.
			if (!isUpdate)
			{
				_ = rule.ApplyAutomaticSetting();
			}

			rules.Add(rule);
		}

		return rules;
	}
	#endregion PUBLIC

	#region PRIVATE
	// Declared foreign keys win over relationships inferred from the FTK naming convention.
	/// <summary>
	///	Finds the foreign key that belongs to a column, preferring declared keys over inferred ones.
	/// </summary>
	/// <param name="table">
	///	The table that owns the column and its foreign-key metadata.
	/// </param>
	/// <param name="column">
	///	The column whose reference is being looked up.
	/// </param>
	/// <returns>
	///	The matching reference, or <see langword="null"/> when the column has none.
	/// </returns>
	private static ForeignKeyModel? FindReference(TableModel table, ColumnModel column)
		=>
			table
				.ForeignKeys
				.Where(
					foreignKey => string.Equals(
						foreignKey.ParentColumn,
						column.Name,
						StringComparison.OrdinalIgnoreCase
					)
				)
				.OrderBy(foreignKey => foreignKey.IsInferred)
				.FirstOrDefault();
	#endregion PRIVATE
	#endregion METHODS
}