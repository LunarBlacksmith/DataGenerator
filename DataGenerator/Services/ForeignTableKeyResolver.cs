using DataGenerator.Interfaces;
using DataGenerator.Models;

namespace DataGenerator.Services;

/// <summary>
/// Links "ftk" columns (e.g. tagFTK or tag_ftk) to the key column with the same base name in another table of the
/// same database. A key column is one whose name ends in "PK" (any casing), in a lower-case letter followed by "TK"
/// (e.g. tagTK) or in "_tk" (any casing), e.g. tagPK, tagTK, tag_PK or tag_tk.
/// </summary>
public sealed class ForeignTableKeyResolver : IForeignTableKeyResolver
{
	public const string PRIMARY_KEY_SUFFIX    = "PK";
	public const string TABLE_KEY_SUFFIX      = "TK";
	public const string UNDERSCORE_KEY_SUFFIX = "_tk";
	public const string INFERRED_KEY_PREFIX   = "inferred:";

	private const char NAME_SEPARATOR = '_';

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

					string baseName = TrimSeparator(column.Name[..^ColumnModel.FOREIGN_TABLE_KEY_SUFFIX.Length]);

					if (baseName.Length == 0 || !candidatesByName.TryGetValue(baseName, out List<KeyCandidate>? candidates))
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
		Dictionary<string, List<KeyCandidate>> candidatesByName = new(StringComparer.OrdinalIgnoreCase);

		foreach (TableModel table in database.Tables)
		{
			foreach (ColumnModel column in table.Columns)
			{
				if (column.IsForeignTableKey || !TryGetKeyBaseName(column.Name, out string baseName))
				{
					continue;
				}

				if (!candidatesByName.TryGetValue(baseName, out List<KeyCandidate>? candidates))
				{
					candidates                 = [];
					candidatesByName[baseName] = candidates;
				}

				candidates.Add(new KeyCandidate(table, column));
			}
		}

		return candidatesByName;
	}

	/// <summary>
	/// The name of a key column without its key suffix and separator, e.g. "tag" for tagPK, tagTK, tag_PK and tag_tk.
	/// </summary>
	public static bool TryGetKeyBaseName(string columnName, out string baseName)
	{
		ArgumentNullException.ThrowIfNull(columnName);

		string? stripped = null;

		if (columnName.EndsWith(UNDERSCORE_KEY_SUFFIX, StringComparison.OrdinalIgnoreCase))
		{
			stripped = columnName[..^UNDERSCORE_KEY_SUFFIX.Length];
		}
		else if (columnName.EndsWith(PRIMARY_KEY_SUFFIX, StringComparison.OrdinalIgnoreCase))
		{
			stripped = columnName[..^PRIMARY_KEY_SUFFIX.Length];
		}
		else if (columnName.Length > TABLE_KEY_SUFFIX.Length
			&& columnName.EndsWith(TABLE_KEY_SUFFIX, StringComparison.Ordinal)
			&& char.IsLower(columnName[^(TABLE_KEY_SUFFIX.Length + 1)]))
		{
			stripped = columnName[..^TABLE_KEY_SUFFIX.Length];
		}

		baseName = stripped is null ? string.Empty : TrimSeparator(stripped);
		return baseName.Length > 0;
	}

	private static string TrimSeparator(string name) => name.TrimEnd(NAME_SEPARATOR);

	private static bool HasForeignKey(TableModel table, ColumnModel column)
		=> table.ForeignKeys.Any(foreignKey => string.Equals(foreignKey.ParentColumn, column.Name, StringComparison.OrdinalIgnoreCase));

	private sealed record KeyCandidate(TableModel Table, ColumnModel Column);
}