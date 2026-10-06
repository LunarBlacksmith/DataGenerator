using System.Data;
using System.Text;
using DataGenerator.Interfaces;
using DataGenerator.Models;
using Microsoft.Data.SqlClient;

namespace DataGenerator.Services.Generation;

/// <summary>
///	Inserts a <see cref="GenerationBlueprint"/> directly into SQL Server inside one transaction.
/// </summary>
internal sealed class DirectDataInserter
{
	#region FIELDS
	#region PRIVATE
	private const int    COMMAND_TIMEOUT_SECONDS         = 120;
	private const int    CLEANUP_TIMEOUT_SECONDS         = 600;
	private const int    POST_GENERATION_TIMEOUT_SECONDS = 600;
	private const int    UPDATE_TIMEOUT_SECONDS          = 600;
	private const int    MAXIMUM_PARAMETERS              = 2_000;
	private const int    MAXIMUM_BATCH_ROWS              = 100;
	private const byte   DEFAULT_PRECISION               = 18;
	private const byte   DEFAULT_SCALE                   = 0;
	private const byte   DEFAULT_TIME_SCALE              = 7;
	private const string INSERTED_KEYS_VARIABLE          = "@dg_inserted";

	private readonly ISqlValueConverter _converter;
	private readonly RowValueBuilder    _rowValueBuilder;
	#endregion PRIVATE
	#endregion FIELDS

	#region CONSTRUCTORS
	#region PUBLIC
	/// <summary>
	///	Creates the service that inserts generated rows directly into SQL Server.
	/// </summary>
	/// <param name="converter">
	///	The converter used to configure SQL parameters and type declarations.
	/// </param>
	/// <param name="rowValueBuilder">
	///	The builder used to generate row values.
	/// </param>
	/// <exception cref="ArgumentNullException">
	///	Thrown when any dependency is <see langword="null"/>.
	/// </exception>
	public DirectDataInserter(ISqlValueConverter converter, RowValueBuilder rowValueBuilder)
	{
		_converter       = converter       ?? throw new ArgumentNullException(nameof(converter));
		_rowValueBuilder = rowValueBuilder ?? throw new ArgumentNullException(nameof(rowValueBuilder));
	}
	#endregion PUBLIC
	#endregion CONSTRUCTORS

	#region METHODS
	#region PUBLIC
	/// <summary>
	///	Opens a SQL Server transaction, performs all cleanup and generation operations, and commits only when every step succeeds.
	/// </summary>
	/// <param name="blueprint">
	///	The generation blueprint to execute.
	/// </param>
	/// <param name="connectionString">
	///	The SQL Server connection string.
	/// </param>
	/// <param name="progress">
	///	The progress reporter for user-visible status.
	/// </param>
	/// <param name="cancellationToken">
	///	Token used to cancel generation before commit.
	/// </param>
	/// <exception cref="OperationCanceledException">
	///	Thrown when <paramref name="cancellationToken"/> is cancelled before commit.
	/// </exception>
	/// <exception cref="DataGenerationException">
	///	Thrown when cleanup, generation or post-generation SQL fails.
	/// </exception>
	public async Task InsertAsync(
		GenerationBlueprint blueprint,
		string              connectionString,
		GenerationProgress  progress,
		CancellationToken   cancellationToken
	)
	{
		progress.Report("Connecting to SQL Server…");

		await using SqlConnection connection = new(connectionString);
		await connection.OpenAsync(cancellationToken);

		await using SqlTransaction transaction = (SqlTransaction)await connection.BeginTransactionAsync(
			IsolationLevel.ReadCommitted,
			cancellationToken
		);

		try
		{
			await ClearTablesAsync(connection, transaction, blueprint, progress, cancellationToken);
			await CreateSnapshotsAsync(connection, transaction, blueprint, progress, cancellationToken);

			HashSet<ExistingKeyPool> loadedPools = [];

			foreach (GenerationOperation operation in blueprint.Operations)
			{
				RowSetBlueprint rowSet = operation.RowSet;

				foreach (ExistingKeyPool pool in rowSet.ExistingKeyPools)
				{
					if (loadedPools.Add(pool))
					{
						await LoadPoolAsync(connection, transaction, pool, progress, cancellationToken);
					}
				}

				foreach (LookupPool lookup in rowSet.LookupPools)
				{
					await LoadLookupAsync(connection, transaction, lookup, progress, cancellationToken);
				}

				if (rowSet.Update is RowSetUpdate update)
				{
					await UpdateRowSetAsync(connection, transaction, operation.Table, rowSet, update, progress, cancellationToken);
				}
				else
				{
					await InsertRowSetAsync(connection, transaction, operation.Table, rowSet, progress, cancellationToken);
				}
			}

			await RunPostGenerationAsync(connection, transaction, blueprint, progress, cancellationToken);
			await DropSnapshotsAsync(connection, transaction, blueprint, cancellationToken);

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
	#endregion PUBLIC

	#region PRIVATE
	/// <summary>
	///	Deletes requested existing rows and optionally restarts identity values before generation begins.
	/// </summary>
	/// <param name="connection">
	///	The open SQL connection.
	/// </param>
	/// <param name="transaction">
	///	The generation transaction.
	/// </param>
	/// <param name="blueprint">
	///	The blueprint containing cleanup settings.
	/// </param>
	/// <param name="progress">
	///	The progress reporter for cleanup messages.
	/// </param>
	/// <param name="cancellationToken">
	///	Token used to cancel SQL execution.
	/// </param>
	/// <exception cref="DataGenerationException">
	///	Thrown when a cleanup statement fails.
	/// </exception>
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

		foreach (TableModel table in
			blueprint
				.TablesToClear
				.Where(SqlCleanupStatements.HasIdentityColumn)
		)
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
	///	Copies the rows that exist before anything is inserted, for the "Generated rows" and "Existing rows" scopes. The
	///	commands have no parameters, so the temporary tables belong to the connection and stay available for later commands.
	/// </summary>
	/// <param name="connection">
	///	The open SQL connection.
	/// </param>
	/// <param name="transaction">
	///	The generation transaction.
	/// </param>
	/// <param name="blueprint">
	///	The blueprint containing the required snapshots.
	/// </param>
	/// <param name="progress">
	///	The progress reporter for snapshot messages.
	/// </param>
	/// <param name="cancellationToken">
	///	Token used to cancel SQL execution.
	/// </param>
	/// <exception cref="DataGenerationException">
	///	Thrown when a snapshot cannot be created.
	/// </exception>
	private static async Task CreateSnapshotsAsync(
		SqlConnection       connection,
		SqlTransaction      transaction,
		GenerationBlueprint blueprint,
		GenerationProgress  progress,
		CancellationToken   cancellationToken
	)
	{
		foreach (RowSnapshot snapshot in blueprint.Snapshots)
		{
			progress.Report($"Remembering the existing rows of {snapshot.Table.FullyQualifiedName}…");

			await ExecuteStatementAsync(
				connection,
				transaction,
				$"{snapshot.BuildDropStatement()}{Environment.NewLine}{snapshot.BuildCreateStatement()}",
				COMMAND_TIMEOUT_SECONDS,
				"The existing rows could not be read.",
				snapshot.Table.FullyQualifiedName,
				cancellationToken
			);
		}
	}

	/// <summary>
	///	Drops temporary snapshot tables after the generation transaction has finished using them.
	/// </summary>
	/// <param name="connection">
	///	The open SQL connection.
	/// </param>
	/// <param name="transaction">
	///	The generation transaction.
	/// </param>
	/// <param name="blueprint">
	///	The blueprint containing the snapshots.
	/// </param>
	/// <param name="cancellationToken">
	///	Token used to cancel SQL execution.
	/// </param>
	/// <exception cref="DataGenerationException">
	///	Thrown when the snapshot tables cannot be removed.
	/// </exception>
	private static async Task DropSnapshotsAsync(
		SqlConnection       connection,
		SqlTransaction      transaction,
		GenerationBlueprint blueprint,
		CancellationToken   cancellationToken
	)
	{
		if (blueprint.Snapshots.Count == 0)
		{
			return;
		}

		await ExecuteStatementAsync(
			connection,
			transaction,
			string.Join(
				Environment.NewLine,
				blueprint
					.Snapshots
					.Select(snapshot => snapshot.BuildDropStatement())
			),
			COMMAND_TIMEOUT_SECONDS,
			"The temporary copies of existing rows could not be removed.",
			"Temporary tables",
			cancellationToken
		);
	}

	/// <summary>
	///	Runs a statement without parameters and reports a SQL error as a <see cref="DataGenerationException"/>.
	/// </summary>
	/// <param name="connection">
	///	The open SQL connection.
	/// </param>
	/// <param name="transaction">
	///	The active SQL transaction.
	/// </param>
	/// <param name="sql">
	///	The SQL statement to execute.
	/// </param>
	/// <param name="timeoutSeconds">
	///	The command timeout in seconds.
	/// </param>
	/// <param name="failureMessage">
	///	The message prefix used when execution fails.
	/// </param>
	/// <param name="location">
	///	The user-facing location reported with the failure.
	/// </param>
	/// <param name="cancellationToken">
	///	Token used to cancel SQL execution.
	/// </param>
	/// <exception cref="DataGenerationException">
	///	Thrown when SQL Server rejects the statement.
	/// </exception>
	private static async Task ExecuteStatementAsync(
		SqlConnection     connection,
		SqlTransaction    transaction,
		string            sql,
		int               timeoutSeconds,
		string            failureMessage,
		string            location,
		CancellationToken cancellationToken
	)
	{
		await using SqlCommand command = CreateCommand(connection, transaction, sql, timeoutSeconds);

		try
		{
			_ = await command.ExecuteNonQueryAsync(cancellationToken);
		}
		catch (SqlException exception)
		{
			throw new DataGenerationException($"{failureMessage} {exception.Message}", location, exception);
		}
	}

	/// <summary>
	///	Runs the stored procedures and SQL the user asked to run after the inserts, in the chosen database and inside the
	///	generation transaction, so a failure rolls back the inserted rows too.
	/// </summary>
	/// <param name="connection">
	///	The open SQL connection.
	/// </param>
	/// <param name="transaction">
	///	The generation transaction.
	/// </param>
	/// <param name="blueprint">
	///	The blueprint containing optional post-generation SQL.
	/// </param>
	/// <param name="progress">
	///	The progress reporter for statement messages.
	/// </param>
	/// <param name="cancellationToken">
	///	Token used to cancel between statements.
	/// </param>
	/// <exception cref="OperationCanceledException">
	///	Thrown when <paramref name="cancellationToken"/> is cancelled.
	/// </exception>
	/// <exception cref="DataGenerationException">
	///	Thrown when a post-generation statement fails.
	/// </exception>
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

	/// <summary>
	///	Executes one post-generation SQL statement and wraps SQL failures for the UI.
	/// </summary>
	/// <param name="connection">
	///	The open SQL connection.
	/// </param>
	/// <param name="transaction">
	///	The generation transaction.
	/// </param>
	/// <param name="sql">
	///	The SQL statement or stored procedure command to execute.
	/// </param>
	/// <param name="location">
	///	The user-facing statement location.
	/// </param>
	/// <param name="cancellationToken">
	///	Token used to cancel SQL execution.
	/// </param>
	/// <exception cref="DataGenerationException">
	///	Thrown when SQL Server rejects the post-generation statement.
	/// </exception>
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

	/// <summary>
	///	Executes one cleanup statement and wraps SQL failures for the table being cleaned.
	/// </summary>
	/// <param name="connection">
	///	The open SQL connection.
	/// </param>
	/// <param name="transaction">
	///	The generation transaction.
	/// </param>
	/// <param name="sql">
	///	The cleanup SQL to execute.
	/// </param>
	/// <param name="table">
	///	The table being cleaned.
	/// </param>
	/// <param name="cancellationToken">
	///	Token used to cancel SQL execution.
	/// </param>
	/// <exception cref="DataGenerationException">
	///	Thrown when SQL Server rejects the cleanup statement.
	/// </exception>
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

	/// <summary>
	///	Reads and stores values for one value-from-table lookup before its row set is generated.
	/// </summary>
	/// <param name="connection">
	///	The open SQL connection.
	/// </param>
	/// <param name="transaction">
	///	The generation transaction.
	/// </param>
	/// <param name="lookup">
	///	The lookup pool to load.
	/// </param>
	/// <param name="progress">
	///	The progress reporter for lookup messages.
	/// </param>
	/// <param name="cancellationToken">
	///	Token used to cancel SQL execution.
	/// </param>
	/// <exception cref="DataGenerationException">
	///	Thrown when lookup values cannot be read or too few are available.
	/// </exception>
	private static async Task LoadLookupAsync(
		SqlConnection      connection,
		SqlTransaction     transaction,
		LookupPool         lookup,
		GenerationProgress progress,
		CancellationToken  cancellationToken
	)
	{
		progress.Report($"Reading values of {lookup.Lookup.SourceDisplayName}…");

		List<object?> values = [];

		await using SqlCommand command = CreateCommand(connection, transaction, $"{lookup.SelectStatement};", COMMAND_TIMEOUT_SECONDS);

		try
		{
			await using SqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken);

			values.AddRange((await ReadRowsAsync(reader, cancellationToken)).Select(row => row[0]));
		}
		catch (SqlException exception)
		{
			throw new DataGenerationException(
				$"The values of {lookup.Lookup.SourceDisplayName} could not be read. {exception.Message}",
				lookup.Location,
				exception
			);
		}

		lookup.Load(values);
	}

	/// <summary>
	///	Runs the UPDATE statement for an update set and reads the number of changed rows.
	/// </summary>
	/// <param name="connection">
	///	The open SQL connection.
	/// </param>
	/// <param name="transaction">
	///	The generation transaction.
	/// </param>
	/// <param name="update">
	///	The update metadata to execute.
	/// </param>
	/// <param name="cancellationToken">
	///	Token used to cancel SQL execution.
	/// </param>
	/// <returns>
	///	The number of rows changed by the update; 0 when SQL Server returns no count.
	/// </returns>
	/// <exception cref="DataGenerationException">
	///	Thrown when SQL Server rejects the update statement.
	/// </exception>
	private static async Task<long> ExecuteUpdateAsync(
		SqlConnection     connection,
		SqlTransaction    transaction,
		RowSetUpdate      update,
		CancellationToken cancellationToken
	)
	{
		await using SqlCommand command = CreateCommand(
			connection,
			transaction,
			$"{update.UpdateStatement}{Environment.NewLine}SELECT CONVERT(BIGINT, @@ROWCOUNT);",
			UPDATE_TIMEOUT_SECONDS
		);

		try
		{
			return await command.ExecuteScalarAsync(cancellationToken) is long count ? count : 0;
		}
		catch (SqlException exception)
		{
			throw new DataGenerationException($"The rows could not be changed. {exception.Message}", update.Location, exception);
		}
	}

	/// <summary>
	///	Sets the parameters of a staging batch: per row the row number (1-based within the update set), then the values.
	/// </summary>
	/// <param name="command">
	///	The staging insert command to populate.
	/// </param>
	/// <param name="rowSet">
	///	The update row-set blueprint.
	/// </param>
	/// <param name="batch">
	///	The generated value rows in this batch.
	/// </param>
	/// <param name="batchStart">
	///	The zero-based row index of the first row in the batch.
	/// </param>
	private static void SetStagingParameterValues(SqlCommand command, RowSetBlueprint rowSet, List<object?[]> batch, long batchStart)
	{
		int columnCount = rowSet.Sources.Count + 1;

		for (int row = 0; row < batch.Count; ++row)
		{
			command.Parameters[row * columnCount].Value = (int)(batchStart + row + 1);

			for (int column = 0; column < rowSet.Sources.Count; ++column)
			{
				command.Parameters[(row * columnCount) + column + 1].Value = ToParameterValue(rowSet, column, batch[row][column]);
			}
		}
	}

	/// <summary>
	///	Executes one insert or update-staging batch and captures generated keys when requested.
	/// </summary>
	/// <param name="command">
	///	The command to execute.
	/// </param>
	/// <param name="table">
	///	The table blueprint being processed.
	/// </param>
	/// <param name="keys">
	///	The key table that receives the keys the batch returns; null when it returns none.
	/// </param>
	/// <param name="rowSet">
	///	The row set whose rows are in the batch.
	/// </param>
	/// <param name="batchStart">
	///	The zero-based row index of the first batch row.
	/// </param>
	/// <param name="rowCount">
	///	The number of rows in the batch.
	/// </param>
	/// <param name="cancellationToken">
	///	Token used to cancel SQL execution.
	/// </param>
	/// <exception cref="DataGenerationException">
	///	Thrown when SQL Server rejects the batch.
	/// </exception>
	private static async Task ExecuteBatchAsync(
		SqlCommand         command,
		TableBlueprint     table,
		GeneratedKeyTable? keys,
		RowSetBlueprint    rowSet,
		long               batchStart,
		int                rowCount,
		CancellationToken  cancellationToken
	)
	{
		try
		{
			if (keys is null)
			{
				_ = await command.ExecuteNonQueryAsync(cancellationToken);
				return;
			}

			await using SqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken);

			foreach (object?[] keyValues in await ReadRowsAsync(reader, cancellationToken))
			{
				keys.AddRow(keyValues);
			}

			while (await reader.NextResultAsync(cancellationToken))
			{
				// Surfaces errors raised after the first result set.
			}
		}
		catch (SqlException exception)
		{
			string rows =
				rowCount == 1
					? $"Row {batchStart + 1:N0}"
					: $"Rows {batchStart + 1:N0}–{batchStart + rowCount:N0}";

			throw new DataGenerationException(
				exception.Message,
				$"{GenerationBlueprintBuilder.DescribeLocation(table.Table, rowSet.Plan)} › {rows}",
				exception
			);
		}
	}

	/// <summary>
	///	Sets value parameters for an insert batch.
	/// </summary>
	/// <param name="command">
	///	The insert command to populate.
	/// </param>
	/// <param name="rowSet">
	///	The insert row-set blueprint.
	/// </param>
	/// <param name="batch">
	///	The generated value rows in this batch.
	/// </param>
	private static void SetParameterValues(SqlCommand command, RowSetBlueprint rowSet, List<object?[]> batch)
	{
		int columnCount = rowSet.Sources.Count;

		for (int row = 0; row < batch.Count; ++row)
		{
			for (int column = 0; column < columnCount; ++column)
			{
				command.Parameters[(row * columnCount) + column].Value = ToParameterValue(rowSet, column, batch[row][column]);
			}
		}
	}

	/// <summary>
	///	Converts a generated value to a value that can be assigned to a SQL parameter.
	/// </summary>
	/// <param name="rowSet">
	///	The row-set blueprint used for error messages.
	/// </param>
	/// <param name="column">
	///	The source-column index of the value.
	/// </param>
	/// <param name="value">
	///	The generated value to convert.
	/// </param>
	/// <returns>
	///	The original value, or <see cref="DBNull.Value"/> for <see langword="null"/>.
	/// </returns>
	/// <exception cref="InvalidOperationException">
	///	Thrown when a SQL-script-only fragment reaches direct insertion.
	/// </exception>
	private static object ToParameterValue(RowSetBlueprint rowSet, int column, object? value)
		=> value switch
		{
			null        => DBNull.Value,
			SqlFragment => throw new InvalidOperationException(
				$"Column [{rowSet.Sources[column].Rule.Column.Name}] received a value that only works in SQL scripts."
			),
			_           => value
		};

	/// <summary>
	///	Describes generated value column names for a temporary or table variable.
	/// </summary>
	/// <param name="count">
	///	The number of value columns to include.
	/// </param>
	/// <returns>
	///	A comma-separated list of quoted generated value column names.
	/// </returns>
	private static string DescribeValueColumns(int count)
		=> string.Join(
			", ",
			Enumerable
				.Range(0, count)
				.Select(index => SqlSyntax.QuoteIdentifier(GeneratedKeyTable.GetValueColumnName(index)))
		);

	/// <summary>
	///	Builds the parameter name for one row and source column.
	/// </summary>
	/// <param name="row">
	///	Zero-based batch row index.
	/// </param>
	/// <param name="column">
	///	Zero-based source-column index.
	/// </param>
	/// <returns>
	///	The SQL parameter name.
	/// </returns>
	private static string GetParameterName(int row, int column) => $"@r{row}c{column}";

	/// <summary>
	///	Builds the row-number parameter name for one staging row.
	/// </summary>
	/// <param name="row">
	///	Zero-based batch row index.
	/// </param>
	/// <returns>
	///	The SQL parameter name for the row number.
	/// </returns>
	private static string GetRowNumberParameterName(int row) => $"@r{row}n";

	/// <summary>
	///	Creates a SQL command bound to the supplied connection, transaction and timeout.
	/// </summary>
	/// <param name="connection">
	///	The open SQL connection.
	/// </param>
	/// <param name="transaction">
	///	The active SQL transaction.
	/// </param>
	/// <param name="sql">
	///	The SQL text to execute.
	/// </param>
	/// <param name="timeoutSeconds">
	///	The command timeout in seconds.
	/// </param>
	/// <returns>
	///	The configured SQL command.
	/// </returns>
	private static SqlCommand CreateCommand(SqlConnection connection, SqlTransaction transaction, string sql, int timeoutSeconds)
		=> new SqlCommand(sql, connection, transaction)
		{
			CommandTimeout = timeoutSeconds
		};

	/// <summary>
	///	Reads all rows from a data reader into nullable object arrays.
	/// </summary>
	/// <param name="reader">
	///	The data reader positioned before the first row.
	/// </param>
	/// <param name="cancellationToken">
	///	Token used to cancel reading.
	/// </param>
	/// <returns>
	///	The rows read from the current result set.
	/// </returns>
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

	/// <summary>
	///	Reads and stores a sample of existing key values for direct insertion.
	/// </summary>
	/// <param name="connection">
	///	The open SQL connection.
	/// </param>
	/// <param name="transaction">
	///	The generation transaction.
	/// </param>
	/// <param name="pool">
	///	The existing-key pool to load.
	/// </param>
	/// <param name="progress">
	///	The progress reporter for sampling messages.
	/// </param>
	/// <param name="cancellationToken">
	///	Token used to cancel SQL execution.
	/// </param>
	/// <exception cref="DataGenerationException">
	///	Thrown when key values cannot be read or no rows are available.
	/// </exception>
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
	///	Stores the new values of an update set in a temporary staging table (in parameterised batches), then changes
	///	randomly chosen rows of the table that meet the scope and condition to those values.
	/// </summary>
	/// <param name="connection">
	///	The open SQL connection.
	/// </param>
	/// <param name="transaction">
	///	The generation transaction.
	/// </param>
	/// <param name="table">
	///	The table blueprint that owns the row set.
	/// </param>
	/// <param name="rowSet">
	///	The update row-set blueprint.
	/// </param>
	/// <param name="update">
	///	The update metadata with staging and update SQL.
	/// </param>
	/// <param name="progress">
	///	The progress reporter for preparation and update messages.
	/// </param>
	/// <param name="cancellationToken">
	///	Token used to cancel row preparation or SQL execution.
	/// </param>
	/// <exception cref="OperationCanceledException">
	///	Thrown when <paramref name="cancellationToken"/> is cancelled.
	/// </exception>
	/// <exception cref="DataGenerationException">
	///	Thrown when staging, updating or the required-row check fails.
	/// </exception>
	private async Task UpdateRowSetAsync(
		SqlConnection      connection,
		SqlTransaction     transaction,
		TableBlueprint     table,
		RowSetBlueprint    rowSet,
		RowSetUpdate       update,
		GenerationProgress progress,
		CancellationToken  cancellationToken
	)
	{
		RowSetPlan      plan            = rowSet.Plan;
		int             columnCount     = rowSet.Sources.Count + 1;
		int             rowsPerBatch    = Math.Clamp(MAXIMUM_PARAMETERS / columnCount, 1, MAXIMUM_BATCH_ROWS);
		List<object?[]> batch           = new(rowsPerBatch);
		SqlCommand?     command         = null;
		int             commandRowCount = 0;
		long            batchStart      = 0;

		await ExecuteStatementAsync(
			connection,
			transaction,
			$"{update.BuildDropStatement()}{Environment.NewLine}{update.CreateStatement}",
			COMMAND_TIMEOUT_SECONDS,
			"The staging table for the changed values could not be created.",
			update.Location,
			cancellationToken
		);

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

					command         = CreateStagingInsertCommand(connection, transaction, rowSet, update, batch.Count);
					commandRowCount = batch.Count;
				}

				SetStagingParameterValues(command, rowSet, batch, batchStart);

				await ExecuteBatchAsync(command, table, null, rowSet, batchStart, batch.Count, cancellationToken);

				batchStart = rowIndex + 1;
				batch.Clear();

				progress.ReportRows("Preparing", table.Table.DisplayName, plan.Name, rowIndex + 1, plan.RowCount);
			}
		}
		finally
		{
			if (command is not null)
			{
				await command.DisposeAsync();
			}
		}

		progress.Report($"Changing {plan.RowCount:N0} row(s) of {table.Table.DisplayName} › Set '{plan.Name}'…");

		long changedCount = await ExecuteUpdateAsync(connection, transaction, update, cancellationToken);

		if (changedCount < update.RequiredCount)
		{
			throw new DataGenerationException(update.DescribeShortage(changedCount), update.Location);
		}

		await ExecuteStatementAsync(
			connection,
			transaction,
			update.BuildDropStatement(),
			COMMAND_TIMEOUT_SECONDS,
			"The staging table for the changed values could not be removed.",
			update.Location,
			cancellationToken
		);
	}

	/// <summary>
	///	Creates a reusable parameterised command for inserting staged update values.
	/// </summary>
	/// <param name="connection">
	///	The open SQL connection.
	/// </param>
	/// <param name="transaction">
	///	The generation transaction.
	/// </param>
	/// <param name="rowSet">
	///	The update row-set blueprint.
	/// </param>
	/// <param name="update">
	///	The update metadata containing the staging table.
	/// </param>
	/// <param name="rowCount">
	///	The number of rows this batch command inserts.
	/// </param>
	/// <returns>
	///	A command with row-number and value parameters configured.
	/// </returns>
	private SqlCommand CreateStagingInsertCommand(
		SqlConnection   connection,
		SqlTransaction  transaction,
		RowSetBlueprint rowSet,
		RowSetUpdate    update,
		int             rowCount
	)
	{
		StringBuilder sql = new StringBuilder(update.CreateInsertPrefix()).Append(" VALUES ");

		for (int row = 0; row < rowCount; ++row)
		{
			_ = sql
				.Append(row == 0 ? "(" : ", (")
				.Append(GetRowNumberParameterName(row))
				.Append(", ")
				.AppendJoin(
					", ",
					Enumerable
						.Range(0, rowSet.Sources.Count)
						.Select(column => GetParameterName(row, column))
				)
				.Append(')');
		}

		_ = sql.Append(';');

		SqlCommand command = CreateCommand(connection, transaction, sql.ToString(), COMMAND_TIMEOUT_SECONDS);

		for (int row = 0; row < rowCount; ++row)
		{
			_ = command.Parameters.Add(new SqlParameter(GetRowNumberParameterName(row), SqlDbType.Int));

			for (int column = 0; column < rowSet.Sources.Count; ++column)
			{
				SqlParameter parameter = new()
				{
					ParameterName = GetParameterName(row, column)
				};

				ConfigureParameter(parameter, rowSet.Sources[column].Rule.Column);

				_ = command.Parameters.Add(parameter);
			}
		}

		return command;
	}

	/// <summary>
	///	Inserts the rows in parameterised multi-row batches. Tables referenced by "Generated key" rules return the
	///	stored key values through an OUTPUT clause; their order does not matter because keys are picked at random.
	/// </summary>
	/// <param name="connection">
	///	The open SQL connection.
	/// </param>
	/// <param name="transaction">
	///	The generation transaction.
	/// </param>
	/// <param name="table">
	///	The table blueprint that owns the row set.
	/// </param>
	/// <param name="rowSet">
	///	The insert row-set blueprint.
	/// </param>
	/// <param name="progress">
	///	The progress reporter for insert messages.
	/// </param>
	/// <param name="cancellationToken">
	///	Token used to cancel row generation or SQL execution.
	/// </param>
	/// <exception cref="OperationCanceledException">
	///	Thrown when <paramref name="cancellationToken"/> is cancelled.
	/// </exception>
	/// <exception cref="DataGenerationException">
	///	Thrown when a batch insert fails.
	/// </exception>
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
		List<object?[]> batch           = new(rowsPerBatch);
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

				await ExecuteBatchAsync(command, table, table.Keys, rowSet, batchStart, batch.Count, cancellationToken);

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

	/// <summary>
	///	Creates a reusable parameterised command for one insert batch, including optional key capture.
	/// </summary>
	/// <param name="connection">
	///	The open SQL connection.
	/// </param>
	/// <param name="transaction">
	///	The generation transaction.
	/// </param>
	/// <param name="table">
	///	The table blueprint receiving rows.
	/// </param>
	/// <param name="rowSet">
	///	The insert row-set blueprint.
	/// </param>
	/// <param name="rowCount">
	///	The number of rows this batch command inserts.
	/// </param>
	/// <returns>
	///	A command with all value parameters configured.
	/// </returns>
	private SqlCommand CreateInsertCommand(
		SqlConnection   connection,
		SqlTransaction  transaction,
		TableBlueprint  table,
		RowSetBlueprint rowSet,
		int             rowCount
	)
	{
		StringBuilder      sql          = new();
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
				.AppendJoin(
					", ",
					rowSet
						.Sources
						.Select(source => SqlSyntax.QuoteIdentifier(source.Rule.Column.Name))
				)
				.Append(')');
		}

		if (keys is not null)
		{
			_ = sql
				.Append(" OUTPUT ")
				.AppendJoin(
					", ",
					keys
						.Columns
						.Select(column => $"INSERTED.{SqlSyntax.QuoteIdentifier(column.Name)}")
				)
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
					.AppendJoin(
						", ",
						Enumerable
							.Range(0, rowSet.Sources.Count)
							.Select(column => GetParameterName(row, column))
					)
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
				SqlParameter parameter = new()
				{
					ParameterName = GetParameterName(row, column)
				};

				ConfigureParameter(parameter, rowSet.Sources[column].Rule.Column);

				_ = command.Parameters.Add(parameter);
			}
		}

		return command;
	}

	/// <summary>
	///	Configures SQL type, size, precision and scale for a parameter from column metadata.
	/// </summary>
	/// <param name="parameter">
	///	The parameter to configure.
	/// </param>
	/// <param name="column">
	///	The column whose SQL metadata controls the parameter.
	/// </param>
	private void ConfigureParameter(SqlParameter parameter, ColumnModel column)
	{
		parameter.SqlDbType = _converter.GetSqlDbType(column);

		switch (parameter.SqlDbType)
		{
			case SqlDbType.Char:
			case SqlDbType.NChar:
			{
				parameter.Size = _converter.GetMaximumTextLength(column) ?? 1;
				break;
			}

			case SqlDbType.VarChar:
			case SqlDbType.NVarChar:
			{
				parameter.Size = _converter.GetMaximumTextLength(column) ?? -1;
				break;
			}

			case SqlDbType.Binary:
			{
				parameter.Size = column.MaximumLength is null or < 1 ? 1 : column.MaximumLength.Value;
				break;
			}

			case SqlDbType.VarBinary:
			{
				parameter.Size = column.MaximumLength is null or < 1 ? -1 : column.MaximumLength.Value;
				break;
			}

			case SqlDbType.Decimal:
			{
				parameter.Precision = column.Precision ?? DEFAULT_PRECISION;
				parameter.Scale     = column.Scale     ?? DEFAULT_SCALE;
				break;
			}

			case SqlDbType.DateTime2:
			case SqlDbType.DateTimeOffset:
			case SqlDbType.Time:
			{
				parameter.Scale = column.Scale ?? DEFAULT_TIME_SCALE;
				break;
			}
		}
	}

	/// <summary>
	///	Describes the table-variable columns used to capture generated keys.
	/// </summary>
	/// <param name="columns">
	///	The generated-key columns to declare.
	/// </param>
	/// <returns>
	///	A comma-separated list of key value column declarations.
	/// </returns>
	private string DescribeKeyColumns(IReadOnlyList<ColumnModel> columns)
		=> string.Join(
			", ",
			columns
				.Select(
					(column, index) => $"{SqlSyntax.QuoteIdentifier(GeneratedKeyTable.GetValueColumnName(index))} {_converter.GetTypeDeclaration(column)} NULL"
				)
		);
	#endregion PRIVATE
	#endregion METHODS
}