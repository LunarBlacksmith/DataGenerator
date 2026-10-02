using System.Data;
using System.Text;
using DataGenerator.Models;
using Microsoft.Data.SqlClient;

namespace DataGenerator.Services.Generation;

/// <summary>
/// Inserts a <see cref="GenerationBlueprint"/> directly into SQL Server inside one transaction.
/// </summary>
internal sealed class DirectDataInserter
{
	private const int    COMMAND_TIMEOUT_SECONDS         = 120;
	private const int    CLEANUP_TIMEOUT_SECONDS         = 600;
	private const int    POST_GENERATION_TIMEOUT_SECONDS = 600;
	private const int    MAXIMUM_PARAMETERS              = 2_000;
	private const int    MAXIMUM_BATCH_ROWS              = 100;
	private const byte   DEFAULT_PRECISION               = 18;
	private const byte   DEFAULT_SCALE                   = 0;
	private const byte   DEFAULT_TIME_SCALE              = 7;
	private const string INSERTED_KEYS_VARIABLE          = "@dg_inserted";

	private readonly ISqlValueConverter _converter;
	private readonly RowValueBuilder    _rowValueBuilder;

	public DirectDataInserter(ISqlValueConverter converter, RowValueBuilder rowValueBuilder)
	{
		_converter       = converter       ?? throw new ArgumentNullException(nameof(converter));
		_rowValueBuilder = rowValueBuilder ?? throw new ArgumentNullException(nameof(rowValueBuilder));
	}

	public async Task InsertAsync(
		GenerationBlueprint blueprint,
		string              connectionString,
		GenerationProgress  progress,
		CancellationToken   cancellationToken
	)
	{
		progress.Report("Connecting to SQL Server…");

		await using SqlConnection connection = new SqlConnection(connectionString);
		await connection.OpenAsync(cancellationToken);

		await using SqlTransaction transaction = (SqlTransaction)await connection.BeginTransactionAsync(
			IsolationLevel.ReadCommitted,
			cancellationToken
		);

		try
		{
			await ClearTablesAsync(connection, transaction, blueprint, progress, cancellationToken);

			HashSet<ExistingKeyPool> loadedPools = new HashSet<ExistingKeyPool>();

			foreach (TableBlueprint table in blueprint.Tables)
			{
				foreach (ExistingKeyPool pool in table.ExistingKeyPools)
				{
					if (loadedPools.Add(pool))
					{
						await LoadPoolAsync(connection, transaction, pool, progress, cancellationToken);
					}
				}

				foreach (RowSetBlueprint rowSet in table.RowSets)
				{
					await InsertRowSetAsync(connection, transaction, table, rowSet, progress, cancellationToken);
				}
			}

			await RunPostGenerationAsync(connection, transaction, blueprint, progress, cancellationToken);

			cancellationToken.ThrowIfCancellationRequested();
			progress.Report("Committing the transaction…");

			await transaction.CommitAsync(CancellationToken.None);
		}
		catch
		{
			try
			{
				await transaction.RollbackAsync(CancellationToken.None);
			}
			catch (Exception exception) when (exception is InvalidOperationException or SqlException)
			{
				// The transaction may already be rolled back by SQL Server; preserve the original exception.
			}

			throw;
		}
	}

	private static async Task ClearTablesAsync(
		SqlConnection       connection,
		SqlTransaction      transaction,
		GenerationBlueprint blueprint,
		GenerationProgress  progress,
		CancellationToken   cancellationToken
	)
	{
		foreach (TableModel table in blueprint.TablesToClear)
		{
			progress.Report($"Deleting existing rows of {table.FullyQualifiedName}…");

			await ExecuteCleanupAsync(
				connection,
				transaction,
				SqlCleanupStatements.CreateDeleteStatement(table),
				table,
				cancellationToken
			);
		}

		if (!blueprint.ResetIdentitySeeds)
		{
			return;
		}

		foreach (TableModel table in blueprint.TablesToClear.Where(SqlCleanupStatements.HasIdentityColumn))
		{
			progress.Report($"Restarting the identity of {table.FullyQualifiedName}…");

			string sql = string.Join(
				Environment.NewLine,
				[SqlCleanupStatements.CreateReseedDeclaration(), .. SqlCleanupStatements.CreateReseedStatements(table)]
			);

			await ExecuteCleanupAsync(connection, transaction, sql, table, cancellationToken);
		}
	}

	/// <summary>
	/// Runs the stored procedures and SQL the user asked to run after the inserts, in the chosen database and inside the
	/// generation transaction, so a failure rolls back the inserted rows too.
	/// </summary>
	private static async Task RunPostGenerationAsync(
		SqlConnection       connection,
		SqlTransaction      transaction,
		GenerationBlueprint blueprint,
		GenerationProgress  progress,
		CancellationToken   cancellationToken
	)
	{
		if (blueprint.PostGeneration is not PostGenerationScript postGeneration)
		{
			return;
		}

		string location = $"Post-generation SQL › database {SqlSyntax.QuoteIdentifier(postGeneration.DatabaseName)}";

		await ExecutePostGenerationAsync(
			connection,
			transaction,
			$"USE {SqlSyntax.QuoteIdentifier(postGeneration.DatabaseName)};",
			location,
			cancellationToken
		);

		for (int index = 0; index < postGeneration.Statements.Count; ++index)
		{
			PostGenerationStatement statement = postGeneration.Statements[index];

			cancellationToken.ThrowIfCancellationRequested();
			progress.Report($"Running post-generation SQL {index + 1:N0} of {postGeneration.Statements.Count:N0} ({statement.Name})…");

			await ExecutePostGenerationAsync(
				connection,
				transaction,
				statement.Sql,
				$"{location} › {statement.Description}",
				cancellationToken
			);
		}
	}

	private static async Task ExecutePostGenerationAsync(
		SqlConnection     connection,
		SqlTransaction    transaction,
		string            sql,
		string            location,
		CancellationToken cancellationToken
	)
	{
		await using SqlCommand command = CreateCommand(connection, transaction, sql, POST_GENERATION_TIMEOUT_SECONDS);

		try
		{
			_ = await command.ExecuteNonQueryAsync(cancellationToken);
		}
		catch (SqlException exception)
		{
			throw new DataGenerationException(
				$"The post-generation SQL failed, so nothing was saved. {exception.Message}",
				location,
				exception
			);
		}
	}

	private static async Task ExecuteCleanupAsync(
		SqlConnection     connection,
		SqlTransaction    transaction,
		string            sql,
		TableModel        table,
		CancellationToken cancellationToken
	)
	{
		await using SqlCommand command = CreateCommand(connection, transaction, sql, CLEANUP_TIMEOUT_SECONDS);

		try
		{
			_ = await command.ExecuteNonQueryAsync(cancellationToken);
		}
		catch (SqlException exception)
		{
			throw new DataGenerationException(
				$"The existing data could not be removed. {exception.Message}",
				table.FullyQualifiedName,
				exception
			);
		}
	}

	private async Task LoadPoolAsync(
		SqlConnection      connection,
		SqlTransaction     transaction,
		ExistingKeyPool    pool,
		GenerationProgress progress,
		CancellationToken  cancellationToken
	)
	{
		progress.Report($"Sampling existing keys of {pool.ReferencedTableName}…");

		List<object?[]> rows = [];

		await using SqlCommand command = CreateCommand(
			connection,
			transaction,
			$"{pool.BuildSelectStatement(_converter)};",
			COMMAND_TIMEOUT_SECONDS
		);

		try
		{
			await using SqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken);

			rows.AddRange(await ReadRowsAsync(reader, cancellationToken));
		}
		catch (SqlException exception)
		{
			throw new DataGenerationException(
				$"The existing keys could not be read. {exception.Message}",
				pool.ReferencedTableName,
				exception
			);
		}

		if (rows.Count == 0)
		{
			throw new DataGenerationException(pool.DescribeEmptyPool(), pool.ReferencedTableName);
		}

		pool.Load(rows);
	}

	/// <summary>
	/// Inserts the rows in parameterised multi-row batches. Tables referenced by "Generated key" rules return the
	/// stored key values through an OUTPUT clause; their order does not matter because keys are picked at random.
	/// </summary>
	private async Task InsertRowSetAsync(
		SqlConnection      connection,
		SqlTransaction     transaction,
		TableBlueprint     table,
		RowSetBlueprint    rowSet,
		GenerationProgress progress,
		CancellationToken  cancellationToken
	)
	{
		RowSetPlan      plan            = rowSet.Plan;
		int             columnCount     = rowSet.Sources.Count;
		int             rowsPerBatch    = columnCount == 0 ? 1 : Math.Clamp(MAXIMUM_PARAMETERS / columnCount, 1, MAXIMUM_BATCH_ROWS);
		List<object?[]> batch           = new List<object?[]>(rowsPerBatch);
		SqlCommand?     command         = null;
		int             commandRowCount = 0;
		long            batchStart      = 0;

		try
		{
			for (long rowIndex = 0; rowIndex < plan.RowCount; ++rowIndex)
			{
				cancellationToken.ThrowIfCancellationRequested();

				batch.Add(_rowValueBuilder.Build(table, rowSet, rowIndex));

				if (batch.Count < rowsPerBatch && rowIndex < plan.RowCount - 1)
				{
					continue;
				}

				if (command is null || commandRowCount != batch.Count)
				{
					if (command is not null)
					{
						await command.DisposeAsync();
					}

					command         = CreateInsertCommand(connection, transaction, table, rowSet, batch.Count);
					commandRowCount = batch.Count;
				}

				SetParameterValues(command, rowSet, batch);

				await ExecuteBatchAsync(command, table, rowSet, batchStart, batch.Count, cancellationToken);

				batchStart = rowIndex + 1;
				batch.Clear();

				progress.ReportRows("Inserting", table.Table.DisplayName, plan.Name, rowIndex + 1, plan.RowCount);
			}
		}
		finally
		{
			if (command is not null)
			{
				await command.DisposeAsync();
			}
		}
	}

	private SqlCommand CreateInsertCommand(
		SqlConnection   connection,
		SqlTransaction  transaction,
		TableBlueprint  table,
		RowSetBlueprint rowSet,
		int             rowCount
	)
	{
		StringBuilder      sql          = new StringBuilder();
		GeneratedKeyTable? keys         = table.Keys;
		string             valueColumns = keys is null ? string.Empty : DescribeValueColumns(keys.Columns.Count);

		if (keys is not null)
		{
			_ = sql
				.Append("DECLARE ")
				.Append(INSERTED_KEYS_VARIABLE)
				.Append(" TABLE (")
				.Append(DescribeKeyColumns(keys.Columns))
				.AppendLine(");");
		}

		_ = sql.Append("INSERT INTO ").Append(table.Table.FullyQualifiedName);

		if (rowSet.Sources.Count > 0)
		{
			_ = sql
				.Append(" (")
				.AppendJoin(", ", rowSet.Sources.Select(source => SqlSyntax.QuoteIdentifier(source.Rule.Column.Name)))
				.Append(')');
		}

		if (keys is not null)
		{
			_ = sql
				.Append(" OUTPUT ")
				.AppendJoin(", ", keys.Columns.Select(column => $"INSERTED.{SqlSyntax.QuoteIdentifier(column.Name)}"))
				.Append(" INTO ")
				.Append(INSERTED_KEYS_VARIABLE)
				.Append(" (")
				.Append(valueColumns)
				.Append(')');
		}

		if (rowSet.Sources.Count == 0)
		{
			_ = sql.AppendLine(" DEFAULT VALUES;");
		}
		else
		{
			_ = sql.Append(" VALUES ");

			for (int row = 0; row < rowCount; ++row)
			{
				_ = sql
					.Append(row == 0 ? "(" : ", (")
					.AppendJoin(", ", Enumerable.Range(0, rowSet.Sources.Count).Select(column => GetParameterName(row, column)))
					.Append(')');
			}

			_ = sql.AppendLine(";");
		}

		if (keys is not null)
		{
			_ = sql.Append("SELECT ").Append(valueColumns).Append(" FROM ").Append(INSERTED_KEYS_VARIABLE).Append(';');
		}

		SqlCommand command = CreateCommand(connection, transaction, sql.ToString(), COMMAND_TIMEOUT_SECONDS);

		for (int row = 0; row < rowCount; ++row)
		{
			for (int column = 0; column < rowSet.Sources.Count; ++column)
			{
				SqlParameter parameter = new SqlParameter
				{
					ParameterName = GetParameterName(row, column)
				};

				ConfigureParameter(parameter, rowSet.Sources[column].Rule.Column);

				_ = command.Parameters.Add(parameter);
			}
		}

		return command;
	}

	private static async Task ExecuteBatchAsync(
		SqlCommand        command,
		TableBlueprint    table,
		RowSetBlueprint   rowSet,
		long              batchStart,
		int               rowCount,
		CancellationToken cancellationToken
	)
	{
		try
		{
			if (table.Keys is null)
			{
				_ = await command.ExecuteNonQueryAsync(cancellationToken);
				return;
			}

			await using SqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken);

			foreach (object?[] keyValues in await ReadRowsAsync(reader, cancellationToken))
			{
				table.Keys.AddRow(keyValues);
			}

			while (await reader.NextResultAsync(cancellationToken))
			{
				// Surfaces errors raised after the first result set.
			}
		}
		catch (SqlException exception)
		{
			string rows = rowCount == 1
				? $"Row {batchStart + 1:N0}"
				: $"Rows {batchStart + 1:N0}–{batchStart + rowCount:N0}";

			throw new DataGenerationException(
				exception.Message,
				$"{GenerationBlueprintBuilder.DescribeLocation(table.Table, rowSet.Plan)} › {rows}",
				exception
			);
		}
	}

	private static void SetParameterValues(SqlCommand command, RowSetBlueprint rowSet, List<object?[]> batch)
	{
		int columnCount = rowSet.Sources.Count;

		for (int row = 0; row < batch.Count; ++row)
		{
			for (int column = 0; column < columnCount; ++column)
			{
				object? value = batch[row][column];

				command.Parameters[(row * columnCount) + column].Value = value switch
				{
					null        => DBNull.Value,
					SqlFragment => throw new InvalidOperationException(
						$"Column [{rowSet.Sources[column].Rule.Column.Name}] received a value that only works in SQL scripts."
					),
					_           => value
				};
			}
		}
	}

	private void ConfigureParameter(SqlParameter parameter, ColumnModel column)
	{
		parameter.SqlDbType = _converter.GetSqlDbType(column);

		switch (parameter.SqlDbType)
		{
			case SqlDbType.Char:
			case SqlDbType.NChar:
				parameter.Size = _converter.GetMaximumTextLength(column) ?? 1;
				break;

			case SqlDbType.VarChar:
			case SqlDbType.NVarChar:
				parameter.Size = _converter.GetMaximumTextLength(column) ?? -1;
				break;

			case SqlDbType.Binary:
				parameter.Size = column.MaximumLength is null or < 1 ? 1 : column.MaximumLength.Value;
				break;

			case SqlDbType.VarBinary:
				parameter.Size = column.MaximumLength is null or < 1 ? -1 : column.MaximumLength.Value;
				break;

			case SqlDbType.Decimal:
				parameter.Precision = column.Precision ?? DEFAULT_PRECISION;
				parameter.Scale     = column.Scale     ?? DEFAULT_SCALE;
				break;

			case SqlDbType.DateTime2:
			case SqlDbType.DateTimeOffset:
			case SqlDbType.Time:
				parameter.Scale = column.Scale ?? DEFAULT_TIME_SCALE;
				break;
		}
	}

	private string DescribeKeyColumns(IReadOnlyList<ColumnModel> columns)
		=> string.Join(
			", ",
			columns.Select(
				(column, index) => $"{SqlSyntax.QuoteIdentifier(GeneratedKeyTable.GetValueColumnName(index))} {_converter.GetTypeDeclaration(column)} NULL"
			)
		);

	private static string DescribeValueColumns(int count)
		=> string.Join(", ", Enumerable.Range(0, count).Select(index => SqlSyntax.QuoteIdentifier(GeneratedKeyTable.GetValueColumnName(index))));

	private static string GetParameterName(int row, int column) => $"@r{row}c{column}";

	private static SqlCommand CreateCommand(SqlConnection connection, SqlTransaction transaction, string sql, int timeoutSeconds)
		=> new SqlCommand(sql, connection, transaction)
		{
			CommandTimeout = timeoutSeconds
		};

	private static async Task<List<object?[]>> ReadRowsAsync(SqlDataReader reader, CancellationToken cancellationToken)
	{
		List<object?[]> rows = [];

		while (await reader.ReadAsync(cancellationToken))
		{
			object?[] values = new object?[reader.FieldCount];

			for (int index = 0; index < values.Length; ++index)
			{
				values[index] = reader.IsDBNull(index) ? null : reader.GetValue(index);
			}

			rows.Add(values);
		}

		return rows;
	}
}