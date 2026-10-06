using System.Collections.ObjectModel;
using DataGenerator.Interfaces;
using DataGenerator.Models;
using Microsoft.Data.SqlClient;

namespace DataGenerator.Services;

public sealed class SqlMetadataService : ISqlMetadataService
{
	public async Task<IReadOnlyList<DatabaseModel>> LoadMetadataAsync(
		string             connectionString,
		IProgress<string>? progress,
		CancellationToken  cancellationToken
	)
	{
		List<DatabaseModel> databases     = [];
		List<string>        databaseNames = [];

		await using SqlConnection masterConnection = new(connectionString);
		await masterConnection.OpenAsync(cancellationToken);

		const string DATABASE_QUERY =
			"""
			SELECT [name]
			FROM sys.databases
			WHERE [state] = 0
				AND HAS_DBACCESS([name]) = 1
				AND [name] NOT IN ('master', 'tempdb', 'model', 'msdb')
			ORDER BY [name];
			""";

		await using SqlCommand    databaseCommand = new(DATABASE_QUERY, masterConnection);
		await using SqlDataReader databaseReader  = await databaseCommand.ExecuteReaderAsync(cancellationToken);


		while (await databaseReader.ReadAsync(cancellationToken))
		{
			databaseNames.Add(databaseReader.GetString(0));
		}

		await databaseReader.CloseAsync();

		foreach (string databaseName in databaseNames)
		{
			cancellationToken.ThrowIfCancellationRequested();
			progress?.Report($"Loading schema for {databaseName}...");

			DatabaseModel database = new()
			{
				Name = databaseName
			};

			SqlConnectionStringBuilder builder = new(connectionString)
			{
				InitialCatalog = databaseName
			};

			await LoadDatabaseAsync(
				builder.ConnectionString,
				database,
				cancellationToken
			);

			databases.Add(database);
		}

		progress?.Report($"Loaded {databases.Count} accessible database(s).");
		return databases;
	}

	private static async Task LoadDatabaseAsync(
		string connectionString,
		DatabaseModel database,
		CancellationToken cancellationToken
	)
	{
		await using SqlConnection connection = new(connectionString);
		await connection.OpenAsync(cancellationToken);

		const string COLUMN_QUERY =
			"""
			SELECT
				s.[name] AS SchemaName,
				t.[name] AS TableName,
				c.[name] AS ColumnName,
				st.SqlType,
				CASE
					WHEN c.max_length = -1								THEN -1
					WHEN st.SqlType IN ('nvarchar', 'nchar')	THEN c.max_length / 2
					ELSE c.max_length
				END AS MaximumLength,
				c.[precision],
				c.[scale],
				c.is_nullable,
				c.is_identity,
				c.is_computed,
				CASE WHEN pk.column_id IS NULL THEN 0 ELSE 1 END AS IsPrimaryKey,
				CASE WHEN c.default_object_id = 0 THEN 0 ELSE 1 END AS HasDefault
			FROM sys.tables t
			INNER JOIN sys.schemas s ON s.schema_id = t.schema_id
			INNER JOIN sys.columns c ON c.object_id = t.object_id
			INNER JOIN sys.types ty ON ty.user_type_id = c.user_type_id
			LEFT JOIN sys.types bt ON bt.user_type_id = ty.system_type_id
			CROSS APPLY
			(
				-- Alias types (CREATE TYPE ... FROM nvarchar(50)) are resolved to their underlying system type.
				SELECT CASE
					WHEN ty.is_user_defined = 1 AND ty.is_assembly_type = 0 AND bt.[name] IS NOT NULL THEN bt.[name]
					ELSE ty.[name]
				END AS SqlType
			) st
			LEFT JOIN
			(
				SELECT ic.object_id, ic.column_id
				FROM sys.indexes i
				INNER JOIN sys.index_columns ic
					ON ic.object_id = i.object_id
					AND ic.index_id = i.index_id
				WHERE i.is_primary_key = 1
			) pk
				ON pk.object_id = c.object_id
				AND pk.column_id = c.column_id
			WHERE t.is_ms_shipped = 0
			ORDER BY s.[name], t.[name], c.column_id;
			""";

		Dictionary<string, TableModel> tableLookup   = new(StringComparer.OrdinalIgnoreCase);
		await using SqlCommand         columnCommand = new(COLUMN_QUERY, connection);
		await using SqlDataReader      reader        = await columnCommand.ExecuteReaderAsync(cancellationToken);

		while (await reader.ReadAsync(cancellationToken))
		{
			string schemaName = reader.GetString(0);
			string tableName  = reader.GetString(1);
			string key        = $"{schemaName}.{tableName}";

			if (!tableLookup.TryGetValue(key, out TableModel? table))
			{
				table = new TableModel
				{
					DatabaseName = database.Name,
					SchemaName   = schemaName,
					Name         = tableName
				};

				tableLookup.Add(key, table);
				database.Tables.Add(table);
			}

			ColumnModel column = new()
			{
				Name          = reader.GetString(2),
				SqlType       = reader.GetString(3),
				MaximumLength = reader.IsDBNull(4) ? null : reader.GetInt32(4),
				Precision     = reader.IsDBNull(5) ? null : reader.GetByte(5),
				Scale         = reader.IsDBNull(6) ? null : reader.GetByte(6),
				IsNullable    = reader.GetBoolean(7),
				IsIdentity    = reader.GetBoolean(8),
				IsComputed    = reader.GetBoolean(9),
				IsPrimaryKey  = Convert.ToBoolean(reader.GetValue(10)),
				HasDefault    = Convert.ToBoolean(reader.GetValue(11))
			};

			table.Columns.Add(column);
		}

		await reader.CloseAsync();

		const string FOREIGN_KEY_QUERY =
			"""
			SELECT
				fk.[name],
				ps.[name] AS ParentSchema,
				pt.[name] AS ParentTable,
				pc.[name] AS ParentColumn,
				rs.[name] AS ReferencedSchema,
				rt.[name] AS ReferencedTable,
				rc.[name] AS ReferencedColumn
			FROM sys.foreign_keys fk
			INNER JOIN sys.foreign_key_columns fkc
				ON fkc.constraint_object_id = fk.object_id
			INNER JOIN sys.tables pt
				ON pt.object_id = fkc.parent_object_id
			INNER JOIN sys.schemas ps
				ON ps.schema_id = pt.schema_id
			INNER JOIN sys.columns pc
				ON pc.object_id = pt.object_id
				AND pc.column_id = fkc.parent_column_id
			INNER JOIN sys.tables rt
				ON rt.object_id = fkc.referenced_object_id
			INNER JOIN sys.schemas rs
				ON rs.schema_id = rt.schema_id
			INNER JOIN sys.columns rc
				ON rc.object_id = rt.object_id
				AND rc.column_id = fkc.referenced_column_id
			ORDER BY fk.[name], fkc.constraint_column_id;
			""";

		await using SqlCommand    foreignKeyCommand = new(FOREIGN_KEY_QUERY, connection);
		await using SqlDataReader foreignKeyReader  = await foreignKeyCommand.ExecuteReaderAsync(cancellationToken);

		while (await foreignKeyReader.ReadAsync(cancellationToken))
		{
			string parentSchema = foreignKeyReader.GetString(1);
			string parentTable  = foreignKeyReader.GetString(2);
			string parentColumn = foreignKeyReader.GetString(3);
			string tableKey     = $"{parentSchema}.{parentTable}";

			if (!tableLookup.TryGetValue(tableKey, out TableModel? table))
			{
				continue;
			}

			table.ForeignKeys.Add(new ForeignKeyModel
			{
				Name               = foreignKeyReader.GetString(0),
				ParentDatabase     = database.Name,
				ParentSchema       = parentSchema,
				ParentTable        = parentTable,
				ParentColumn       = parentColumn,
				ReferencedDatabase = database.Name,
				ReferencedSchema   = foreignKeyReader.GetString(4),
				ReferencedTable    = foreignKeyReader.GetString(5),
				ReferencedColumn   = foreignKeyReader.GetString(6)
			});

			ColumnModel? column = 
				table
					.Columns
					.FirstOrDefault(
						item => string.Equals(
							item.Name,
							parentColumn,
							StringComparison.OrdinalIgnoreCase
						)
					);

			if (column is not null)
			{
				column.IsForeignKey = true;
			}
		}
	}
}
