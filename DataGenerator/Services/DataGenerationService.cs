using System.Data;
using System.Globalization;
using System.IO;
using System.Text;
using DataGenerator.Models;
using Microsoft.Data.SqlClient;

namespace DataGenerator.Services;

public sealed class DataGenerationService : IDataGenerationService
{
	private const int SQL_COMMAND_TIMEOUT_SECONDS  = 120;
	private const int DEFAULT_STRING_LENGTH        = 20;
	private const int MAXIMUM_RANDOM_STRING_LENGTH = 200;
	private const int MAXIMUM_RANDOM_BINARY_LENGTH = 256;

	private readonly IRegexValueGenerator _regexValueGenerator;

	public DataGenerationService(IRegexValueGenerator regexValueGenerator)
	{
		_regexValueGenerator = regexValueGenerator ?? throw new ArgumentNullException(nameof(regexValueGenerator));
	}

	public async Task GenerateAsync(
		GenerationRequest  request,
		IProgress<string>? progress,
		CancellationToken  cancellationToken
	)
	{
		ArgumentNullException.ThrowIfNull(request);
		ValidateRequest(request);

		IReadOnlyList<TableModel>         orderedTables = OrderTablesByDependencies(request.Tables);
		Dictionary<string, List<object?>> generatedKeys = new(StringComparer.OrdinalIgnoreCase);

		if (request.Mode == DataGenerator.Models.GenerationMode.SqlFile)
		{
			string script = BuildSqlScript(
				orderedTables,
				generatedKeys,
				progress,
				cancellationToken
			);

			await File.WriteAllTextAsync(
				request.OutputFilePath!,
				script,
				new UTF8Encoding(false),
				cancellationToken
			);

			progress?.Report($"SQL script written to '{request.OutputFilePath}'.");
			return;
		}

		await InsertDirectlyAsync(
			request.ConnectionString!,
			orderedTables,
			generatedKeys,
			progress,
			cancellationToken
		);
	}

	private string BuildSqlScript(
		IReadOnlyList<TableModel>         orderedTables,
		Dictionary<string, List<object?>> generatedKeys,
		IProgress<string>?                progress,
		CancellationToken                 cancellationToken
	)
	{
		StringBuilder script = new();

		_ = script.AppendLine("SET NOCOUNT ON;");
		_ = script.AppendLine("SET XACT_ABORT ON;");
		_ = script.AppendLine();
		_ = script.AppendLine("BEGIN TRY");
		_ = script.AppendLine("	BEGIN TRANSACTION;");
		_ = script.AppendLine();

		foreach (TableModel table in orderedTables)
		{
			cancellationToken.ThrowIfCancellationRequested();

			progress?.Report($"Generating {table.RowCount} row(s) for {table.FullyQualifiedName}.");

			List<ColumnModel> insertColumns = [.. table.Columns.Where(IsInsertableColumn)];

			if (insertColumns.Count == 0)
			{
				AppendDefaultValuesStatements(
					script,
					table,
					table.RowCount
				);

				continue;
			}

			for (int rowIndex = 0; rowIndex < table.RowCount; ++rowIndex)
			{
				cancellationToken.ThrowIfCancellationRequested();

				List<object?> values = CreateRowValues(
					table,
					insertColumns,
					rowIndex,
					generatedKeys
				);

				string columnList = 
					string.Join(
						", ",
						insertColumns.Select(column => QuoteIdentifier(column.Name))
					);

				string valueList =
					string.Join(
						", ",
						values.Select(
							(value, valueIndex) => ToSqlLiteral(value, insertColumns[valueIndex])
						)
					);

				_ = script.Append("	INSERT INTO ");
				_ = script.Append(table.FullyQualifiedName);
				_ = script.Append(" (");
				_ = script.Append(columnList);
				_ = script.Append(") VALUES (");
				_ = script.Append(valueList);
				_ = script.AppendLine(");");

				CaptureKnownPrimaryKeys(
					table,
					insertColumns,
					values,
					generatedKeys
				);
			}

			_ = script.AppendLine();
		}

		_ = script.AppendLine("	COMMIT TRANSACTION;");
		_ = script.AppendLine("END TRY");
		_ = script.AppendLine("BEGIN CATCH");
		_ = script.AppendLine("	IF XACT_STATE() <> 0");
		_ = script.AppendLine("		ROLLBACK TRANSACTION;");
		_ = script.AppendLine();
		_ = script.AppendLine("	THROW;");
		_ = script.AppendLine("END CATCH;");

		return script.ToString();
	}

	private static void AppendDefaultValuesStatements(
		StringBuilder script,
		TableModel    table,
		int           rowCount
	)
	{
		for (int rowIndex = 0; rowIndex < rowCount; ++rowIndex)
		{
			_ = script.Append("	INSERT INTO ");
			_ = script.Append(table.FullyQualifiedName);
			_ = script.AppendLine(" DEFAULT VALUES;");
		}

		_ = script.AppendLine();
	}

	private async Task InsertDirectlyAsync(
		string                           connectionString,
		IReadOnlyList<TableModel>        orderedTables,
		Dictionary<string,List<object?>> generatedKeys,
		IProgress<string>?               progress,
		CancellationToken                cancellationToken
	)
	{
		await using SqlConnection connection = new(connectionString);
		await connection.OpenAsync(cancellationToken);

		await using SqlTransaction transaction = (SqlTransaction)await
			connection
				.BeginTransactionAsync(
					IsolationLevel.ReadCommitted,
					cancellationToken
				);

		try
		{
			foreach (TableModel table in orderedTables)
			{
				cancellationToken.ThrowIfCancellationRequested();

				progress?.Report($"Inserting {table.RowCount} row(s) into {table.FullyQualifiedName}.");

				List<ColumnModel> insertColumns = [.. table.Columns.Where(IsInsertableColumn)];

				ColumnModel? identityPrimaryKey =
					table.Columns.FirstOrDefault(column => column.IsIdentity && column.IsPrimaryKey);

				for (int rowIndex = 0; rowIndex < table.RowCount; ++rowIndex)
				{
					cancellationToken.ThrowIfCancellationRequested();

					List<object?> values =
						CreateRowValues(
							table,
							insertColumns,
							rowIndex,
							generatedKeys
						);

					await using SqlCommand command =
						CreateInsertCommand(
							connection,
							transaction,
							table,
							insertColumns,
							values,
							identityPrimaryKey
						);

					object? insertedIdentity = await command.ExecuteScalarAsync(cancellationToken);

					if (	identityPrimaryKey is not null
							&& insertedIdentity is not null
							&& insertedIdentity != DBNull.Value
					)
					{
						CapturePrimaryKeyValue(
							table,
							identityPrimaryKey,
							insertedIdentity,
							generatedKeys
						);
					}

					CaptureKnownPrimaryKeys(
						table,
						insertColumns,
						values,
						generatedKeys
					);
				}
			}

			await transaction.CommitAsync(cancellationToken);
			progress?.Report("All rows were inserted successfully and the transaction was committed.");
		}
		catch
		{
			try
			{
				await transaction.RollbackAsync(CancellationToken.None);
			}
			catch
			{
				// Preserve the original insertion exception.
			}

			throw;
		}
	}

	private static SqlCommand CreateInsertCommand(
		SqlConnection              connection,
		SqlTransaction             transaction,
		TableModel                 table,
		IReadOnlyList<ColumnModel> columns,
		IReadOnlyList<object?>     values,
		ColumnModel?               identityPrimaryKey
	)
	{
		string outputClause = identityPrimaryKey == null
										? string.Empty
										: $" OUTPUT INSERTED.{QuoteIdentifier(identityPrimaryKey.Name)}";
		string sql;

		if (columns.Count == 0)
		{
			sql = $"INSERT INTO {table.FullyQualifiedName}{outputClause} DEFAULT VALUES;";
		}
		else
		{
			string columnList =
				string.Join(
					", ",
					columns.Select(column => QuoteIdentifier(column.Name))
				);

			string parameterList =
				string.Join(
					", ",
					columns.Select((column, parameterIndex) => $"@p{parameterIndex}")
				);

			sql = $"INSERT INTO {table.FullyQualifiedName} ({columnList}){outputClause} VALUES ({parameterList});";
		}

		SqlCommand command = new(sql, connection, transaction)
		{
			CommandTimeout = SQL_COMMAND_TIMEOUT_SECONDS
		};

		for (int parameterIndex = 0; parameterIndex < columns.Count; ++parameterIndex)
		{
			SqlParameter parameter =
				command
					.Parameters
					.Add(
						$"@p{parameterIndex}",
						GetSqlDbType(columns[parameterIndex])
					);

			ConfigureParameter(parameter, columns[parameterIndex]);

			parameter.Value = values[parameterIndex] ?? DBNull.Value;
		}

		return command;
	}

	private List<object?> CreateRowValues(
		TableModel                        table,
		IReadOnlyList<ColumnModel>        columns,
		int                               rowIndex,
		Dictionary<string, List<object?>> generatedKeys
	)
	{
		List<object?> values = [];

		foreach (ColumnModel column in columns)
		{
			object? value = GenerateValue(
				table,
				column,
				rowIndex,
				generatedKeys
			);

			values.Add(value);
		}

		return values;
	}

	private object? GenerateValue(
		TableModel                        table,
		ColumnModel                       column,
		int                               rowIndex,
		Dictionary<string, List<object?>> generatedKeys
	)
		=> column.GenerationMode switch
		{
			ValueGenerationMode.Fixed => ConvertTextToSqlType(column.FixedValue, column),

			ValueGenerationMode.Sequence
				=> ConvertSequenceValue(
						checked(column.SequenceStart + (column.SequenceStep * rowIndex)),
						column
					),

			ValueGenerationMode.Regex
				=> ConvertGeneratedTextToSqlType(
						_regexValueGenerator.Generate(
							column.RegexPattern,
							GetMaximumStringLength(column)
						),
						column
					),

			ValueGenerationMode.ExistingForeignKey => GetForeignKeyValue(table, column, generatedKeys),
			ValueGenerationMode.GeneratedForeignKey => GetForeignKeyValue(table, column, generatedKeys),
			ValueGenerationMode.Null when column.IsNullable => null,
			ValueGenerationMode.Null
				=> throw new InvalidOperationException($"{table.FullyQualifiedName}.{QuoteIdentifier(column.Name)} is not nullable."),
			ValueGenerationMode.DatabaseGenerated
				=> throw new InvalidOperationException(
						$"{table.FullyQualifiedName}.{QuoteIdentifier(column.Name)} is configured as "
						+ "database-generated and must not be included in an INSERT statement."
					),
			_ => GenerateRandomValue(column)
		};

	private static object? GetForeignKeyValue(
		TableModel                        table,
		ColumnModel                       column,
		Dictionary<string, List<object?>> generatedKeys
	)
	{
		ForeignKeyModel? foreignKey =
			table
				.ForeignKeys
				.FirstOrDefault(
					item => string.Equals(
									item.ParentColumn,
									column.Name,
									StringComparison.OrdinalIgnoreCase
								)
				)
				?? throw new InvalidOperationException(
						$"Column {table.FullyQualifiedName}.{QuoteIdentifier(column.Name)} is configured as a "
							+ "foreign key, but no matching foreign-key metadata was found."
					);
		string referencedColumnKey = CreateColumnKey(
			foreignKey.ReferencedDatabase,
			foreignKey.ReferencedSchema,
			foreignKey.ReferencedTable,
			foreignKey.ReferencedColumn
		);

		return
			!generatedKeys.TryGetValue(referencedColumnKey, out List<object?>? availableValues)
			|| availableValues.Count == 0
				?	throw new InvalidOperationException(
						$"No generated value is available for foreign key '{foreignKey.Name}'. Select the referenced table "
							+ $"[{foreignKey.ReferencedDatabase}].[{foreignKey.ReferencedSchema}].[{foreignKey.ReferencedTable}], "
							+ $"or configure column {QuoteIdentifier(column.Name)} with a fixed value that already exists."
					)
				: availableValues[Random.Shared.Next(availableValues.Count)];
	}

	private static object GenerateRandomValue(ColumnModel column) => column.SqlType.ToLowerInvariant() switch
	{
		"bigint"           => Random.Shared.NextInt64(1,long.MaxValue),
		"int"              => Random.Shared.Next(1,int.MaxValue),
		"smallint"         => (short)Random.Shared.Next(1,short.MaxValue),
		"tinyint"          => (byte)Random.Shared.Next(byte.MinValue,byte.MaxValue + 1),
		"bit"              => Random.Shared.Next(0, 2) == 1,
		"uniqueidentifier" => Guid.NewGuid(),
		"date"             => DateTime.Today.AddDays(-Random.Shared.Next(0, 3650)),
		"datetime"  or
		"datetime2" or
		"smalldatetime"    => DateTime.Now.AddMinutes(-Random.Shared.Next(0, 5_000_000)),
		"datetimeoffset"   => DateTimeOffset.Now.AddMinutes(-Random.Shared.Next(0, 5_000_000)),
		"time"             => TimeSpan.FromSeconds(Random.Shared.Next(0, 86_400)),
		"decimal" or
		"numeric" or
		"money"   or
		"smallmoney"       => GenerateRandomDecimal(column),
		"float"            => Random.Shared.NextDouble() * 100_000D,
		"real"             => (float)(Random.Shared.NextDouble() *100_000D),
		"binary"    or
		"varbinary" or
		"image"            => GenerateRandomBytes(column),
		"char"     or
		"varchar"  or
		"text"     or
		"nchar"    or
		"nvarchar" or
		"ntext"    or
		"xml"              => GenerateRandomString(column),
		_                  => throw new NotSupportedException(
										$"SQL type '{column.SqlType}' is not currently supported for random generation. "
											+ $"Configure the " +"column as Fixed or Null, or extend GenerateRandomValue."
									)
	};

	private static decimal GenerateRandomDecimal(ColumnModel column)
	{
		int scale                 = Math.Clamp(column.Scale ?? 2, (byte)0, (byte)8);
		int precision             = column.Precision ?? 18;
		int integerDigits         = Math.Max(1, precision - scale);
		int safeIntegerDigits     = Math.Min(integerDigits, 9);
		decimal maximumWholeValue = (decimal)Math.Pow(10, safeIntegerDigits) - 1M;
		decimal generatedValue    = (decimal)Random.Shared.NextDouble() * maximumWholeValue;

		return Math.Round(generatedValue, scale, MidpointRounding.AwayFromZero);
	}

	private static string GenerateRandomString(ColumnModel column)
	{
		const string CHARACTERS =
			"ABCDEFGHIJKLMNOPQRSTUVWXYZ"
				+ "abcdefghijklmnopqrstuvwxyz"
				+ "0123456789";

		int           maximumLength = GetMaximumStringLength(column);
		int           targetLength  = Math.Min(DEFAULT_STRING_LENGTH, maximumLength);
		StringBuilder result        = new(targetLength);

		for (int characterIndex = 0; characterIndex < targetLength; ++characterIndex)
		{
			int selectedIndex = Random.Shared.Next(CHARACTERS.Length);
			_ = result.Append(CHARACTERS[selectedIndex]);
		}

		return result.ToString();
	}

	private static byte[] GenerateRandomBytes(ColumnModel column)
	{
		int configuredLength = column.MaximumLength ?? 16;

		if (configuredLength < 0)
		{
			configuredLength = MAXIMUM_RANDOM_BINARY_LENGTH;
		}

		int    length = Math.Clamp(configuredLength, 1, MAXIMUM_RANDOM_BINARY_LENGTH);
		byte[] result = new byte[length];

		Random.Shared.NextBytes(result);
		return result;
	}

	private static int GetMaximumStringLength(ColumnModel column)
		=> !column.MaximumLength.HasValue
			|| column.MaximumLength.Value < 0
				? MAXIMUM_RANDOM_STRING_LENGTH
				: Math.Clamp(column.MaximumLength.Value, 1, MAXIMUM_RANDOM_STRING_LENGTH);

	private static object ConvertGeneratedTextToSqlType(string value, ColumnModel column) => column.SqlType.ToLowerInvariant() switch
	{
		"char"     or
		"varchar"  or
		"text"     or
		"nchar"    or
		"nvarchar" or
		"ntext"    or
		"xml" => value,
		_     => ConvertTextToSqlType(value, column)
	};

	private static object ConvertSequenceValue(long value, ColumnModel column) => column.SqlType.ToLowerInvariant() switch
	{
		"bigint"     => value,
		"int"        => checked((int)value),
		"smallint"   => checked((short)value),
		"tinyint"    => checked((byte)value),
		"decimal" or
		"numeric" or
		"money"   or
		"smallmoney" => (decimal)value,
		"char"    or
		"varchar" or
		"nchar"   or
		"nvarchar"   => value.ToString(CultureInfo.InvariantCulture),
		_            =>	throw new InvalidOperationException
									($"Column '{column.Name}' uses SQL type '{column.SqlType}', which cannot use a numeric sequence.")
	};

	private static object ConvertTextToSqlType(string input, ColumnModel column)
	{
		CultureInfo culture = CultureInfo.InvariantCulture;

		return
			column.SqlType.ToLowerInvariant() switch
			{
				"bigint"           => long.Parse(input, culture),
				"int"              => int.Parse(input, culture),
				"smallint"         => short.Parse(input, culture),
				"tinyint"          => byte.Parse(input, culture),
				"bit"              => ParseBoolean(input),
				"decimal"       or
				"numeric"       or
				"money"         or
				"smallmoney"       => decimal.Parse(input, culture),
				"float"            => double.Parse(input, culture),
				"real"             => float.Parse(input, culture),
				"uniqueidentifier" => Guid.Parse(input),
				"date"          or
				"datetime"      or
				"datetime2"     or
				"smalldatetime"    => DateTime.Parse(input, culture, DateTimeStyles.AllowWhiteSpaces),
				"datetimeoffset"   => DateTimeOffset.Parse(input, culture, DateTimeStyles.AllowWhiteSpaces),
				"time"             => TimeSpan.Parse(input, culture),
				"binary"        or
				"varbinary"     or
				"image"            => ConvertHexStringToBytes(input),
				_                  => input
			};
	}

	private static bool ParseBoolean(string input)
	{
		if (bool.TryParse(input, out bool booleanValue))
		{
			return booleanValue;
		}

		if (input == "1")
		{
			return true;
		}

		return input == "0"
					? false
					: throw new FormatException($"'{input}' is not a valid bit value. Use true, false, 1, or 0.");
	}

	private static byte[] ConvertHexStringToBytes(string input)
	{
		string normalizedInput =
			input.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
				? input[2..]
				: input;

		return Convert.FromHexString(normalizedInput);
	}

	private static string ToSqlLiteral(object? value, ColumnModel column)
		=> value is null
			? "NULL"
			: value switch
			{
				string         text
					                                =>	column.SqlType.StartsWith("n", StringComparison.OrdinalIgnoreCase)
																		? $"N'{EscapeSqlString(text)}'"
																		: $"'{EscapeSqlString(text)}'",
				bool           booleanValue        => booleanValue ? "1" : "0",
				Guid           guidValue           => $"'{guidValue:D}'",
				DateTime       dateTimeValue       => $"'{dateTimeValue:yyyy-MM-ddTHH:mm:ss.fffffff}'",
				DateTimeOffset dateTimeOffsetValue => $"'{dateTimeOffsetValue:yyyy-MM-ddTHH:mm:ss.fffffffzzz}'",
				TimeSpan       timeSpanValue       => $"'{timeSpanValue:c}'",
				byte[]         byteValues          => $"0x{Convert.ToHexString(byteValues)}",
				IFormattable   formattableValue    => formattableValue.ToString(null, CultureInfo.InvariantCulture),
				_                                  =>	throw new InvalidOperationException($"Cannot create a SQL literal for column '{column.Name}' using value type '{value.GetType().FullName}'.")
			};

	private static string EscapeSqlString(string value) => value.Replace("'", "''", StringComparison.Ordinal);

	private static void CaptureKnownPrimaryKeys(
		TableModel                        table,
		IReadOnlyList<ColumnModel>        columns,
		IReadOnlyList<object?>            values,
		Dictionary<string, List<object?>> generatedKeys
	)
	{
		for (int columnIndex = 0; columnIndex < columns.Count; ++columnIndex)
		{
			ColumnModel column = columns[columnIndex];

			if (!column.IsPrimaryKey)
			{
				continue;
			}

			CapturePrimaryKeyValue(
				table,
				column,
				values[columnIndex],
				generatedKeys
			);
		}
	}

	private static void CapturePrimaryKeyValue(
		TableModel                        table,
		ColumnModel                       column,
		object?                           value,
		Dictionary<string, List<object?>> generatedKeys
	)
	{
		string columnKey = CreateColumnKey(
			table.DatabaseName,
			table.SchemaName,
			table.Name,
			column.Name
		);

		if (!generatedKeys.TryGetValue(columnKey, out List<object?>? keyValues))
		{
			keyValues = [];
			generatedKeys.Add(columnKey, keyValues);
		}

		keyValues.Add(value);
	}

	private static IReadOnlyList<TableModel> OrderTablesByDependencies(IReadOnlyList<TableModel> tables)
	{
		List<TableModel> result               = [];
		HashSet<string>  visiting             = new(StringComparer.OrdinalIgnoreCase);
		HashSet<string>  visited              = new(StringComparer.OrdinalIgnoreCase);
		Dictionary<string, TableModel> lookup = tables.ToDictionary(GetTableKey, StringComparer.OrdinalIgnoreCase);

		foreach (TableModel table in tables)
		{
			VisitTable(
				table,
				lookup,
				visiting,
				visited,
				result
			);
		}

		return result;
	}

	private static void VisitTable(
		TableModel       table,
		IReadOnlyDictionary<string, TableModel> lookup,
		HashSet<string>  visiting,
		HashSet<string>  visited,
		List<TableModel> result
	)
	{
		string tableKey = GetTableKey(table);

		if (visited.Contains(tableKey))
		{
			return;
		}

		if (!visiting.Add(tableKey))
		{
			throw new InvalidOperationException(
				 $"A circular foreign-key dependency involving '{tableKey}' was detected. "
					+ $"Configure at least one side of the relationship with a fixed existing value."
			);
		}

		foreach (ForeignKeyModel foreignKey in table.ForeignKeys)
		{
			ColumnModel? parentColumn =
				table.Columns.FirstOrDefault(
					column =>	string.Equals(
										column.Name,
										foreignKey.ParentColumn,
										StringComparison.OrdinalIgnoreCase
									)
				);

			if (	parentColumn == null
					|| parentColumn.GenerationMode == ValueGenerationMode.Fixed
					|| parentColumn.GenerationMode == ValueGenerationMode.Null
			)
			{
				continue;
			}

			string dependencyKey = $"{foreignKey.ReferencedDatabase}.{foreignKey.ReferencedSchema}.{foreignKey.ReferencedTable}";

			if (lookup.TryGetValue(dependencyKey, out TableModel? dependency))
			{
				VisitTable(
					dependency,
					lookup,
					visiting,
					visited,
					result
				);
			}
		}

		_ = visiting.Remove(tableKey);
		_ = visited.Add(tableKey);
		result.Add(table);
	}

	private static void ValidateRequest(GenerationRequest request)
	{
		if (request.Tables.Count == 0)
		{
			throw new InvalidOperationException("Select at least one table.");
		}

		if (request.Mode == GenerationMode.SqlFile && string.IsNullOrWhiteSpace(request.OutputFilePath))
		{
			throw new InvalidOperationException("Select an output SQL file.");
		}

		if (	request.Mode == GenerationMode.SqlFile
				&& SecurePathService.IsInsideApplicationInstallationDirectory(request.OutputFilePath!)
		)
		{
			throw new InvalidOperationException(
				"The SQL output file cannot be stored inside the application installation or project directory. "
				+ "Select a location outside the application directory."
			);
		}

		if (request.Mode == GenerationMode.DirectInsert && string.IsNullOrWhiteSpace(request.ConnectionString))
		{
			throw new InvalidOperationException("A SQL Server connection is required for direct insertion.");
		}

		foreach (TableModel table in request.Tables)
		{
			if (table.RowCount < 1)
			{
				throw new InvalidOperationException($"{table.FullyQualifiedName} must generate at least one row.");
			}

			ValidateTableConfiguration(table);
		}
	}

	private static void ValidateTableConfiguration(TableModel table)
	{
		foreach (ColumnModel column in table.Columns)
		{
			if (column.IsIdentity || column.IsComputed)
			{
				continue;
			}

			if (column.GenerationMode == ValueGenerationMode.Null && !column.IsNullable)
			{
				throw new InvalidOperationException($"{table.FullyQualifiedName}.{QuoteIdentifier(column.Name)} cannot be NULL.");
			}

			if (column.GenerationMode == ValueGenerationMode.Regex && string.IsNullOrWhiteSpace(column.RegexPattern))
			{
				throw new InvalidOperationException($"{table.FullyQualifiedName}.{QuoteIdentifier(column.Name)} requires a regex pattern.");
			}

			if (	column.GenerationMode == ValueGenerationMode.Fixed
					&& string.IsNullOrWhiteSpace( column.FixedValue)
					&& !IsCharacterType(column.SqlType)
			)
			{
				throw new InvalidOperationException
					($"{table.FullyQualifiedName}.{QuoteIdentifier(column.Name)} requires a fixed value compatible with SQL type '{column.SqlType}'.");
			}
		}
	}

	private static bool IsCharacterType(string sqlType)
		=>	sqlType.Equals("char"       , StringComparison.OrdinalIgnoreCase)
			|| sqlType.Equals("varchar" , StringComparison.OrdinalIgnoreCase)
			|| sqlType.Equals("text"    , StringComparison.OrdinalIgnoreCase)
			|| sqlType.Equals("nchar"   , StringComparison.OrdinalIgnoreCase)
			|| sqlType.Equals("nvarchar", StringComparison.OrdinalIgnoreCase)
			|| sqlType.Equals("ntext"   , StringComparison.OrdinalIgnoreCase)
			|| sqlType.Equals("xml"     , StringComparison.OrdinalIgnoreCase);

	private static bool IsInsertableColumn(ColumnModel column)
		=>	!column.IsIdentity
			&& !column.IsComputed
			&& column.GenerationMode
			!= ValueGenerationMode.DatabaseGenerated;

	private static string GetTableKey(TableModel table) => $"{table.DatabaseName}.{table.SchemaName}.{table.Name}";

	private static string CreateColumnKey(string database, string schema, string table, string column)
		=> $"{database}.{schema}.{table}.{column}";

	private static string QuoteIdentifier(string identifier) => $"[{identifier.Replace("]", "]]", StringComparison.Ordinal)}]";

	private static SqlDbType GetSqlDbType(ColumnModel column) => column.SqlType.ToLowerInvariant() switch
	{
		"bigint"           => SqlDbType.BigInt,
		"binary"           => SqlDbType.Binary,
		"bit"              => SqlDbType.Bit,
		"char"             => SqlDbType.Char,
		"date"             => SqlDbType.Date,
		"datetime"         => SqlDbType.DateTime,
		"datetime2"        => SqlDbType.DateTime2,
		"datetimeoffset"   => SqlDbType.DateTimeOffset,
		"decimal"          => SqlDbType.Decimal,
		"float"            => SqlDbType.Float,
		"image"            => SqlDbType.Image,
		"int"              => SqlDbType.Int,
		"money"            => SqlDbType.Money,
		"nchar"            => SqlDbType.NChar,
		"ntext"            => SqlDbType.NText,
		"numeric"          => SqlDbType.Decimal,
		"nvarchar"         => SqlDbType.NVarChar,
		"real"             => SqlDbType.Real,
		"smalldatetime"    => SqlDbType.SmallDateTime,
		"smallint"         => SqlDbType.SmallInt,
		"smallmoney"       => SqlDbType.SmallMoney,
		"text"             => SqlDbType.Text,
		"time"             => SqlDbType.Time,
		"tinyint"          => SqlDbType.TinyInt,
		"uniqueidentifier" => SqlDbType.UniqueIdentifier,
		"varbinary"        => SqlDbType.VarBinary,
		"varchar"          => SqlDbType.VarChar,
		"xml"              => SqlDbType.Xml,
		_                  => throw new NotSupportedException($"SQL type '{column.SqlType}' is not supported for parameterised insertion.")
	};

	private static void ConfigureParameter(SqlParameter parameter, ColumnModel column)
	{
		string sqlType = column.SqlType.ToLowerInvariant();

		if (sqlType   is
			"char"     or
			"varchar"  or
			"nchar"    or
			"nvarchar" or
			"binary"   or
			"varbinary"
		)
		{
			parameter.Size =
				column.MaximumLength switch
				{
					null => 1,
					< 0  => -1,
					0    => 1,
					_    => column.MaximumLength.Value
				};
		}

		if (sqlType is "decimal" or "numeric")
		{
			parameter.Precision = column.Precision ?? 18;
			parameter.Scale     = column.Scale ?? 0;
		}

		if (sqlType is "datetime2" or "datetimeoffset" or "time")
		{
			parameter.Scale = column.Scale ?? 7;
		}
	}
}