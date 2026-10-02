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

	public IReadOnlyList<ColumnRuleViewModel> CreateRules(TableModel table)
	{
		ArgumentNullException.ThrowIfNull(table);

		List<ColumnRuleViewModel> rules = new List<ColumnRuleViewModel>(table.Columns.Count);

		foreach (ColumnModel column in table.Columns)
		{
			ForeignKeyModel? reference       = FindReference(table, column);
			bool             isSelfReference =
				reference is not null
				&& string.Equals(reference.ReferencedTableKey, table.Key, StringComparison.OrdinalIgnoreCase);

			ColumnRuleViewModel rule = new ColumnRuleViewModel(table.DisplayName, column, reference, isSelfReference, _services);

			_ = rule.ApplyAutomaticSetting();
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