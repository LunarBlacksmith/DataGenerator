using System.Globalization;
using System.IO;
using System.Text;
using DataGenerator.Interfaces;
using DataGenerator.Models;

namespace DataGenerator.Services.Generation;

/// <summary>
///	Writes a <see cref="GenerationBlueprint"/> as one transactional T-SQL script.
/// </summary>
internal sealed class SqlScriptWriter
{
	private const int    SCRIPT_BATCH_SIZE     = 100;
	private const int    UPDATE_SHORTAGE_ERROR = 50002;
	private const int    LOOKUP_SHORTAGE_ERROR = 50003;
	private const string INDENT                = "\t";
	private const string SEPARATOR_LINE        = "-- =============================================================================";

	private readonly ISqlValueConverter _converter;
	private readonly RowValueBuilder    _rowValueBuilder;

	/// <summary>
	///	Creates the service that writes transactional generation scripts.
	/// </summary>
	/// <param name="converter">
	///	The converter used to format SQL literals and type declarations.
	/// </param>
	/// <param name="rowValueBuilder">
	///	The builder used to generate row values.
	/// </param>
	/// <exception cref="ArgumentNullException">
	///	Thrown when any dependency is <see langword="null"/>.
	/// </exception>
	public SqlScriptWriter(ISqlValueConverter converter, RowValueBuilder rowValueBuilder)
	{
		_converter       = converter       ?? throw new ArgumentNullException(nameof(converter));
		_rowValueBuilder = rowValueBuilder ?? throw new ArgumentNullException(nameof(rowValueBuilder));
	}

	/// <summary>
	///	Writes to a temporary file first, so an existing script is only replaced when the new one is complete.
	/// </summary>
	/// <param name="blueprint">
	///	The generation blueprint to write.
	/// </param>
	/// <param name="outputFilePath">
	///	The script file path to create or replace.
	/// </param>
	/// <param name="progress">
	///	The progress reporter for row writing.
	/// </param>
	/// <param name="cancellationToken">
	///	Token used to cancel script generation.
	/// </param>
	public Task WriteAsync(
		GenerationBlueprint blueprint,
		string              outputFilePath,
		GenerationProgress  progress,
		CancellationToken   cancellationToken
	)
		=> Task.Run(() => Write(blueprint, outputFilePath, progress, cancellationToken), cancellationToken);

	/// <summary>
	///	Writes the script to a temporary file and then atomically replaces the target file.
	/// </summary>
	/// <param name="blueprint">
	///	The generation blueprint to write.
	/// </param>
	/// <param name="outputFilePath">
	///	The script file path to create or replace.
	/// </param>
	/// <param name="progress">
	///	The progress reporter for row writing.
	/// </param>
	/// <param name="cancellationToken">
	///	Token used to cancel script generation.
	/// </param>
	/// <exception cref="DataGenerationException">
	///	Thrown when the output path has no containing folder.
	/// </exception>
	private void Write(
		GenerationBlueprint blueprint,
		string              outputFilePath,
		GenerationProgress  progress,
		CancellationToken   cancellationToken
	)
	{
		string fullPath  = Path.GetFullPath(outputFilePath);
		string directory = Path.GetDirectoryName(fullPath)
			?? throw new DataGenerationException("The output file path does not contain a folder.", fullPath);
		string tempPath  = Path.Combine(directory, $".{Path.GetFileName(fullPath)}.{Guid.NewGuid():N}.tmp");

		_ = Directory.CreateDirectory(directory);

		try
		{
			using (StreamWriter writer = new(tempPath, false, new UTF8Encoding(false)))
			{
				writer.NewLine = "\r\n";
				WriteScript(writer, blueprint, progress, cancellationToken);
			}

			File.Move(tempPath, fullPath, true);
		}
		catch
		{
			TryDeleteFile(tempPath);
			throw;
		}
	}

	/// <summary>
	///	Writes the full transaction script, including cleanup, row generation, post-generation SQL and error handling.
	/// </summary>
	/// <param name="writer">
	///	The text writer receiving the script.
	/// </param>
	/// <param name="blueprint">
	///	The generation blueprint to write.
	/// </param>
	/// <param name="progress">
	///	The progress reporter for row writing.
	/// </param>
	/// <param name="cancellationToken">
	///	Token used to cancel script generation.
	/// </param>
	/// <exception cref="OperationCanceledException">
	///	Thrown when <paramref name="cancellationToken"/> is cancelled.
	/// </exception>
	private void WriteScript(
		TextWriter          writer,
		GenerationBlueprint blueprint,
		GenerationProgress  progress,
		CancellationToken   cancellationToken
	)
	{
		HashSet<ExistingKeyPool> loadedPools = [];

		WriteHeader(writer, blueprint);

		writer.WriteLine("SET NOCOUNT ON;");
		writer.WriteLine("SET XACT_ABORT ON;");
		writer.WriteLine();

		WriteDeclarations(writer, blueprint);

		writer.WriteLine("BEGIN TRY");
		writer.WriteLine($"{INDENT}BEGIN TRANSACTION;");
		writer.WriteLine();

		WriteCleanup(writer, blueprint);
		WriteSnapshots(writer, blueprint);

		int currentStep = 0;

		foreach (GenerationOperation operation in blueprint.Operations)
		{
			RowSetBlueprint rowSet = operation.RowSet;

			cancellationToken.ThrowIfCancellationRequested();

			if (blueprint.StepCount > 1 && rowSet.Plan.Step != currentStep)
			{
				currentStep = rowSet.Plan.Step;
				writer.WriteLine($"{INDENT}-- ----- Step {currentStep} -----");
				writer.WriteLine();
			}

			foreach (ExistingKeyPool pool in rowSet.ExistingKeyPools)
			{
				if (loadedPools.Add(pool))
				{
					WritePoolLoad(writer, pool);
				}
			}

			foreach (LookupPool lookup in rowSet.LookupPools)
			{
				WriteLookupLoad(writer, lookup);
			}

			if (rowSet.Update is RowSetUpdate update)
			{
				WriteUpdate(writer, operation.Table, rowSet, update, progress, cancellationToken);
			}
			else
			{
				WriteRowSet(writer, operation.Table, rowSet, progress, cancellationToken);
			}
		}

		WritePostGeneration(writer, blueprint);

		writer.WriteLine($"{INDENT}COMMIT TRANSACTION;");

		foreach (RowSnapshot snapshot in blueprint.Snapshots)
		{
			writer.WriteLine($"{INDENT}{snapshot.BuildDropStatement()}");
		}

		writer.WriteLine("END TRY");
		writer.WriteLine("BEGIN CATCH");
		writer.WriteLine($"{INDENT}IF XACT_STATE() <> 0");
		writer.WriteLine($"{INDENT}{INDENT}ROLLBACK TRANSACTION;");
		writer.WriteLine();
		writer.WriteLine($"{INDENT}THROW;");
		writer.WriteLine("END CATCH;");
	}

	/// <summary>
	///	Writes the script banner, totals, run order and warnings.
	/// </summary>
	/// <param name="writer">
	///	The text writer receiving the script.
	/// </param>
	/// <param name="blueprint">
	///	The generation blueprint being described.
	/// </param>
	private static void WriteHeader(TextWriter writer, GenerationBlueprint blueprint)
	{
		string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

		writer.WriteLine(SEPARATOR_LINE);
		writer.WriteLine($"-- Generated by DataGenerator on {timestamp}");
		writer.WriteLine(
			$"-- {FormatCount(blueprint.Tables.Count, "table")}, "
			+ $"{FormatCount(blueprint.RowSetCount, "row set")}, "
			+ $"{FormatCount(blueprint.TotalRowCount, "new row")}"
			+ (blueprint.UpdatedRowCount > 0 ? $", {FormatCount(blueprint.UpdatedRowCount, "changed row")}" : string.Empty)
		);
		writer.WriteLine("--");

		foreach (TableBlueprint table in blueprint.Tables)
		{
			writer.WriteLine(
				$"--   {SqlSyntax.ToCommentText(table.Table.FullyQualifiedName)}: "
				+ $"{FormatCount(table.RowSets.Count, "row set")}, {FormatCount(table.TotalRowCount, "new row")}"
				+ (table.UpdatedRowCount > 0 ? $", {FormatCount(table.UpdatedRowCount, "changed row")}" : string.Empty)
			);
		}

		if (blueprint.StepCount > 1 || blueprint.UpdatedRowCount > 0)
		{
			writer.WriteLine("--");
			writer.WriteLine("-- Run order:");

			foreach (GenerationOperation operation in blueprint.Operations)
			{
				writer.WriteLine($"--   {SqlSyntax.ToCommentText(DescribeOperation(operation))}");
			}
		}

		if (blueprint.TablesToClear.Count > 0)
		{
			writer.WriteLine("--");
			writer.WriteLine(
				$"-- WARNING: all existing rows of {FormatCount(blueprint.TablesToClear.Count, "table")} "
				+ "are deleted before the new rows are inserted."
			);
		}

		if (blueprint.PostGeneration is PostGenerationScript postGeneration)
		{
			writer.WriteLine("--");
			writer.WriteLine(
				$"-- Then runs {FormatCount(postGeneration.Statements.Count, "post-generation statement")} "
				+ $"in {SqlSyntax.ToCommentText(SqlSyntax.QuoteIdentifier(postGeneration.DatabaseName))} before the commit."
			);
		}

		writer.WriteLine(SEPARATOR_LINE);
		writer.WriteLine();
	}

	/// <summary>
	///	Writes table-variable declarations and cleanup for generated keys, sampled pools and temporary tables.
	/// </summary>
	/// <param name="writer">
	///	The text writer receiving the script.
	/// </param>
	/// <param name="blueprint">
	///	The generation blueprint whose declarations are needed.
	/// </param>
	private void WriteDeclarations(TextWriter writer, GenerationBlueprint blueprint)
	{
		bool hasDeclarations = false;

		foreach (TableBlueprint table in blueprint.Tables)
		{
			if (table.Keys?.OutputVariableName is string variableName)
			{
				writer.WriteLine($"-- Keys of {SqlSyntax.ToCommentText(table.Table.FullyQualifiedName)} used by \"Generated key\" columns");
				writer.WriteLine($"DECLARE {variableName} TABLE ({DescribeVariableColumns(table.Keys.Columns)});");
				hasDeclarations = true;
			}
		}

		foreach (ExistingKeyPool pool in blueprint.ExistingKeyPools)
		{
			writer.WriteLine($"-- Sample of existing keys of {SqlSyntax.ToCommentText(pool.ReferencedTableName)} used by \"Existing key\" columns");
			writer.WriteLine($"DECLARE {pool.VariableName} TABLE ({DescribeVariableColumns(pool.TargetColumns)});");
			writer.WriteLine($"DECLARE {pool.CountVariableName} INT = 0;");
			hasDeclarations = true;
		}

		foreach (LookupPool lookup in blueprint.LookupPools)
		{
			writer.WriteLine($"-- Values of {SqlSyntax.ToCommentText(lookup.Lookup.SourceDisplayName)} used by {SqlSyntax.ToCommentText(lookup.Location)}");
			writer.WriteLine($"DECLARE {lookup.VariableName} TABLE ({DescribeVariableColumns([lookup.TargetColumn])});");
			writer.WriteLine($"DECLARE {lookup.CountVariableName} INT = 0;");
			hasDeclarations = true;
		}

		List<string> temporaryTables = [
			.. blueprint.Snapshots.Select(snapshot => snapshot.BuildDropStatement()),
			.. blueprint.Updates.Select(update => update.BuildDropStatement())
		];

		if (temporaryTables.Count > 0)
		{
			writer.WriteLine("-- Temporary tables left behind by an earlier, failed run of this script");

			foreach (string statement in temporaryTables)
			{
				writer.WriteLine(statement);
			}

			hasDeclarations = true;
		}

		if (blueprint.ResetIdentitySeeds && blueprint.TablesToClear.Any(SqlCleanupStatements.HasIdentityColumn))
		{
			writer.WriteLine(SqlCleanupStatements.CreateReseedDeclaration());
			hasDeclarations = true;
		}

		if (hasDeclarations)
		{
			writer.WriteLine();
		}
	}

	/// <summary>
	///	Writes optional table deletion and identity reseed statements.
	/// </summary>
	/// <param name="writer">
	///	The text writer receiving the script.
	/// </param>
	/// <param name="blueprint">
	///	The generation blueprint whose cleanup settings are written.
	/// </param>
	private static void WriteCleanup(TextWriter writer, GenerationBlueprint blueprint)
	{
		if (blueprint.TablesToClear.Count == 0)
		{
			return;
		}

		writer.WriteLine($"{INDENT}-- Delete existing rows (tables that reference other tables first)");

		foreach (TableModel table in blueprint.TablesToClear)
		{
			writer.WriteLine($"{INDENT}{SqlCleanupStatements.CreateDeleteStatement(table)}");
		}

		writer.WriteLine();

		List<TableModel> identityTables =
			blueprint.ResetIdentitySeeds
				? [.. blueprint.TablesToClear.Where(SqlCleanupStatements.HasIdentityColumn)]
				: [];

		if (identityTables.Count == 0)
		{
			return;
		}

		writer.WriteLine($"{INDENT}-- Restart identity values at their seed");

		foreach (TableModel table in identityTables)
		{
			foreach (string statement in SqlCleanupStatements.CreateReseedStatements(table))
			{
				writer.WriteLine($"{INDENT}{statement}");
			}
		}

		writer.WriteLine();
	}

	/// <summary>
	///	Writes the stored procedures and SQL the user asked to run after the inserts. Their SQL is written exactly as typed
	///	(not indented), so text that spans several lines inside quotes is not changed.
	/// </summary>
	/// <param name="writer">
	///	The text writer receiving the script.
	/// </param>
	/// <param name="blueprint">
	///	The generation blueprint whose post-generation SQL is written.
	/// </param>
	private static void WritePostGeneration(TextWriter writer, GenerationBlueprint blueprint)
	{
		if (blueprint.PostGeneration is not PostGenerationScript postGeneration)
		{
			return;
		}

		writer.WriteLine($"{INDENT}-- Post-generation SQL, run before the transaction is committed");
		writer.WriteLine($"{INDENT}USE {SqlSyntax.QuoteIdentifier(postGeneration.DatabaseName)};");
		writer.WriteLine();

		foreach (PostGenerationStatement statement in postGeneration.Statements)
		{
			writer.WriteLine($"{INDENT}-- {SqlSyntax.ToCommentText(statement.Description)}");

			if (statement.Kind == PostGenerationStatementKind.StoredProcedure)
			{
				writer.WriteLine($"{INDENT}{statement.Sql}");
			}
			else
			{
				writer.WriteLine(statement.Sql);
			}

			writer.WriteLine();
		}
	}

	/// <summary>
	///	Copies the rows that exist before anything is inserted, for the "Generated rows" and "Existing rows" scopes.
	/// </summary>
	/// <param name="writer">
	///	The text writer receiving the script.
	/// </param>
	/// <param name="blueprint">
	///	The generation blueprint whose snapshots are written.
	/// </param>
	private static void WriteSnapshots(TextWriter writer, GenerationBlueprint blueprint)
	{
		if (blueprint.Snapshots.Count == 0)
		{
			return;
		}

		writer.WriteLine($"{INDENT}-- Remember which rows exist before this run");

		foreach (RowSnapshot snapshot in blueprint.Snapshots)
		{
			writer.WriteLine($"{INDENT}{snapshot.BuildCreateStatement()}");
		}

		writer.WriteLine();
	}

	/// <summary>
	///	Writes SQL that loads and validates one value-from-table lookup variable.
	/// </summary>
	/// <param name="writer">
	///	The text writer receiving the script.
	/// </param>
	/// <param name="lookup">
	///	The lookup pool to load.
	/// </param>
	private static void WriteLookupLoad(TextWriter writer, LookupPool lookup)
	{
		string valueColumn = SqlSyntax.QuoteIdentifier(GeneratedKeyTable.GetValueColumnName(0));

		writer.WriteLine($"{INDENT}-- Values of {SqlSyntax.ToCommentText(lookup.Lookup.SourceDisplayName)} for {SqlSyntax.ToCommentText(lookup.Location)}");
		writer.WriteLine($"{INDENT}INSERT INTO {lookup.VariableName} ({valueColumn})");
		writer.WriteLine($"{INDENT}{lookup.SelectStatement};");
		writer.WriteLine($"{INDENT}SET {lookup.CountVariableName} = @@ROWCOUNT;");
		writer.WriteLine();
		writer.WriteLine($"{INDENT}IF {lookup.CountVariableName} < {lookup.RequiredCount}");
		writer.WriteLine($"{INDENT}BEGIN");
		writer.WriteLine(
			$"{INDENT}{INDENT}THROW {LOOKUP_SHORTAGE_ERROR}, {SqlSyntax.QuoteUnicodeText($"{lookup.DescribeShortage()} ({lookup.Location})")}, 1;"
		);
		writer.WriteLine($"{INDENT}END;");
		writer.WriteLine();
	}

	/// <summary>
	///	Writes an update set: its new values go into a staging table, then randomly chosen matching rows are changed.
	/// </summary>
	/// <param name="writer">
	///	The text writer receiving the script.
	/// </param>
	/// <param name="table">
	///	The table blueprint that owns the row set.
	/// </param>
	/// <param name="rowSet">
	///	The row set whose values are written.
	/// </param>
	/// <param name="update">
	///	The update metadata with staging and update SQL.
	/// </param>
	/// <param name="progress">
	///	The progress reporter for row writing.
	/// </param>
	/// <param name="cancellationToken">
	///	Token used to cancel script generation.
	/// </param>
	/// <exception cref="OperationCanceledException">
	///	Thrown when <paramref name="cancellationToken"/> is cancelled.
	/// </exception>
	private void WriteUpdate(
		TextWriter         writer,
		TableBlueprint     table,
		RowSetBlueprint    rowSet,
		RowSetUpdate       update,
		GenerationProgress progress,
		CancellationToken  cancellationToken
	)
	{
		RowSetPlan   plan         = rowSet.Plan;
		string       insertPrefix = update.CreateInsertPrefix();
		List<string> batch        = new(SCRIPT_BATCH_SIZE);

		writer.WriteLine(
			$"{INDENT}-- {SqlSyntax.ToCommentText($"{table.Table.FullyQualifiedName} › Set '{plan.Name}'")}: "
			+ $"change {FormatCount(plan.RowCount, "row")}"
		);
		writer.WriteLine($"{INDENT}{update.CreateStatement}");

		for (long rowIndex = 0; rowIndex < plan.RowCount; ++rowIndex)
		{
			cancellationToken.ThrowIfCancellationRequested();

			object?[] values = _rowValueBuilder.Build(table, rowSet, rowIndex);

			batch.Add($"{(rowIndex + 1).ToString(CultureInfo.InvariantCulture)}, {FormatValues(rowSet, values)}");

			if (batch.Count == SCRIPT_BATCH_SIZE)
			{
				WriteBatch(writer, insertPrefix, batch);
			}

			progress.ReportRows("Writing", table.Table.DisplayName, plan.Name, rowIndex + 1, plan.RowCount);
		}

		WriteBatch(writer, insertPrefix, batch);
		writer.WriteLine();
		writer.WriteLine($"{INDENT}{update.UpdateStatement}");

		if (update.RequiredCount > 0)
		{
			writer.WriteLine();
			writer.WriteLine($"{INDENT}IF @@ROWCOUNT < {update.RequiredCount}");
			writer.WriteLine($"{INDENT}BEGIN");
			writer.WriteLine(
				$"{INDENT}{INDENT}THROW {UPDATE_SHORTAGE_ERROR}, {SqlSyntax.QuoteUnicodeText($"{update.DescribeShortage(null)} ({update.Location})")}, 1;"
			);
			writer.WriteLine($"{INDENT}END;");
		}

		writer.WriteLine();
		writer.WriteLine($"{INDENT}DROP TABLE {update.StagingTableName};");
		writer.WriteLine();
	}

	/// <summary>
	///	Formats one operation for the script run-order header.
	/// </summary>
	/// <param name="operation">
	///	The operation to describe.
	/// </param>
	/// <returns>
	///	A readable step, table, row-set and action description.
	/// </returns>
	private static string DescribeOperation(GenerationOperation operation)
	{
		RowSetPlan plan   = operation.RowSet.Plan;
		string     action = plan.IsUpdate ? $"change {FormatCount(plan.RowCount, "row")}" : $"insert {FormatCount(plan.RowCount, "row")}";

		return $"Step {plan.Step}: {operation.Table.Table.FullyQualifiedName} › Set '{plan.Name}' ({action})";
	}

	/// <summary>
	///	Writes SQL that loads and validates one existing-key sample variable.
	/// </summary>
	/// <param name="writer">
	///	The text writer receiving the script.
	/// </param>
	/// <param name="pool">
	///	The existing-key pool to load.
	/// </param>
	private void WritePoolLoad(TextWriter writer, ExistingKeyPool pool)
	{
		string columns = string.Join(
			", ",
			Enumerable.Range(0, pool.TargetColumns.Count).Select(index => SqlSyntax.QuoteIdentifier(GeneratedKeyTable.GetValueColumnName(index)))
		);

		writer.WriteLine($"{INDENT}-- Sample existing keys of {SqlSyntax.ToCommentText(pool.ReferencedTableName)}");
		writer.WriteLine($"{INDENT}INSERT INTO {pool.VariableName} ({columns})");
		writer.WriteLine($"{INDENT}{pool.BuildSelectStatement(_converter)};");
		writer.WriteLine($"{INDENT}SET {pool.CountVariableName} = @@ROWCOUNT;");
		writer.WriteLine();
		writer.WriteLine($"{INDENT}IF {pool.CountVariableName} = 0");
		writer.WriteLine($"{INDENT}BEGIN");
		writer.WriteLine($"{INDENT}{INDENT}THROW 50001, {SqlSyntax.QuoteUnicodeText(pool.DescribeEmptyPool())}, 1;");
		writer.WriteLine($"{INDENT}END;");
		writer.WriteLine();
	}

	/// <summary>
	///	Writes the INSERT statements for one insert row set and records any captured keys.
	/// </summary>
	/// <param name="writer">
	///	The text writer receiving the script.
	/// </param>
	/// <param name="table">
	///	The table blueprint that owns the row set.
	/// </param>
	/// <param name="rowSet">
	///	The row set whose rows are written.
	/// </param>
	/// <param name="progress">
	///	The progress reporter for row writing.
	/// </param>
	/// <param name="cancellationToken">
	///	Token used to cancel script generation.
	/// </param>
	/// <exception cref="OperationCanceledException">
	///	Thrown when <paramref name="cancellationToken"/> is cancelled.
	/// </exception>
	private void WriteRowSet(
		TextWriter         writer,
		TableBlueprint     table,
		RowSetBlueprint    rowSet,
		GenerationProgress progress,
		CancellationToken  cancellationToken
	)
	{
		RowSetPlan         plan         = rowSet.Plan;
		GeneratedKeyTable? keys         = table.Keys;
		string             insertPrefix = CreateInsertPrefix(table.Table, rowSet) + CreateOutputClause(keys);
		List<string>       batch        = new(SCRIPT_BATCH_SIZE);

		writer.WriteLine(
			$"{INDENT}-- {SqlSyntax.ToCommentText($"{table.Table.FullyQualifiedName} › Set '{plan.Name}'")}: "
			+ FormatCount(plan.RowCount, "row")
		);

		for (long rowIndex = 0; rowIndex < plan.RowCount; ++rowIndex)
		{
			cancellationToken.ThrowIfCancellationRequested();

			object?[] values = _rowValueBuilder.Build(table, rowSet, rowIndex);

			if (rowSet.Sources.Count == 0)
			{
				writer.WriteLine($"{INDENT}{insertPrefix} DEFAULT VALUES;");
			}
			else
			{
				batch.Add(FormatValues(rowSet, values));

				if (batch.Count == SCRIPT_BATCH_SIZE)
				{
					WriteBatch(writer, insertPrefix, batch);
				}
			}

			// Captured rows are only ever picked at random, so the order of OUTPUT rows does not matter.
			if (keys?.OutputVariableName is not null)
			{
				keys.AddOutputRow();
			}
			else
			{
				keys?.AddRow(RowValueBuilder.GetKeyValues(rowSet, values));
			}

			progress.ReportRows("Writing", table.Table.DisplayName, plan.Name, rowIndex + 1, plan.RowCount);
		}

		WriteBatch(writer, insertPrefix, batch);
		writer.WriteLine();
	}

	/// <summary>
	///	Writes a pending multi-row VALUES batch and clears it.
	/// </summary>
	/// <param name="writer">
	///	The text writer receiving the script.
	/// </param>
	/// <param name="insertPrefix">
	///	The INSERT prefix that precedes VALUES.
	/// </param>
	/// <param name="batch">
	///	The formatted value rows to write.
	/// </param>
	private static void WriteBatch(TextWriter writer, string insertPrefix, List<string> batch)
	{
		if (batch.Count == 0)
		{
			return;
		}

		writer.WriteLine($"{INDENT}{insertPrefix} VALUES");

		for (int index = 0; index < batch.Count; ++index)
		{
			writer.WriteLine($"{INDENT}{INDENT}({batch[index]}){(index == batch.Count - 1 ? ";" : ",")}");
		}

		batch.Clear();
	}

	/// <summary>
	///	Builds the INSERT prefix for a row set.
	/// </summary>
	/// <param name="table">
	///	The table receiving the inserted rows.
	/// </param>
	/// <param name="rowSet">
	///	The row set whose source columns are inserted.
	/// </param>
	/// <returns>
	///	The INSERT statement prefix, without VALUES or DEFAULT VALUES.
	/// </returns>
	private static string CreateInsertPrefix(TableModel table, RowSetBlueprint rowSet)
	{
		if (rowSet.Sources.Count == 0)
		{
			return $"INSERT INTO {table.FullyQualifiedName}";
		}

		string columns = string.Join(", ", rowSet.Sources.Select(source => SqlSyntax.QuoteIdentifier(source.Rule.Column.Name)));

		return $"INSERT INTO {table.FullyQualifiedName} ({columns})";
	}

	/// <summary>
	///	Captures key values that SQL Server produces (for example identities) for later "Generated key" references.
	/// </summary>
	/// <param name="keys">
	///	The generated-key table to capture into, or <see langword="null"/> when no keys are captured.
	/// </param>
	/// <returns>
	///	The OUTPUT clause, or an empty string when no script variable is needed.
	/// </returns>
	private static string CreateOutputClause(GeneratedKeyTable? keys)
	{
		if (keys?.OutputVariableName is null)
		{
			return string.Empty;
		}

		string insertedColumns = string.Join(", ", keys.Columns.Select(column => $"INSERTED.{SqlSyntax.QuoteIdentifier(column.Name)}"));
		string valueColumns    = string.Join(
			", ",
			Enumerable.Range(0, keys.Columns.Count).Select(index => SqlSyntax.QuoteIdentifier(GeneratedKeyTable.GetValueColumnName(index)))
		);

		return $" OUTPUT {insertedColumns} INTO {keys.OutputVariableName} ({valueColumns})";
	}

	/// <summary>
	///	Formats one generated row as SQL literals in source order.
	/// </summary>
	/// <param name="rowSet">
	///	The row-set blueprint that supplies column metadata.
	/// </param>
	/// <param name="values">
	///	The generated values to format.
	/// </param>
	/// <returns>
	///	The comma-separated SQL literal list for the row.
	/// </returns>
	private string FormatValues(RowSetBlueprint rowSet, object?[] values)
	{
		StringBuilder builder = new();

		for (int index = 0; index < values.Length; ++index)
		{
			if (index > 0)
			{
				_ = builder.Append(", ");
			}

			_ = builder.Append(_converter.ToSqlLiteral(rowSet.Sources[index].Rule.Column, values[index]));
		}

		return builder.ToString();
	}

	/// <summary>
	///	Describes the columns of a script table variable used for captured values.
	/// </summary>
	/// <param name="columns">
	///	The value columns to include after the row number.
	/// </param>
	/// <returns>
	///	A comma-separated list of table-variable column declarations.
	/// </returns>
	private string DescribeVariableColumns(IReadOnlyList<ColumnModel> columns)
	{
		StringBuilder builder = new("[RowNumber] INT IDENTITY(1, 1) PRIMARY KEY");

		for (int index = 0; index < columns.Count; ++index)
		{
			_ = builder
				.Append(", ")
				.Append(SqlSyntax.QuoteIdentifier(GeneratedKeyTable.GetValueColumnName(index)))
				.Append(' ')
				.Append(_converter.GetTypeDeclaration(columns[index]))
				.Append(" NULL");
		}

		return builder.ToString();
	}

	/// <summary>
	///	Formats a count and noun for script comments.
	/// </summary>
	/// <param name="count">
	///	The count to format.
	/// </param>
	/// <param name="noun">
	///	The singular noun to pluralise when needed.
	/// </param>
	/// <returns>
	///	The formatted count and noun.
	/// </returns>
	private static string FormatCount(long count, string noun)
		=> $"{count.ToString("N0", CultureInfo.InvariantCulture)} {noun}{(count == 1 ? string.Empty : "s")}";

	/// <summary>
	///	Tries to delete an incomplete temporary script file, ignoring harmless file-system failures.
	/// </summary>
	/// <param name="path">
	///	The file path to delete.
	/// </param>
	private static void TryDeleteFile(string path)
	{
		try
		{
			File.Delete(path);
		}
		catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
		{
			// The incomplete temporary file is harmless; the original error is more important.
		}
	}
}