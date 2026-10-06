using DataGenerator.Models;

namespace DataGenerator.ViewModels;

/// <summary>
/// Creates the column rules of a new row set, linking each column to the table it references (declared or inferred) and
/// applying the saved settings that are marked to be used automatically.
/// </summary>
public sealed class ColumnRuleFactory
{
	private readonly ColumnRuleServices _services;

	public ColumnRuleFactory(ColumnRuleServices services)
	{
		_services = services ?? throw new ArgumentNullException(nameof(services));
	}

	/// <param name="isUpdate">Whether the rules are for an update set; their columns keep their current values until changed.</param>
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

	// Declared foreign keys win over relationships inferred from the FTK naming convention.
	private static ForeignKeyModel? FindReference(TableModel table, ColumnModel column)
		=> table.ForeignKeys
			.Where(foreignKey => string.Equals(foreignKey.ParentColumn, column.Name, StringComparison.OrdinalIgnoreCase))
			.OrderBy(foreignKey => foreignKey.IsInferred)
			.FirstOrDefault();
}