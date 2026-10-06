using System.Data;
using System.Globalization;
using DataGenerator.Interfaces;
using DataGenerator.Models;

namespace DataGenerator.Services;

public sealed class SqlValueConverter : ISqlValueConverter
{
	private const decimal MONEY_MAXIMUM       = 922337203685477.5807m;
	private const decimal SMALL_MONEY_MAXIMUM = 214748.3647m;
	private const int     MONEY_SCALE         = 4;
	private const int     DEFAULT_PRECISION   = 18;
	private const int     DEFAULT_TIME_SCALE  = 7;
	private const int     SYSNAME_LENGTH      = 128;
	private const double  REAL_MAXIMUM        = 3.40E+38;

	private static readonly CultureInfo INVARIANT              = CultureInfo.InvariantCulture;
	private static readonly DateTime    DATETIME_MINIMUM       = new(1753, 1, 1);
	private static readonly DateTime    SMALL_DATETIME_MINIMUM = new(1900, 1, 1);
	private static readonly DateTime    SMALL_DATETIME_MAXIMUM = new(2079, 6, 6, 23, 59, 0);
	private static readonly string[]    TRUE_VALUES            = ["1", "true", "yes", "y", "on"];
	private static readonly string[]    FALSE_VALUES           = ["0", "false", "no", "n", "off"];

	public SqlTypeCategory GetCategory(ColumnModel column)
	{
		ArgumentNullException.ThrowIfNull(column);

		return NormalizeType(column) switch
		{
			"bigint" or "int" or "smallint" or "tinyint"                                   => SqlTypeCategory.Integer,
			"decimal" or "numeric" or "money" or "smallmoney" or "float" or "real"         => SqlTypeCategory.Decimal,
			"bit"                                                                          => SqlTypeCategory.Boolean,
			"char" or "varchar" or "nchar" or "nvarchar" or "text" or "ntext" or "sysname" => SqlTypeCategory.Text,
			"date" or "datetime" or "datetime2" or "smalldatetime" or "datetimeoffset"     => SqlTypeCategory.DateTime,
			"time"                                                                         => SqlTypeCategory.Time,
			"uniqueidentifier"                                                             => SqlTypeCategory.Guid,
			"binary" or "varbinary" or "image"                                             => SqlTypeCategory.Binary,
			"timestamp" or "rowversion"                                                    => SqlTypeCategory.RowVersion,
			_                                                                              => SqlTypeCategory.Unsupported
		};
	}

	public object ConvertText(ColumnModel column, string text)
	{
		ArgumentNullException.ThrowIfNull(column);
		ArgumentNullException.ThrowIfNull(text);

		string sqlType     = NormalizeType(column);
		string trimmedText = text.Trim();

		switch (GetCategory(column))
		{
			case SqlTypeCategory.Integer:
			{
				if (!long.TryParse(trimmedText, NumberStyles.Integer, INVARIANT, out long integerValue))
				{
					throw new FormatException($"'{text}' is not a whole number. Enter {DescribeAcceptedInput(column)}.");
				}

				return ConvertIntegerValue(column, integerValue);
			}

			case SqlTypeCategory.Decimal:
			{
				if (sqlType is "float" or "real")
				{
					if (!double.TryParse(trimmedText, NumberStyles.Float | NumberStyles.AllowThousands, INVARIANT, out double doubleValue)
						|| double.IsNaN(doubleValue)
						|| double.IsInfinity(doubleValue))
					{
						throw new FormatException($"'{text}' is not a number. Enter {DescribeAcceptedInput(column)}.");
					}

					return ConvertFloatingValue(column, doubleValue);
				}

				if (!decimal.TryParse(trimmedText, NumberStyles.Number | NumberStyles.AllowExponent, INVARIANT, out decimal decimalValue))
				{
					throw new FormatException($"'{text}' is not a number. Enter {DescribeAcceptedInput(column)}.");
				}

				return ConvertDecimalValue(column, decimalValue);
			}

			case SqlTypeCategory.Boolean:
			{
				if (TRUE_VALUES.Contains(trimmedText, StringComparer.OrdinalIgnoreCase))
				{
					return true;
				}

				if (FALSE_VALUES.Contains(trimmedText, StringComparer.OrdinalIgnoreCase))
				{
					return false;
				}

				throw new FormatException($"'{text}' is not a bit value. Enter {DescribeAcceptedInput(column)}.");
			}

			case SqlTypeCategory.Text:
			{
				EnsureTextLength(column, text, $"The text is {text.Length} characters long");
				return text;
			}

			case SqlTypeCategory.DateTime:
			{
				return ConvertDateTimeText(column, text, trimmedText, sqlType);
			}

			case SqlTypeCategory.Time:
			{
				if (!TimeSpan.TryParse(trimmedText, INVARIANT, out TimeSpan timeValue)
					|| timeValue < TimeSpan.Zero
					|| timeValue >= TimeSpan.FromDays(1))
				{
					throw new FormatException($"'{text}' is not a valid time of day. Enter {DescribeAcceptedInput(column)}.");
				}

				return timeValue;
			}

			case SqlTypeCategory.Guid:
			{
				if (!Guid.TryParse(trimmedText, out Guid guidValue))
				{
					throw new FormatException($"'{text}' is not a valid GUID. Enter {DescribeAcceptedInput(column)}.");
				}

				return guidValue;
			}

			case SqlTypeCategory.Binary:
			{
				return ConvertHexText(column, text, trimmedText);
			}

			case SqlTypeCategory.RowVersion:
			{
				throw new InvalidOperationException("rowversion values are always generated by SQL Server.");
			}

			default:
			{
				return text;
			}
		}
	}

	public bool TryConvertText(ColumnModel column, string text, out object? value, out string errorMessage)
	{
		try
		{
			value        = ConvertText(column, text);
			errorMessage = string.Empty;
			return true;
		}
		catch (Exception exception) when (exception is FormatException or OverflowException or InvalidOperationException or ArgumentException)
		{
			value        = null;
			errorMessage = exception.Message;
			return false;
		}
	}

	public object ConvertSequenceValue(ColumnModel column, decimal value)
	{
		ArgumentNullException.ThrowIfNull(column);

		switch (GetCategory(column))
		{
			case SqlTypeCategory.Integer:
			{
				if (value != decimal.Truncate(value))
				{
					throw new FormatException(
						$"The sequence produced {value.ToString(INVARIANT)}, but {GetDisplayType(column)} only stores whole numbers. "
						+ "Use whole numbers for the start and step."
					);
				}

				if (value < long.MinValue || value > long.MaxValue)
				{
					throw new OverflowException($"The sequence value {value.ToString(INVARIANT)} is outside the range of {GetDisplayType(column)}.");
				}

				return ConvertIntegerValue(column, (long)value);
			}

			case SqlTypeCategory.Decimal:
			{
				return NormalizeType(column) is "float" or "real"
					? ConvertFloatingValue(column, (double)value)
					: ConvertDecimalValue(column, value);
			}

			case SqlTypeCategory.Text:
			{
				string text = value.ToString(INVARIANT);
				EnsureTextLength(column, text, $"The sequence value '{text}' is {text.Length} characters long");
				return text;
			}

			default:
			{
				throw new InvalidOperationException($"Sequences are not supported for {GetDisplayType(column)} columns.");
			}
		}
	}

	public object ConvertGeneratedText(ColumnModel column, string text)
	{
		ArgumentNullException.ThrowIfNull(column);
		ArgumentNullException.ThrowIfNull(text);

		if (GetCategory(column) == SqlTypeCategory.Text)
		{
			EnsureTextLength(column, text, $"The generated value '{text}' is {text.Length} characters long");
			return text;
		}

		try
		{
			return ConvertText(column, text);
		}
		catch (Exception exception) when (exception is FormatException or OverflowException)
		{
			throw new FormatException(
				$"The generated value '{text}' cannot be stored in column [{column.Name}] ({GetDisplayType(column)}). {exception.Message}",
				exception
			);
		}
	}

	public string ToSqlLiteral(ColumnModel column, object? value)
	{
		ArgumentNullException.ThrowIfNull(column);

		return value switch
		{
			null or DBNull                => "NULL",
			SqlFragment fragment          => $"CONVERT({GetTypeDeclaration(column)}, {fragment.Sql})",
			bool booleanValue             => booleanValue ? "1" : "0",
			string text                   => QuoteText(column, text),
			DateTime dateTimeValue        => $"'{FormatDateTimeLiteral(column, dateTimeValue)}'",
			DateTimeOffset offsetValue    => $"'{offsetValue.ToString("yyyy-MM-ddTHH:mm:ss.fffffffzzz", INVARIANT)}'",
			TimeSpan timeValue            => $"'{timeValue.ToString(@"hh\:mm\:ss\.fffffff", INVARIANT)}'",
			Guid guidValue                => $"'{guidValue:D}'",
			byte[] bytes                  => $"0x{Convert.ToHexString(bytes)}",
			float singleValue             => singleValue.ToString("R", INVARIANT),
			double doubleValue            => doubleValue.ToString("R", INVARIANT),
			decimal decimalValue          => decimalValue.ToString(INVARIANT),
			IFormattable formattableValue => formattableValue.ToString(null, INVARIANT),
			_                             => QuoteText(column, value.ToString() ?? string.Empty)
		};
	}

	public string FormatForDisplay(ColumnModel column, object? value)
	{
		ArgumentNullException.ThrowIfNull(column);

		return value switch
		{
			null or DBNull                => "NULL",
			SqlFragment                   => "(resolved by SQL Server)",
			bool booleanValue             => booleanValue ? "1" : "0",
			string text                   => text,
			DateTime dateTimeValue        => NormalizeType(column) == "date"
				? dateTimeValue.ToString("yyyy-MM-dd", INVARIANT)
				: dateTimeValue.ToString("yyyy-MM-dd HH:mm:ss", INVARIANT),
			DateTimeOffset offsetValue    => offsetValue.ToString("yyyy-MM-dd HH:mm:ss zzz", INVARIANT),
			TimeSpan timeValue            => timeValue.ToString(@"hh\:mm\:ss", INVARIANT),
			Guid guidValue                => guidValue.ToString("D").ToUpperInvariant(),
			byte[] bytes                  => $"0x{Convert.ToHexString(bytes)}",
			IFormattable formattableValue => formattableValue.ToString(null, INVARIANT),
			_                             => value.ToString() ?? string.Empty
		};
	}

	public string GetTypeDeclaration(ColumnModel column)
	{
		ArgumentNullException.ThrowIfNull(column);

		return NormalizeType(column) switch
		{
			"text"                      => "varchar(max)",
			"ntext"                     => "nvarchar(max)",
			"image"                     => "varbinary(max)",
			"timestamp" or "rowversion" => "binary(8)",
			"sysname"                   => $"nvarchar({SYSNAME_LENGTH})",
			_                           => GetDisplayType(column)
		};
	}

	public string GetDisplayType(ColumnModel column)
	{
		ArgumentNullException.ThrowIfNull(column);

		string sqlType = NormalizeType(column);

		return sqlType switch
		{
			"char" or "varchar" or "nchar" or "nvarchar" or "binary" or "varbinary" => $"{sqlType}({FormatLength(column.MaximumLength)})",
			"decimal" or "numeric"                                                  => $"{sqlType}({column.Precision ?? DEFAULT_PRECISION}, {column.Scale ?? 0})",
			"datetime2" or "time" or "datetimeoffset"                               => $"{sqlType}({column.Scale ?? DEFAULT_TIME_SCALE})",
			_                                                                       => sqlType
		};
	}

	public int? GetMaximumTextLength(ColumnModel column)
	{
		ArgumentNullException.ThrowIfNull(column);

		return NormalizeType(column) switch
		{
			"char" or "varchar" or "nchar" or "nvarchar" => column.MaximumLength is null or < 1 ? null : column.MaximumLength,
			"sysname"                                    => SYSNAME_LENGTH,
			_                                            => null
		};
	}

	public SqlDbType GetSqlDbType(ColumnModel column)
	{
		ArgumentNullException.ThrowIfNull(column);

		return NormalizeType(column) switch
		{
			"bigint"                    => SqlDbType.BigInt,
			"int"                       => SqlDbType.Int,
			"smallint"                  => SqlDbType.SmallInt,
			"tinyint"                   => SqlDbType.TinyInt,
			"bit"                       => SqlDbType.Bit,
			"decimal" or "numeric"      => SqlDbType.Decimal,
			"money"                     => SqlDbType.Money,
			"smallmoney"                => SqlDbType.SmallMoney,
			"float"                     => SqlDbType.Float,
			"real"                      => SqlDbType.Real,
			"date"                      => SqlDbType.Date,
			"datetime"                  => SqlDbType.DateTime,
			"datetime2"                 => SqlDbType.DateTime2,
			"smalldatetime"             => SqlDbType.SmallDateTime,
			"datetimeoffset"            => SqlDbType.DateTimeOffset,
			"time"                      => SqlDbType.Time,
			"char"                      => SqlDbType.Char,
			"varchar"                   => SqlDbType.VarChar,
			"nchar"                     => SqlDbType.NChar,
			"text"                      => SqlDbType.Text,
			"ntext"                     => SqlDbType.NText,
			"uniqueidentifier"          => SqlDbType.UniqueIdentifier,
			"binary"                    => SqlDbType.Binary,
			"varbinary"                 => SqlDbType.VarBinary,
			"image"                     => SqlDbType.Image,
			"xml"                       => SqlDbType.Xml,
			"timestamp" or "rowversion" => SqlDbType.Timestamp,
			"sql_variant"               => SqlDbType.Variant,
			_                           => SqlDbType.NVarChar
		};
	}

	public string DescribeAcceptedInput(ColumnModel column)
	{
		ArgumentNullException.ThrowIfNull(column);

		string sqlType = NormalizeType(column);

		switch (GetCategory(column))
		{
			case SqlTypeCategory.Integer:
			{
				(long minimum, long maximum) = GetIntegerRange(sqlType);
				return $"a whole number from {minimum.ToString("N0", INVARIANT)} to {maximum.ToString("N0", INVARIANT)}";
			}

			case SqlTypeCategory.Decimal:
			{
				return sqlType switch
				{
					"money"      => $"a number from -{MONEY_MAXIMUM.ToString(INVARIANT)} to {MONEY_MAXIMUM.ToString(INVARIANT)} with up to {MONEY_SCALE} decimal places",
					"smallmoney" => $"a number from -{SMALL_MONEY_MAXIMUM.ToString(INVARIANT)} to {SMALL_MONEY_MAXIMUM.ToString(INVARIANT)} with up to {MONEY_SCALE} decimal places",
					"float"      => "a number, e.g. 1234.5678",
					"real"       => "a number, e.g. 1234.56",
					_            => DescribeDecimal(column)
				};
			}

			case SqlTypeCategory.Boolean:
			{
				return "1 or 0 (true/false and yes/no are also accepted)";
			}

			case SqlTypeCategory.Text:
			{
				int? maximumLength = GetMaximumTextLength(column);
				return maximumLength.HasValue
					? $"text of at most {maximumLength.Value.ToString("N0", INVARIANT)} characters"
					: "any text";
			}

			case SqlTypeCategory.DateTime:
			{
				return sqlType switch
				{
					"date"           => "a date such as 2024-12-31",
					"datetime"       => "a date and time from 1753-01-01 to 9999-12-31, such as 2024-12-31 13:45:00",
					"smalldatetime"  => "a date and time from 1900-01-01 to 2079-06-06, such as 2024-12-31 13:45",
					"datetimeoffset" => "a date, time and offset such as 2024-12-31 13:45:00 +10:00",
					_                => "a date and time such as 2024-12-31 13:45:00"
				};
			}

			case SqlTypeCategory.Time:
			{
				return "a time of day such as 13:45:00";
			}

			case SqlTypeCategory.Guid:
			{
				return "a GUID such as 3F2504E0-4F89-11D3-9A0C-0305E82C3301";
			}

			case SqlTypeCategory.Binary:
			{
				int? maximumBytes = column.MaximumLength is null or < 1 ? null : column.MaximumLength;
				return maximumBytes.HasValue
					? $"hexadecimal bytes such as 0x1A2B (at most {maximumBytes.Value.ToString(INVARIANT)} bytes)"
					: "hexadecimal bytes such as 0x1A2B";
			}

			case SqlTypeCategory.RowVersion:
			{
				return "nothing - SQL Server generates rowversion values";
			}

			default:
			{
				return $"text that SQL Server can convert to {GetDisplayType(column)}";
			}
		}
	}

	private static string NormalizeType(ColumnModel column) => column.SqlType.Trim().ToLowerInvariant();

	private static string FormatLength(int? maximumLength)
		=> maximumLength is null or < 1 ? "max" : maximumLength.Value.ToString(INVARIANT);

	private static (long Minimum, long Maximum) GetIntegerRange(string sqlType) => sqlType switch
	{
		"tinyint"  => (byte.MinValue, byte.MaxValue),
		"smallint" => (short.MinValue, short.MaxValue),
		"int"      => (int.MinValue, int.MaxValue),
		_          => (long.MinValue, long.MaxValue)
	};

	private object ConvertIntegerValue(ColumnModel column, long value)
	{
		string sqlType               = NormalizeType(column);
		(long minimum, long maximum) = GetIntegerRange(sqlType);

		if (value < minimum || value > maximum)
		{
			throw new OverflowException(
				$"{value.ToString("N0", INVARIANT)} is outside the range of {sqlType}. Enter {DescribeAcceptedInput(column)}."
			);
		}

		// A switch statement keeps the CLR type of each column type; a switch expression would widen every value back to long.
		switch (sqlType)
		{
			case "tinyint":
			{
				return (byte)value;
			}

			case "smallint":
			{
				return (short)value;
			}

			case "int":
			{
				return (int)value;
			}

			default:
			{
				return value;
			}
		}
	}

	private object ConvertFloatingValue(ColumnModel column, double value)
	{
		if (NormalizeType(column) != "real")
		{
			return value;
		}

		if (Math.Abs(value) > REAL_MAXIMUM)
		{
			throw new OverflowException($"{value.ToString(INVARIANT)} is outside the range of real.");
		}

		return (float)value;
	}

	private object ConvertDecimalValue(ColumnModel column, decimal value)
	{
		string sqlType       = NormalizeType(column);
		int    decimalPlaces = CountDecimalPlaces(value);

		if (sqlType is "money" or "smallmoney")
		{
			decimal maximum = sqlType == "money" ? MONEY_MAXIMUM : SMALL_MONEY_MAXIMUM;

			if (Math.Abs(value) > maximum)
			{
				throw new OverflowException($"{value.ToString(INVARIANT)} is outside the range of {sqlType}. Enter {DescribeAcceptedInput(column)}.");
			}

			if (decimalPlaces > MONEY_SCALE)
			{
				throw new FormatException($"{value.ToString(INVARIANT)} has more than {MONEY_SCALE} decimal places.");
			}

			return value;
		}

		int precision = column.Precision ?? DEFAULT_PRECISION;
		int scale     = column.Scale ?? 0;

		if (decimalPlaces > scale)
		{
			throw new FormatException(
				$"{value.ToString(INVARIANT)} has {decimalPlaces} decimal places, but {GetDisplayType(column)} allows {scale}. "
				+ $"Enter {DescribeAcceptedInput(column)}."
			);
		}

		int integerDigits = CountIntegerDigits(value);

		if (integerDigits > precision - scale)
		{
			throw new OverflowException(
				$"{value.ToString(INVARIANT)} has too many digits for {GetDisplayType(column)}. Enter {DescribeAcceptedInput(column)}."
			);
		}

		return value;
	}

	private string DescribeDecimal(ColumnModel column)
	{
		int precision = column.Precision ?? DEFAULT_PRECISION;
		int scale     = column.Scale ?? 0;

		return scale == 0
			? $"a whole number with at most {precision} digits"
			: $"a number with at most {precision - scale} digits before and {scale} after the decimal point";
	}

	private object ConvertDateTimeText(ColumnModel column, string text, string trimmedText, string sqlType)
	{
		if (sqlType == "datetimeoffset")
		{
			if (!DateTimeOffset.TryParse(trimmedText, INVARIANT, DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeLocal, out DateTimeOffset offsetValue))
			{
				throw new FormatException($"'{text}' is not a valid date and time. Enter {DescribeAcceptedInput(column)}.");
			}

			return offsetValue;
		}

		if (!DateTime.TryParse(trimmedText, INVARIANT, DateTimeStyles.AllowWhiteSpaces, out DateTime dateTimeValue))
		{
			throw new FormatException($"'{text}' is not a valid date. Enter {DescribeAcceptedInput(column)}.");
		}

		DateTime minimum = sqlType switch
		{
			"datetime"      => DATETIME_MINIMUM,
			"smalldatetime" => SMALL_DATETIME_MINIMUM,
			_               => DateTime.MinValue
		};
		DateTime maximum = sqlType == "smalldatetime" ? SMALL_DATETIME_MAXIMUM : DateTime.MaxValue;

		if (dateTimeValue < minimum || dateTimeValue > maximum)
		{
			throw new OverflowException($"'{text}' is outside the range of {sqlType}. Enter {DescribeAcceptedInput(column)}.");
		}

		return sqlType == "date" ? dateTimeValue.Date : dateTimeValue;
	}

	private byte[] ConvertHexText(ColumnModel column, string text, string trimmedText)
	{
		string hexText =
			trimmedText.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
				? trimmedText[2..]
				: trimmedText;

		if (hexText.Length % 2 != 0 || !hexText.All(Uri.IsHexDigit))
		{
			throw new FormatException($"'{text}' is not valid hexadecimal. Enter {DescribeAcceptedInput(column)}.");
		}

		byte[] bytes = Convert.FromHexString(hexText);

		if (column.MaximumLength is > 0 && bytes.Length > column.MaximumLength.Value)
		{
			throw new FormatException(
				$"The value is {bytes.Length} bytes long, but {GetDisplayType(column)} allows at most {column.MaximumLength.Value}."
			);
		}

		return bytes;
	}

	private void EnsureTextLength(ColumnModel column, string text, string problem)
	{
		int? maximumLength = GetMaximumTextLength(column);

		if (maximumLength.HasValue && text.Length > maximumLength.Value)
		{
			throw new FormatException(
				$"{problem}, but column [{column.Name}] ({GetDisplayType(column)}) allows at most {maximumLength.Value}."
			);
		}
	}

	private static string QuoteText(ColumnModel column, string text)
	{
		string escapedText = text.Replace("'", "''");

		return NormalizeType(column) is "char" or "varchar" or "text"
			? $"'{escapedText}'"
			: $"N'{escapedText}'";
	}

	private static string FormatDateTimeLiteral(ColumnModel column, DateTime value) => NormalizeType(column) switch
	{
		"date"          => value.ToString("yyyy-MM-dd", INVARIANT),
		"datetime"      => value.ToString("yyyy-MM-ddTHH:mm:ss.fff", INVARIANT),
		"smalldatetime" => value.ToString("yyyy-MM-ddTHH:mm:ss", INVARIANT),
		_               => value.ToString("yyyy-MM-ddTHH:mm:ss.fffffff", INVARIANT)
	};

	private static int CountDecimalPlaces(decimal value)
	{
		string text         = value.ToString(INVARIANT);
		int    decimalIndex = text.IndexOf('.');

		return decimalIndex < 0 ? 0 : text.TrimEnd('0').Length - decimalIndex - 1;
	}

	private static int CountIntegerDigits(decimal value)
	{
		decimal integerPart = decimal.Truncate(Math.Abs(value));

		return integerPart == 0 ? 0 : integerPart.ToString(INVARIANT).Length;
	}
}