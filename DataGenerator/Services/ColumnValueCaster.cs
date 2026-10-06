using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using DataGenerator.Interfaces;
using DataGenerator.Models;

namespace DataGenerator.Services;

public sealed class ColumnValueCaster : IColumnValueCaster
{
	private const decimal MONEY_MAXIMUM       = 922337203685477.5807m;
	private const decimal SMALL_MONEY_MAXIMUM = 214748.3647m;
	private const int     MONEY_SCALE         = 4;
	private const int     DEFAULT_PRECISION   = 18;
	private const int     MAXIMUM_PRECISION   = 28;
	private const double  REAL_MAXIMUM        = 3.40E+38;
	private const string  DATE_FORMAT         = "yyyy-MM-dd";
	private const string  DATE_TIME_FORMAT    = "yyyy-MM-dd HH:mm:ss";

	private static readonly CultureInfo INVARIANT              = CultureInfo.InvariantCulture;
	private static readonly DateTime    DEFAULT_DATE           = new(1900, 1, 1);
	private static readonly DateTime    DATETIME_MINIMUM       = new(1753, 1, 1);
	private static readonly DateTime    SMALL_DATETIME_MAXIMUM = new(2079, 6, 6, 23, 59, 0);
	private static readonly string[]    TRUE_WORDS             = ["true", "yes", "y", "on"];

	private readonly ISqlValueConverter _converter;

	public ColumnValueCaster(ISqlValueConverter converter)
	{
		_converter = converter ?? throw new ArgumentNullException(nameof(converter));
	}

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

	public string ToText(object? value) => value switch
	{
		null or DBNull                => string.Empty,
		SqlFragment                   => throw new InvalidOperationException(
			"The referenced column takes its value from a key that SQL Server only knows while the script runs, so the value "
				+ "cannot be used in a pattern. Use the 'Copy of column' mode to copy it exactly, or insert directly into the database."
		),
		string text                   => text,
		bool booleanValue             => booleanValue ? "1" : "0",
		DateTime dateTimeValue        => dateTimeValue.TimeOfDay == TimeSpan.Zero
			? dateTimeValue.ToString(DATE_FORMAT, INVARIANT)
			: dateTimeValue.ToString(DATE_TIME_FORMAT, INVARIANT),
		DateTimeOffset offsetValue    => offsetValue.ToString("yyyy-MM-dd HH:mm:ss zzz", INVARIANT),
		TimeSpan timeValue            => timeValue.ToString(@"hh\:mm\:ss", INVARIANT),
		Guid guidValue                => guidValue.ToString("D").ToUpperInvariant(),
		byte[] bytes                  => $"0x{Convert.ToHexString(bytes)}",
		IFormattable formattableValue => formattableValue.ToString(null, INVARIANT),
		_                             => value.ToString() ?? string.Empty
	};

	private object CastToInteger(ColumnModel target, string sqlType, object? value)
	{
		decimal number = TryGetNumber(value, out decimal numericValue)
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

	private object CastToDecimal(ColumnModel target, string sqlType, object? value)
	{
		decimal number = TryGetNumber(value, out decimal numericValue)
			? numericValue
			: ParseDigits(ToText(value), allowDecimalPoint: true);

		if (sqlType is "float" or "real")
		{
			double floatingValue = (double)number;

			return sqlType == "real"
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

	private object CastToText(ColumnModel target, object? value)
	{
		string text          = ToText(value);
		int?   maximumLength = _converter.GetMaximumTextLength(target);

		return maximumLength.HasValue && text.Length > maximumLength.Value
			? text[..maximumLength.Value]
			: text;
	}

	/// <remarks>
	/// A number is read as a count of days after 1900-01-01, like CONVERT(datetime, number) in SQL Server.
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

	private DateTime ParseDateTime(object? value)
	{
		if (TryGetNumber(value, out decimal days))
		{
			return AddDays(days);
		}

		string text = ToText(value);

		if (DateTime.TryParse(text.Trim(), INVARIANT, DateTimeStyles.AllowWhiteSpaces, out DateTime parsed))
		{
			return parsed;
		}

		return AddDays(ParseDigits(text, allowDecimalPoint: false));
	}

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

		return _converter.TryConvertText(target, ToText(value), out object? converted, out _) && converted is not null
			? converted
			: TimeSpan.Zero;
	}

	private object CastToGuid(ColumnModel target, object? value)
	{
		if (value is Guid guidValue)
		{
			return guidValue;
		}

		string text = ToText(value);

		if (_converter.TryConvertText(target, text, out object? converted, out _) && converted is not null)
		{
			return converted;
		}

		// The same text always gives the same GUID, so copied values stay consistent between rows.
		return text.Length == 0 ? Guid.Empty : new Guid(MD5.HashData(Encoding.UTF8.GetBytes(text)));
	}

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

			bytes = _converter.TryConvertText(target, text, out object? converted, out _) && converted is byte[] hexBytes
				? hexBytes
				: Encoding.UTF8.GetBytes(text);
		}

		return target.MaximumLength is > 0 && bytes.Length > target.MaximumLength.Value
			? bytes[..target.MaximumLength.Value]
			: bytes;
	}

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
	/// Keeps only the digits of <paramref name="text"/> (and the first decimal point when allowed), e.g. '-AB12.5C' gives -12.5.
	/// Text without digits gives 0; numbers too large for a decimal give the largest decimal.
	/// </summary>
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

		decimal number = decimal.TryParse(digits, NumberStyles.AllowDecimalPoint, INVARIANT, out decimal parsed)
			? parsed
			: decimal.MaxValue;

		return text.TrimStart().StartsWith('-') ? -number : number;
	}

	private static DateTime AddDays(decimal days)
	{
		double minimumDays = (DateTime.MinValue - DEFAULT_DATE).TotalDays;
		double maximumDays = (DateTime.MaxValue - DEFAULT_DATE).TotalDays - 1;

		return DEFAULT_DATE.AddDays(Math.Clamp((double)days, minimumDays, maximumDays));
	}

	private static decimal Pow10(int exponent)
	{
		decimal value = 1;

		for (int index = 0; index < exponent; ++index)
		{
			value *= 10;
		}

		return value;
	}
}