using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using DataGenerator.Interfaces;
using DataGenerator.Models;

namespace DataGenerator.Services;

public sealed class ColumnValueCaster : IColumnValueCaster
{
	#region FIELDS
	#region PRIVATE
	private const decimal MONEY_MAXIMUM       = 922337203685477.5807m;
	private const decimal SMALL_MONEY_MAXIMUM = 214748.3647m;
	private const int     MONEY_SCALE         = 4;
	private const int     DEFAULT_PRECISION   = 18;
	private const int     MAXIMUM_PRECISION   = 28;
	private const double  REAL_MAXIMUM        = 3.40E+38;
	private const string  DATE_FORMAT         = "yyyy-MM-dd";
	private const string  DATE_TIME_FORMAT    = "yyyy-MM-dd HH:mm:ss";

	private static readonly CultureInfo INVARIANT;
	private static readonly DateTime    DEFAULT_DATE;
	private static readonly DateTime    DATETIME_MINIMUM;
	private static readonly DateTime    SMALL_DATETIME_MAXIMUM;
	private static readonly string[]    TRUE_WORDS;

	private readonly ISqlValueConverter _converter;
	#endregion PRIVATE
	#endregion FIELDS

	#region CONSTRUCTORS
	#region STATIC
	/// <summary>
	///	Sets the default values of the static fields and properties of <see cref="ColumnValueCaster"/>.
	/// </summary>
	static ColumnValueCaster()
	{
		INVARIANT              = CultureInfo.InvariantCulture;
		DEFAULT_DATE           = new(1900, 1, 1);
		DATETIME_MINIMUM       = new(1753, 1, 1);
		SMALL_DATETIME_MAXIMUM = new(2079, 6, 6, 23, 59, 0);
		TRUE_WORDS             = ["true", "yes", "y", "on"];
	}
	#endregion STATIC

	#region PUBLIC
	/// <summary>
	///	Creates a caster that uses the shared SQL value converter for column metadata and SQL type conversions.
	/// </summary>
	/// <param name="converter">
	///	The converter used to classify SQL types and produce CLR values that fit each column.
	/// </param>
	/// <exception cref="ArgumentNullException">
	///	Thrown when <paramref name="converter"/> is <see langword="null"/>.
	/// </exception>
	public ColumnValueCaster(ISqlValueConverter converter)
	{
		_converter = converter ?? throw new ArgumentNullException(nameof(converter));
	}
	#endregion PUBLIC
	#endregion CONSTRUCTORS

	#region METHODS
	#region PUBLIC
	/// <summary>
	///	Converts a copied value so it fits the target column, applying SQL Server-like leniency for copied pattern values.
	/// </summary>
	/// <param name="target">
	///	The column that receives the value.
	/// </param>
	/// <param name="value">
	///	The source value to copy. <see cref="DBNull"/> is treated as <see langword="null"/>, and SQL fragments are
	///	returned unchanged.
	/// </param>
	/// <returns>
	///	The converted value, <see langword="null"/> when the target is nullable and the source is null, or the original
	///	SQL fragment.
	/// </returns>
	/// <exception cref="ArgumentNullException">
	///	Thrown when <paramref name="target"/> is <see langword="null"/>.
	/// </exception>
	/// <exception cref="InvalidOperationException">
	///	Thrown when values cannot be copied into the target SQL type.
	/// </exception>
	public object? Cast(ColumnModel target, object? value)
	{
		ArgumentNullException.ThrowIfNull(target);

		if (value is DBNull)
		{
			value = null;
		}

		// Values resolved by SQL Server while a script runs are converted there with CONVERT(...).
		if (value is SqlFragment)
		{
			return value;
		}

		if (value is null && target.IsNullable)
		{
			return null;
		}

		string sqlType = target.SqlType.Trim().ToLowerInvariant();

		return _converter.GetCategory(target) switch
		{
			SqlTypeCategory.Integer  => CastToInteger(target, sqlType, value),
			SqlTypeCategory.Decimal  => CastToDecimal(target, sqlType, value),
			SqlTypeCategory.Boolean  => CastToBoolean(value),
			SqlTypeCategory.Text     => CastToText(target, value),
			SqlTypeCategory.DateTime => CastToDateTime(sqlType, value),
			SqlTypeCategory.Time     => CastToTime(target, value),
			SqlTypeCategory.Guid     => CastToGuid(target, value),
			SqlTypeCategory.Binary   => CastToBinary(target, value),
			_                        => throw new InvalidOperationException(
				$"Values cannot be copied into column [{target.Name}] ({_converter.GetDisplayType(target)})."
			)
		};
	}

	/// <summary>
	///	Formats a generated or copied value as invariant text for pattern substitution.
	/// </summary>
	/// <param name="value">
	///	The value to format. <see langword="null"/> and <see cref="DBNull"/> become an empty string.
	/// </param>
	/// <returns>
	///	The text representation of the value, or an empty string for null values.
	/// </returns>
	/// <exception cref="InvalidOperationException">
	///	Thrown when <paramref name="value"/> is a SQL fragment whose runtime value is not available.
	/// </exception>
	public string ToText(object? value) => value switch
	{
		null or DBNull                => string.Empty,
		SqlFragment                   => throw new InvalidOperationException(
			"The referenced column takes its value from a key that SQL Server only knows while the script runs, so the value "
				+ "cannot be used in a pattern. Use the 'Copy of column' mode to copy it exactly, or insert directly into the database."
		),
		string text                   => text,
		bool booleanValue             => booleanValue ? "1" : "0",
		DateTime dateTimeValue        =>
			dateTimeValue.TimeOfDay == TimeSpan.Zero
				? dateTimeValue.ToString(DATE_FORMAT, INVARIANT)
				: dateTimeValue.ToString(DATE_TIME_FORMAT, INVARIANT),
		DateTimeOffset offsetValue    => offsetValue.ToString("yyyy-MM-dd HH:mm:ss zzz", INVARIANT),
		TimeSpan timeValue            => timeValue.ToString(@"hh\:mm\:ss", INVARIANT),
		Guid guidValue                => guidValue.ToString("D").ToUpperInvariant(),
		byte[] bytes                  => $"0x{Convert.ToHexString(bytes)}",
		IFormattable formattableValue => formattableValue.ToString(null, INVARIANT),
		_                             => value.ToString() ?? string.Empty
	};
	#endregion PUBLIC

	#region PRIVATE
	/// <summary>
	///	Attempts to read a value as a finite decimal number.
	/// </summary>
	/// <param name="value">
	///	The value to inspect.
	/// </param>
	/// <param name="number">
	///	The numeric value when conversion succeeds, or zero when conversion fails.
	/// </param>
	/// <returns>
	///	<see langword="true"/> when a number was read; otherwise <see langword="false"/>.
	/// </returns>
	private static bool TryGetNumber(object? value, out decimal number)
	{
		switch (value)
		{
			case byte or sbyte or short or ushort or int or uint or long or ulong or decimal:
			{
				number = Convert.ToDecimal(value, INVARIANT);
				return true;
			}

			case double doubleValue when double.IsFinite(doubleValue):
			{
				number = (decimal)Math.Clamp(doubleValue, (double)decimal.MinValue, (double)decimal.MaxValue);
				return true;
			}

			case float singleValue when float.IsFinite(singleValue):
			{
				number = (decimal)Math.Clamp(singleValue, (double)decimal.MinValue, (double)decimal.MaxValue);
				return true;
			}

			case bool booleanValue:
			{
				number = booleanValue ? 1 : 0;
				return true;
			}

			case string text when decimal.TryParse(text.Trim(), NumberStyles.Number | NumberStyles.AllowExponent, INVARIANT, out decimal parsed):
			{
				number = parsed;
				return true;
			}

			default:
			{
				number = 0;
				return false;
			}
		}
	}

	/// <summary>
	///	Keeps only the digits of <paramref name="text"/> and the first decimal point when allowed.
	///	Text without digits gives 0; numbers too large for a decimal give the largest decimal.
	/// </summary>
	/// <param name="text">
	///	The text whose digits are read. A leading minus sign makes the result negative.
	/// </param>
	/// <param name="allowDecimalPoint">
	///	Whether the first decimal point after a digit is kept.
	/// </param>
	/// <returns>
	///	The decimal represented by the digits, zero when there are no digits, or decimal maximum when too large.
	/// </returns>
	private static decimal ParseDigits(string text, bool allowDecimalPoint)
	{
		StringBuilder builder         = new();
		bool          hasDecimalPoint = false;

		foreach (char character in text)
		{
			if (char.IsAsciiDigit(character))
			{
				_ = builder.Append(character);
			}
			else if (character == '.' && allowDecimalPoint && !hasDecimalPoint && builder.Length > 0)
			{
				hasDecimalPoint = true;
				_               = builder.Append(character);
			}
		}

		string digits = builder.ToString().TrimEnd('.');

		if (digits.Length == 0)
		{
			return 0;
		}

		decimal number =
			decimal.TryParse(digits, NumberStyles.AllowDecimalPoint, INVARIANT, out decimal parsed)
				? parsed
				: decimal.MaxValue;

		return text.TrimStart().StartsWith('-') ? -number : number;
	}

	/// <summary>
	///	Adds a day count to the default SQL Server date while staying inside the CLR date range.
	/// </summary>
	/// <param name="days">
	///	The number of days after 1900-01-01.
	/// </param>
	/// <returns>
	///	The resulting date and time, clamped so it can be represented by <see cref="DateTime"/>.
	/// </returns>
	private static DateTime AddDays(decimal days)
	{
		double minimumDays = (DateTime.MinValue - DEFAULT_DATE).TotalDays;
		double maximumDays = (DateTime.MaxValue - DEFAULT_DATE).TotalDays - 1;

		return DEFAULT_DATE.AddDays(Math.Clamp((double)days, minimumDays, maximumDays));
	}

	/// <summary>
	///	Calculates a power of ten using decimal arithmetic.
	/// </summary>
	/// <param name="exponent">
	///	The non-negative exponent.
	/// </param>
	/// <returns>
	///	Ten raised to <paramref name="exponent"/>.
	/// </returns>
	private static decimal Pow10(int exponent)
	{
		decimal value = 1;

		for (int index = 0; index < exponent; ++index)
		{
			value *= 10;
		}

		return value;
	}

	/// <summary>
	///	Converts a value to an integer SQL type, truncating decimals and clamping it to the type range.
	/// </summary>
	/// <param name="target">
	///	The integer column that receives the value.
	/// </param>
	/// <param name="sqlType">
	///	The normalised SQL type name used to choose the integer range.
	/// </param>
	/// <param name="value">
	///	The source value to convert.
	/// </param>
	/// <returns>
	///	A CLR value suitable for the target integer column.
	/// </returns>
	private object CastToInteger(ColumnModel target, string sqlType, object? value)
	{
		decimal number =
			TryGetNumber(value, out decimal numericValue)
				? decimal.Truncate(numericValue)
				: ParseDigits(ToText(value), allowDecimalPoint: false);

		(long minimum, long maximum) = sqlType switch
		{
			"tinyint"  => (byte.MinValue, byte.MaxValue),
			"smallint" => (short.MinValue, short.MaxValue),
			"int"      => (int.MinValue, int.MaxValue),
			_          => (long.MinValue, long.MaxValue)
		};

		return _converter.ConvertSequenceValue(target, Math.Clamp(number, minimum, maximum));
	}

	/// <summary>
	///	Converts a value to a decimal, money or floating-point SQL type, rounding and clamping to the column shape.
	/// </summary>
	/// <param name="target">
	///	The numeric column that receives the value.
	/// </param>
	/// <param name="sqlType">
	///	The normalised SQL type name used to choose precision, scale and range rules.
	/// </param>
	/// <param name="value">
	///	The source value to convert.
	/// </param>
	/// <returns>
	///	A CLR numeric value suitable for the target column.
	/// </returns>
	private object CastToDecimal(ColumnModel target, string sqlType, object? value)
	{
		decimal number =
			TryGetNumber(value, out decimal numericValue)
				? numericValue
				: ParseDigits(ToText(value), allowDecimalPoint: true);

		if (sqlType is "float" or "real")
		{
			double floatingValue = (double)number;

			return
				sqlType == "real"
					? (float)Math.Clamp(floatingValue, -REAL_MAXIMUM, REAL_MAXIMUM)
					: floatingValue;
		}

		int     scale;
		decimal maximum;

		if (sqlType is "money" or "smallmoney")
		{
			scale   = MONEY_SCALE;
			maximum = sqlType == "money" ? MONEY_MAXIMUM : SMALL_MONEY_MAXIMUM;
		}
		else
		{
			int precision = Math.Min((int)(target.Precision ?? DEFAULT_PRECISION), MAXIMUM_PRECISION);

			scale   = Math.Clamp((int)(target.Scale ?? 0), 0, precision);
			maximum = Pow10(precision - scale) - (scale == 0 ? 1 : 1m / Pow10(scale));
		}

		decimal rounded = Math.Round(number, scale, MidpointRounding.AwayFromZero);

		return _converter.ConvertSequenceValue(target, Math.Clamp(rounded, -maximum, maximum));
	}

	/// <summary>
	///	Converts a value to a Boolean, accepting non-zero numbers and common true words.
	/// </summary>
	/// <param name="value">
	///	The source value to convert.
	/// </param>
	/// <returns>
	///	<see langword="true"/> when the value is true, non-zero or a true word; otherwise <see langword="false"/>.
	/// </returns>
	private object CastToBoolean(object? value)
	{
		if (value is bool booleanValue)
		{
			return booleanValue;
		}

		if (TryGetNumber(value, out decimal numericValue))
		{
			return numericValue != 0;
		}

		string text = ToText(value).Trim();

		return TRUE_WORDS.Contains(text, StringComparer.OrdinalIgnoreCase) || ParseDigits(text, allowDecimalPoint: true) != 0;
	}

	/// <summary>
	///	Converts a value to text and truncates it to the target column length when required.
	/// </summary>
	/// <param name="target">
	///	The text column that receives the value.
	/// </param>
	/// <param name="value">
	///	The source value to convert.
	/// </param>
	/// <returns>
	///	The formatted text, shortened when it is longer than the target column allows.
	/// </returns>
	private object CastToText(ColumnModel target, object? value)
	{
		string text          = ToText(value);
		int?   maximumLength = _converter.GetMaximumTextLength(target);

		return
			maximumLength.HasValue && text.Length > maximumLength.Value
				? text[..maximumLength.Value]
				: text;
	}

	/// <summary>
	///	Converts a value to a SQL date or time-related CLR value, clamping to SQL Server date ranges where needed.
	/// </summary>
	/// <param name="sqlType">
	///	The normalised SQL type name used to choose date, smalldatetime, datetime or datetimeoffset behaviour.
	/// </param>
	/// <param name="value">
	///	The source value to convert.
	/// </param>
	/// <returns>
	///	A <see cref="DateTime"/> or <see cref="DateTimeOffset"/> value suitable for the requested SQL type.
	/// </returns>
	/// <remarks>
	///	A number is read as a count of days after 1900-01-01, like CONVERT(datetime, number) in SQL Server.
	/// </remarks>
	private object CastToDateTime(string sqlType, object? value)
	{
		if (value is DateTimeOffset sourceOffset && sqlType == "datetimeoffset")
		{
			return sourceOffset;
		}

		DateTime dateTimeValue = value switch
		{
			DateTimeOffset offsetValue => offsetValue.DateTime,
			DateTime sourceValue       => sourceValue,
			_                          => ParseDateTime(value)
		};

		DateTime minimum = sqlType switch
		{
			"datetime"      => DATETIME_MINIMUM,
			"smalldatetime" => DEFAULT_DATE,
			_               => DateTime.MinValue
		};
		DateTime maximum = sqlType == "smalldatetime" ? SMALL_DATETIME_MAXIMUM : DateTime.MaxValue;

		if (dateTimeValue < minimum)
		{
			dateTimeValue = minimum;
		}
		else if (dateTimeValue > maximum)
		{
			dateTimeValue = maximum;
		}

		// A switch statement keeps DateTime values as DateTime; a switch expression would convert them all to DateTimeOffset.
		switch (sqlType)
		{
			case "date":
			{
				return dateTimeValue.Date;
			}

			case "smalldatetime":
			{
				return new DateTime(dateTimeValue.Year, dateTimeValue.Month, dateTimeValue.Day, dateTimeValue.Hour, dateTimeValue.Minute, 0);
			}

			case "datetimeoffset":
			{
				return new DateTimeOffset(DateTime.SpecifyKind(dateTimeValue, DateTimeKind.Unspecified), TimeSpan.Zero);
			}

			default:
			{
				return dateTimeValue;
			}
		}
	}

	/// <summary>
	///	Reads a value as a date and time, falling back to digit extraction when normal parsing fails.
	/// </summary>
	/// <param name="value">
	///	The source value to parse.
	/// </param>
	/// <returns>
	///	The parsed date and time, or a date offset from 1900-01-01 when the value is numeric.
	/// </returns>
	private DateTime ParseDateTime(object? value)
	{
		if (TryGetNumber(value, out decimal days))
		{
			return AddDays(days);
		}

		string text = ToText(value);

		return
			DateTime.TryParse(text.Trim(), INVARIANT, DateTimeStyles.AllowWhiteSpaces, out DateTime parsed)
				? parsed
				: AddDays(ParseDigits(text, allowDecimalPoint: false));
	}

	/// <summary>
	///	Converts a value to a time of day, using converter parsing and midnight as the fallback.
	/// </summary>
	/// <param name="target">
	///	The time column that receives the value.
	/// </param>
	/// <param name="value">
	///	The source value to convert.
	/// </param>
	/// <returns>
	///	A <see cref="TimeSpan"/> value within a day, or <see cref="TimeSpan.Zero"/> when no time can be read.
	/// </returns>
	private object CastToTime(ColumnModel target, object? value)
	{
		switch (value)
		{
			case TimeSpan timeValue when timeValue >= TimeSpan.Zero && timeValue < TimeSpan.FromDays(1):
			{
				return timeValue;
			}

			case DateTime dateTimeValue:
			{
				return dateTimeValue.TimeOfDay;
			}

			case DateTimeOffset offsetValue:
			{
				return offsetValue.TimeOfDay;
			}
		}

		return
			_converter.TryConvertText(target, ToText(value), out object? converted, out _) && converted is not null
				? converted
				: TimeSpan.Zero;
	}

	/// <summary>
	///	Converts a value to a GUID, deriving a stable GUID from text when it is not already a valid GUID.
	/// </summary>
	/// <param name="target">
	///	The GUID column that receives the value.
	/// </param>
	/// <param name="value">
	///	The source value to convert.
	/// </param>
	/// <returns>
	///	The existing GUID, a parsed GUID, <see cref="Guid.Empty"/> for empty text, or a stable hash-based GUID.
	/// </returns>
	private object CastToGuid(ColumnModel target, object? value)
	{
		if (value is Guid guidValue)
		{
			return guidValue;
		}

		string text = ToText(value);

		// The same text always gives the same GUID, so copied values stay consistent between rows.
		return
			_converter.TryConvertText(target, text, out object? converted, out _) && converted is not null
				? converted
				: text.Length == 0
					? Guid.Empty
					: new Guid(MD5.HashData(Encoding.UTF8.GetBytes(text)));
	}

	/// <summary>
	///	Converts a value to bytes and truncates it to the target binary length when required.
	/// </summary>
	/// <param name="target">
	///	The binary column that receives the value.
	/// </param>
	/// <param name="value">
	///	The source value to convert.
	/// </param>
	/// <returns>
	///	The byte array from the source value, GUID, hex text or UTF-8 text, shortened when the target column requires it.
	/// </returns>
	private object CastToBinary(ColumnModel target, object? value)
	{
		byte[] bytes;

		if (value is byte[] sourceBytes)
		{
			bytes = sourceBytes;
		}
		else if (value is Guid guidValue)
		{
			bytes = guidValue.ToByteArray();
		}
		else
		{
			string text = ToText(value);

			bytes =
				_converter.TryConvertText(target, text, out object? converted, out _) && converted is byte[] hexBytes
					? hexBytes
					: Encoding.UTF8.GetBytes(text);
		}

		return
			target.MaximumLength is > 0 && bytes.Length > target.MaximumLength.Value
				? bytes[..target.MaximumLength.Value]
				: bytes;
	}
	#endregion PRIVATE
	#endregion METHODS
}