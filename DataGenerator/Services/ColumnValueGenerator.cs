using System.Globalization;
using DataGenerator.Interfaces;
using DataGenerator.Models;

namespace DataGenerator.Services;

public sealed class ColumnValueGenerator : IColumnValueGenerator
{
	#region FIELDS
	private const int    RANDOM_TEXT_LENGTH       = 20;
	private const int    RANDOM_BINARY_LENGTH     = 16;
	private const int    UNLIMITED_TEXT_LENGTH    = 4000;
	private const int    MAXIMUM_INTEGER_DIGITS   = 6;
	private const int    MAXIMUM_RANDOM_DECIMALS  = 4;
	private const int    SECONDS_PER_DAY          = 86_400;
	private const string RANDOM_TEXT_CHARACTERS   = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

	private static readonly DateTime RANDOM_DATE_MINIMUM;
	private static readonly DateTime RANDOM_DATE_MAXIMUM;

	private readonly ISqlValueConverter     _converter;
	private readonly IRegexValueGenerator   _regexGenerator;
	private readonly IPatternValueGenerator _patternGenerator;
	private readonly IColumnValueCaster     _caster;
	#endregion FIELDS

	#region CONSTRUCTOR
	/// <summary>
	///	Sets the default values of the static fields and properties of <see cref="ColumnValueGenerator"/>.
	/// </summary>
	static ColumnValueGenerator()
	{
		RANDOM_DATE_MINIMUM = new(2000, 1, 1);
		RANDOM_DATE_MAXIMUM = new(2030, 12, 31, 23, 59, 59);
	}

	/// <summary>
	///	Creates a generator for fixed, sequence, random, regex, pattern and copied column values.
	/// </summary>
	/// <param name="converter">
	///	The converter used to create CLR values that match SQL column metadata.
	/// </param>
	/// <param name="regexGenerator">
	///	The generator used for regex-based text values.
	/// </param>
	/// <param name="patternGenerator">
	///	The generator used for DataGenerator pattern expressions.
	/// </param>
	/// <param name="caster">
	///	The caster used to copy and coerce values between columns.
	/// </param>
	/// <exception cref="ArgumentNullException">
	///	Thrown when any dependency is <see langword="null"/>.
	/// </exception>
	public ColumnValueGenerator(
		ISqlValueConverter     converter,
		IRegexValueGenerator   regexGenerator,
		IPatternValueGenerator patternGenerator,
		IColumnValueCaster     caster
	)
	{
		_converter        = converter ?? throw new ArgumentNullException(nameof(converter));
		_regexGenerator   = regexGenerator ?? throw new ArgumentNullException(nameof(regexGenerator));
		_patternGenerator = patternGenerator ?? throw new ArgumentNullException(nameof(patternGenerator));
		_caster           = caster ?? throw new ArgumentNullException(nameof(caster));
	}
	#endregion CONSTRUCTOR

	#region METHODS
	#region PUBLIC
	/// <summary>
	///	Checks whether this service can generate a value immediately for the supplied mode.
	/// </summary>
	/// <param name="mode">
	///	The generation mode to check.
	/// </param>
	/// <returns>
	///	<see langword="true"/> when the mode is generated locally; otherwise <see langword="false"/>.
	/// </returns>
	public bool CanGenerate(ValueGenerationMode mode)
		=> mode is ValueGenerationMode.Random
			or ValueGenerationMode.Fixed
			or ValueGenerationMode.Sequence
			or ValueGenerationMode.Regex
			or ValueGenerationMode.Pattern
			or ValueGenerationMode.CopyColumn
			or ValueGenerationMode.Null;

	/// <summary>
	///	Lists the other columns that a rule needs while generating a row.
	/// </summary>
	/// <param name="rule">
	///	The column rule to inspect.
	/// </param>
	/// <returns>
	///	The copied column name or pattern column references, or an empty list when the rule is independent.
	/// </returns>
	/// <exception cref="ArgumentNullException">
	///	Thrown when <paramref name="rule"/> is <see langword="null"/>.
	/// </exception>
	public IReadOnlyList<string> GetReferencedColumns(ColumnRule rule)
	{
		ArgumentNullException.ThrowIfNull(rule);

		return rule.GenerationMode switch
		{
			ValueGenerationMode.CopyColumn => string.IsNullOrWhiteSpace(rule.SourceColumnName) ? [] : [rule.SourceColumnName.Trim()],
			ValueGenerationMode.Pattern    => _patternGenerator.GetColumnReferences(rule.PatternExpression),
			_                              => []
		};
	}

	/// <summary>
	///	Generates the value for one column rule in one row.
	/// </summary>
	/// <param name="rule">
	///	The rule that describes how to produce the value.
	/// </param>
	/// <param name="rowIndex">
	///	The zero-based row index used by sequence and pattern generation.
	/// </param>
	/// <param name="rowCount">
	///	The total number of rows available to pattern generation.
	/// </param>
	/// <param name="rowValues">
	///	The values already generated for this row, required by copied column and referencing pattern rules.
	/// </param>
	/// <returns>
	///	The generated value, or <see langword="null"/> when NULL mode is used for a nullable column.
	/// </returns>
	/// <exception cref="ArgumentNullException">
	///	Thrown when <paramref name="rule"/> is <see langword="null"/>.
	/// </exception>
	/// <exception cref="InvalidOperationException">
	///	Thrown when the rule cannot be generated immediately or would produce NULL for a non-nullable column.
	/// </exception>
	public object? Generate(ColumnRule rule, long rowIndex, long rowCount, IRowValueLookup? rowValues = null)
	{
		ArgumentNullException.ThrowIfNull(rule);

		ColumnModel column = rule.Column;

		return rule.GenerationMode switch
		{
			ValueGenerationMode.Null     =>
				column.IsNullable
					? null
					: throw new InvalidOperationException($"Column [{column.Name}] does not allow NULL values."),
			ValueGenerationMode.Fixed    => _converter.ConvertText(column, rule.FixedValue),
			ValueGenerationMode.Sequence => _converter.ConvertSequenceValue(column, rule.SequenceStart + (rule.SequenceStep * rowIndex)),
			ValueGenerationMode.Regex    => _converter.ConvertGeneratedText(
				column,
				_regexGenerator.Generate(rule.RegexPattern, _converter.GetMaximumTextLength(column) ?? UNLIMITED_TEXT_LENGTH)
			),
			ValueGenerationMode.Pattern    => GeneratePatternValue(rule, rowIndex, rowCount, rowValues),
			ValueGenerationMode.CopyColumn => _caster.Cast(column, GetRowValues(rule, rowValues).GetValue(rule.SourceColumnName.Trim())),
			ValueGenerationMode.Random     => GenerateRandomValue(column),
			_                              => throw new InvalidOperationException(
				$"Values for [{column.Name}] in '{rule.GenerationMode}' mode are resolved while the data is generated."
			)
		};
	}

	/// <summary>
	///	Describes the random values that would be produced for a column.
	/// </summary>
	/// <param name="column">
	///	The column whose random generation behaviour is described.
	/// </param>
	/// <returns>
	///	A short user-facing description of the random value range or a message that random values are unsupported.
	/// </returns>
	/// <exception cref="ArgumentNullException">
	///	Thrown when <paramref name="column"/> is <see langword="null"/>.
	/// </exception>
	public string DescribeRandomValues(ColumnModel column)
	{
		ArgumentNullException.ThrowIfNull(column);

		string sqlType = NormalizeType(column);

		return _converter.GetCategory(column) switch
		{
			SqlTypeCategory.Integer     => DescribeRandomInteger(sqlType),
			SqlTypeCategory.Decimal     => $"Random number from 0 to {DescribeDecimalMaximum(column, sqlType)}",
			SqlTypeCategory.Boolean     => "Random 0 or 1",
			SqlTypeCategory.Text        => $"Random {GetRandomTextLength(column)} letters and digits",
			SqlTypeCategory.DateTime    =>
				sqlType == "date"
					? "Random date between 2000 and 2030"
					: "Random date and time between 2000 and 2030",
			SqlTypeCategory.Time        => "Random time of day",
			SqlTypeCategory.Guid        => "New random GUID for every row",
			SqlTypeCategory.Binary      => $"Random {GetRandomBinaryLength(column)} bytes",
			SqlTypeCategory.RowVersion  => "Generated by SQL Server",
			_                           => $"Random values are not supported for {_converter.GetDisplayType(column)}"
		};
	}
	#endregion PUBLIC

	#region PRIVATE
	/// <summary>
	///	Gets the current row lookup required by rules that reference another column.
	/// </summary>
	/// <param name="rule">
	///	The rule that needs row values, used to name the column in the error message.
	/// </param>
	/// <param name="rowValues">
	///	The current row values supplied by the row generator.
	/// </param>
	/// <returns>
	///	The supplied row value lookup.
	/// </returns>
	/// <exception cref="InvalidOperationException">
	///	Thrown when <paramref name="rowValues"/> is <see langword="null"/>.
	/// </exception>
	private static IRowValueLookup GetRowValues(ColumnRule rule, IRowValueLookup? rowValues)
		=> rowValues ?? throw new InvalidOperationException(
			$"Column [{rule.Column.Name}] uses the value of another column, which is only available while rows are generated."
		);

	/// <summary>
	///	Generates a random whole number for an integer SQL type, keeping narrow types as narrow CLR values.
	/// </summary>
	/// <param name="sqlType">
	///	The normalised SQL type name.
	/// </param>
	/// <param name="random">
	///	The random source used to choose the value.
	/// </param>
	/// <returns>
	///	A byte, short, int or long value in the configured random range for the SQL type.
	/// </returns>
	/// <remarks>
	///	A switch statement keeps the CLR type of each column type; a switch expression would widen every value to long.
	/// </remarks>
	private static object GenerateRandomInteger(string sqlType, Random random)
	{
		switch (sqlType)
		{
			case "tinyint":
			{
				return (byte)random.Next(0, byte.MaxValue + 1);
			}

			case "smallint":
			{
				return (short)random.Next(0, short.MaxValue + 1);
			}

			case "int":
			{
				return random.Next(1, 1_000_001);
			}

			default:
			{
				return random.NextInt64(1, 1_000_000_001);
			}
		}
	}

	/// <summary>
	///	Generates a random decimal, money or floating-point value for a SQL column.
	/// </summary>
	/// <param name="column">
	///	The column whose precision and scale shape decimal values.
	/// </param>
	/// <param name="sqlType">
	///	The normalised SQL type name.
	/// </param>
	/// <param name="random">
	///	The random source used to choose the value.
	/// </param>
	/// <returns>
	///	A random numeric CLR value suitable for the column type.
	/// </returns>
	private static object GenerateRandomDecimal(ColumnModel column, string sqlType, Random random)
	{
		switch (sqlType)
		{
			case "money":
			{
				return Math.Round((decimal)(random.NextDouble() * 100_000), 2);
			}

			case "smallmoney":
			{
				return Math.Round((decimal)(random.NextDouble() * 10_000), 2);
			}

			case "float":
			{
				return Math.Round(random.NextDouble() * 100_000, MAXIMUM_RANDOM_DECIMALS);
			}

			case "real":
			{
				return (float)Math.Round(random.NextDouble() * 100_000, 2);
			}
		}

		(int integerDigits, int decimals) = GetRandomDecimalShape(column);

		long    integerPart    = integerDigits == 0 ? 0 : random.NextInt64(0, Pow10(integerDigits));
		long    fractionDigits = decimals == 0 ? 0 : random.NextInt64(0, Pow10(decimals));
		decimal fraction       = decimals == 0 ? 0 : fractionDigits / (decimal)Pow10(decimals);

		return integerPart + fraction;
	}

	/// <summary>
	///	Describes the random range used for an integer SQL type.
	/// </summary>
	/// <param name="sqlType">
	///	The normalised SQL type name.
	/// </param>
	/// <returns>
	///	A user-facing range description for random integer values.
	/// </returns>
	private static string DescribeRandomInteger(string sqlType) => sqlType switch
	{
		"tinyint"  => "Random whole number from 0 to 255",
		"smallint" => "Random whole number from 0 to 32,767",
		"int"      => "Random whole number from 1 to 1,000,000",
		_          => "Random whole number from 1 to 1,000,000,000"
	};

	/// <summary>
	///	Formats the maximum shown for random decimal and floating-point values.
	/// </summary>
	/// <param name="column">
	///	The column whose precision and scale shape decimal values.
	/// </param>
	/// <param name="sqlType">
	///	The normalised SQL type name.
	/// </param>
	/// <returns>
	///	The display text for the highest random value.
	/// </returns>
	private static string DescribeDecimalMaximum(ColumnModel column, string sqlType)
	{
		switch (sqlType)
		{
			case "money":
			case "float":
			case "real":
			{
				return "100,000";
			}

			case "smallmoney":
			{
				return "10,000";
			}
		}

		(int integerDigits, int decimals) = GetRandomDecimalShape(column);

		decimal maximum = Pow10(integerDigits) - (decimals == 0 ? 1 : 1m / Pow10(decimals));

		return maximum.ToString(decimals == 0 ? "N0" : $"N{decimals}", CultureInfo.InvariantCulture);
	}

	/// <summary>
	///	Chooses how many integer and fractional digits to generate for a decimal column.
	/// </summary>
	/// <param name="column">
	///	The column whose precision and scale are inspected.
	/// </param>
	/// <returns>
	///	The limited number of integer digits and decimal places used for random values.
	/// </returns>
	private static (int IntegerDigits, int Decimals) GetRandomDecimalShape(ColumnModel column)
	{
		int precision = column.Precision ?? 18;
		int scale     = column.Scale ?? 0;

		return (Math.Min(precision - scale, MAXIMUM_INTEGER_DIGITS), Math.Min(scale, MAXIMUM_RANDOM_DECIMALS));
	}

	/// <summary>
	///	Generates a random date or date-time value between the configured random bounds.
	/// </summary>
	/// <param name="sqlType">
	///	The normalised SQL type name.
	/// </param>
	/// <param name="random">
	///	The random source used to choose the ticks.
	/// </param>
	/// <returns>
	///	A <see cref="DateTime"/> or <see cref="DateTimeOffset"/> value suitable for the SQL type.
	/// </returns>
	/// <remarks>
	///	A switch statement keeps DateTime values as DateTime; a switch expression would convert them all to DateTimeOffset with the local offset.
	/// </remarks>
	private static object GenerateRandomDateTime(string sqlType, Random random)
	{
		long     ticks = random.NextInt64(RANDOM_DATE_MINIMUM.Ticks, RANDOM_DATE_MAXIMUM.Ticks);
		DateTime value = new(ticks - (ticks % TimeSpan.TicksPerSecond));

		switch (sqlType)
		{
			case "date":
			{
				return value.Date;
			}

			case "smalldatetime":
			{
				return new DateTime(value.Year, value.Month, value.Day, value.Hour, value.Minute, 0);
			}

			case "datetimeoffset":
			{
				return new DateTimeOffset(value, TimeSpan.Zero);
			}

			default:
			{
				return value;
			}
		}
	}

	/// <summary>
	///	Generates random uppercase letters and digits.
	/// </summary>
	/// <param name="length">
	///	The number of characters to generate.
	/// </param>
	/// <param name="random">
	///	The random source used to choose characters.
	/// </param>
	/// <returns>
	///	A random text value of exactly <paramref name="length"/> characters.
	/// </returns>
	private static string GenerateRandomText(int length, Random random)
	{
		char[] characters = new char[length];

		for (int index = 0; index < characters.Length; ++index)
		{
			characters[index] = RANDOM_TEXT_CHARACTERS[random.Next(RANDOM_TEXT_CHARACTERS.Length)];
		}

		return new string(characters);
	}

	/// <summary>
	///	Chooses the byte count used for random binary values in a column.
	/// </summary>
	/// <param name="column">
	///	The binary column whose maximum length is inspected.
	/// </param>
	/// <returns>
	///	The lesser of the column maximum length and the default random binary length, or the default when unlimited.
	/// </returns>
	private static int GetRandomBinaryLength(ColumnModel column)
		=> column.MaximumLength is null or < 1 ? RANDOM_BINARY_LENGTH : Math.Min(column.MaximumLength.Value, RANDOM_BINARY_LENGTH);

	/// <summary>
	///	Calculates a power of ten using integer arithmetic.
	/// </summary>
	/// <param name="exponent">
	///	The non-negative exponent.
	/// </param>
	/// <returns>
	///	Ten raised to <paramref name="exponent"/>.
	/// </returns>
	private static long Pow10(int exponent)
	{
		long value = 1;

		for (int index = 0; index < exponent; ++index)
		{
			value *= 10;
		}

		return value;
	}

	/// <summary>
	///	Normalises a column SQL type for switch comparisons.
	/// </summary>
	/// <param name="column">
	///	The column whose SQL type is normalised.
	/// </param>
	/// <returns>
	///	The trimmed SQL type in lower-case invariant form.
	/// </returns>
	private static string NormalizeType(ColumnModel column) => column.SqlType.Trim().ToLowerInvariant();

	/// <summary>
	///	Generates a pattern value and converts it to the target column type.
	/// </summary>
	/// <param name="rule">
	///	The pattern rule to evaluate.
	/// </param>
	/// <param name="rowIndex">
	///	The zero-based row index passed to the pattern engine.
	/// </param>
	/// <param name="rowCount">
	///	The total row count passed to the pattern engine.
	/// </param>
	/// <param name="rowValues">
	///	The current row values used to resolve column references, or <see langword="null"/> for independent patterns.
	/// </param>
	/// <returns>
	///	The generated and converted pattern value.
	/// </returns>
	/// <remarks>
	///	Patterns that use other columns are converted leniently, because the other column's value may not suit this column's
	///	type (e.g. text copied into an int column keeps only its digits). Other patterns must produce a valid value.
	/// </remarks>
	private object? GeneratePatternValue(ColumnRule rule, long rowIndex, long rowCount, IRowValueLookup? rowValues)
	{
		if (_patternGenerator.GetColumnReferences(rule.PatternExpression).Count == 0)
		{
			return _converter.ConvertGeneratedText(rule.Column, _patternGenerator.Generate(rule.PatternExpression, rowIndex, rowCount, null));
		}

		IRowValueLookup lookup = GetRowValues(rule, rowValues);
		string          text   = _patternGenerator.Generate(
			rule.PatternExpression,
			rowIndex,
			rowCount,
			columnName => _caster.ToText(lookup.GetValue(columnName)),
			lookup.GetValue
		);

		return _caster.Cast(rule.Column, text);
	}

	/// <summary>
	///	Generates one random value for a supported SQL column type.
	/// </summary>
	/// <param name="column">
	///	The column whose type controls the generated value.
	/// </param>
	/// <returns>
	///	A random CLR value suitable for the column.
	/// </returns>
	/// <exception cref="InvalidOperationException">
	///	Thrown when the column is a rowversion column, because SQL Server generates those values.
	/// </exception>
	/// <exception cref="NotSupportedException">
	///	Thrown when random values are not supported for the column type.
	/// </exception>
	private object GenerateRandomValue(ColumnModel column)
	{
		Random random  = Random.Shared;
		string sqlType = NormalizeType(column);

		switch (_converter.GetCategory(column))
		{
			case SqlTypeCategory.Integer:
			{
				return GenerateRandomInteger(sqlType, random);
			}

			case SqlTypeCategory.Decimal:
			{
				return GenerateRandomDecimal(column, sqlType, random);
			}

			case SqlTypeCategory.Boolean:
			{
				return random.Next(2) == 1;
			}

			case SqlTypeCategory.Text:
			{
				return GenerateRandomText(GetRandomTextLength(column), random);
			}

			case SqlTypeCategory.DateTime:
			{
				return GenerateRandomDateTime(sqlType, random);
			}

			case SqlTypeCategory.Time:
			{
				return TimeSpan.FromSeconds(random.Next(0, SECONDS_PER_DAY));
			}

			case SqlTypeCategory.Guid:
			{
				return Guid.NewGuid();
			}

			case SqlTypeCategory.Binary:
			{
				byte[] bytes = new byte[GetRandomBinaryLength(column)];
				random.NextBytes(bytes);
				return bytes;
			}

			case SqlTypeCategory.RowVersion:
			{
				throw new InvalidOperationException("rowversion values are always generated by SQL Server.");
			}

			default:
			{
				throw new NotSupportedException(
					$"Random values are not supported for {_converter.GetDisplayType(column)} columns. Use Fixed value, Pattern or NULL instead."
				);
			}
		}
	}

	/// <summary>
	///	Chooses the length used for random text in a column.
	/// </summary>
	/// <param name="column">
	///	The text column whose maximum length is inspected.
	/// </param>
	/// <returns>
	///	The lesser of the column maximum length and the default random text length.
	/// </returns>
	private int GetRandomTextLength(ColumnModel column)
		=> Math.Min(_converter.GetMaximumTextLength(column) ?? RANDOM_TEXT_LENGTH, RANDOM_TEXT_LENGTH);
	#endregion PRIVATE
	#endregion METHODS
}