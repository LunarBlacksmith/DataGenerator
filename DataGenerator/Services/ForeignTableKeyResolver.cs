using DataGenerator.Models;

namespace DataGenerator.Services;

public sealed class ForeignTableKeyResolver : IForeignTableKeyResolver
{
	public const string PRIMARY_KEY_SUFFIX  = "PK";
	public const string INFERRED_KEY_PREFIX = "inferred:";

	public int ResolveInferredKeys(IReadOnlyList<DatabaseModel> databases)
	{
		ArgumentNullException.ThrowIfNull(databases);

		int inferredCount = 0;

		foreach (DatabaseModel database in databases)
		{
			Dictionary<string, List<KeyCandidate>> candidatesByName = BuildCandidateLookup(database);

			foreach (TableModel table in database.Tables)
			{
				foreach (ColumnModel column in table.Columns)
				{
					if (!column.IsForeignTableKey || column.IsForeignKey || HasForeignKey(table, column))
					{
						continue;
					}

					string prefix = column.Name[..^ColumnModel.FOREIGN_TABLE_KEY_SUFFIX.Length];

					if (!candidatesByName.TryGetValue(prefix + PRIMARY_KEY_SUFFIX, out List<KeyCandidate>? candidates))
					{
						continue;
					}

					KeyCandidate? target = candidates
						.Where(candidate => !ReferenceEquals(candidate.Table, table))
						.OrderByDescending(candidate => candidate.Column.IsPrimaryKey)
						.ThenByDescending(candidate => string.Equals(candidate.Table.SchemaName, table.SchemaName, StringComparison.OrdinalIgnoreCase))
						.ThenBy(candidate => candidate.Table.SchemaName, StringComparer.OrdinalIgnoreCase)
						.ThenBy(candidate => candidate.Table.Name, StringComparer.OrdinalIgnoreCase)
						.FirstOrDefault();

					if (target is null)
					{
						continue;
					}

					table.ForeignKeys.Add(new ForeignKeyModel
					{
						Name               = $"{INFERRED_KEY_PREFIX}{table.SchemaName}.{table.Name}.{column.Name}",
						ParentDatabase     = database.Name,
						ParentSchema       = table.SchemaName,
						ParentTable        = table.Name,
						ParentColumn       = column.Name,
						ReferencedDatabase = database.Name,
						ReferencedSchema   = target.Table.SchemaName,
						ReferencedTable    = target.Table.Name,
						ReferencedColumn   = target.Column.Name,
						IsInferred         = true
					});

					++inferredCount;
				}
			}
		}

		return inferredCount;
	}

	private static Dictionary<string, List<KeyCandidate>> BuildCandidateLookup(DatabaseModel database)
	{
		Dictionary<string, List<KeyCandidate>> candidatesByName = new Dictionary<string, List<KeyCandidate>>(StringComparer.OrdinalIgnoreCase);

		foreach (TableModel table in database.Tables)
		{
			foreach (ColumnModel column in table.Columns)
			{
				if (column.Name.Length <= PRIMARY_KEY_SUFFIX.Length
					|| !column.Name.EndsWith(PRIMARY_KEY_SUFFIX, StringComparison.OrdinalIgnoreCase))
				{
					continue;
				}

				if (!candidatesByName.TryGetValue(column.Name, out List<KeyCandidate>? candidates))
				{
					candidates                    = [];
					candidatesByName[column.Name] = candidates;
				}

				candidates.Add(new KeyCandidate(table, column));
			}
		}

		return candidatesByName;
	}

	private static bool HasForeignKey(TableModel table, ColumnModel column)
		=> table.ForeignKeys.Any(foreignKey => string.Equals(foreignKey.ParentColumn, column.Name, StringComparison.OrdinalIgnoreCase));

	private sealed record KeyCandidate(TableModel Table, ColumnModel Column);
}