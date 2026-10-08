using System.Globalization;
using System.Text.RegularExpressions;
using DataGenerator.Models;
using DataGenerator.Services;
using DataGenerator.Services.Patterns;
using Xunit;

namespace DataGenerator.Tests;

public sealed class ExpressionBuilderTests
{
	#region FIELDS
	private const string EXAMPLE = "MS[10]-Y[01, 03, 05]-X[001 > 050]-[LR]-D[1]";
	#endregion FIELDS

	#region CONSTRUCTOR
	public ExpressionBuilderTests()
	{
	}
	#endregion CONSTRUCTOR

	#region METHODS
	[Theory]
	[InlineData("MS00-Y01-X051-L-D0", true)]
	[InlineData("MS99-Y03-X999-R-D9", true)]
	[InlineData("MS10-Y05-X100-L-D1", true)]
	[InlineData("MS00-Y01-X050-L-D0", false)]
	[InlineData("MS00-Y01-X001-L-D0", false)]
	[InlineData("MS00-Y02-X051-L-D0", false)]
	[InlineData("MS00-Y1-X051-L-D0", false)]
	[InlineData("MS00-Y01-X51-L-D0", false)]
	[InlineData("MS00-Y01-X1000-L-D0", false)]
	[InlineData("MS00-Y01-X051-Z-D0", false)]
	[InlineData("ms00-Y01-X051-L-D0", false)]
	[InlineData("MS\u0660\u0660-Y01-X051-L-D0", false)]
	[InlineData("MS00-Y01-X051-L-D0\n", false)]
	[InlineData("MS00-Y01-X051-L-D0 ", false)]
	public void ExampleRegexMatchesExactlyRequestedLanguage(string value, bool expected)
	{
		ExpressionBuilder builder = new();
		BuiltExpression   result  = builder.Build(EXAMPLE, "Value");

		Assert.Equal(expected, Regex.IsMatch(value, result.Regex, RegexOptions.None, TimeSpan.FromSeconds(1)));
	}

	[Fact]
	public void ExampleProducesUsableGenerationPatternAndDescriptions()
	{
		ExpressionBuilder     builder = new();
		BuiltExpression       result  = builder.Build(EXAMPLE, "Value");
		PatternValueGenerator generator = new();

		Assert.Contains("RAND_NUM(0, 99, 2)", result.Pattern);
		Assert.Contains("ONE_OF('01', '03', '05')", result.Pattern);
		Assert.Contains("RAND_NUM(51, 999, 3)", result.Pattern);
		Assert.Contains("ONE_OF('L', 'R')", result.Pattern);
		Assert.Contains("RAND_NUM(0, 9, 1)", result.Pattern);
		Assert.Contains("051 to 999 (strictly greater than 50)", result.Description);
		Assert.Equal(10, result.Description.Split(Environment.NewLine).Length);

		for (int row = 0; row < 200; ++row)
		{
			string generated = generator.Generate(result.Pattern, row);
			Assert.Matches(result.Regex, generated);
		}
	}

	[Theory]
	[InlineData("[1]", 0, 9)]
	[InlineData("[10]", 0, 99)]
	[InlineData("[001 > 050]", 51, 999)]
	[InlineData("[001 > 009]", 10, 999)]
	[InlineData("[001 > 099]", 100, 999)]
	[InlineData("[001 > 989]", 990, 999)]
	[InlineData("[001 > 998]", 999, 999)]
	[InlineData("[01 > 00]", 1, 99)]
	public void NumericRegexPreciselyMatchesEveryValueAtWidth(string feed, int minimum, int maximum)
	{
		ExpressionBuilder builder = new();
		BuiltExpression result = builder.Build(feed, "Code");
		int width = feed.StartsWith("[001", StringComparison.Ordinal) ? 3 : feed.StartsWith("[1]", StringComparison.Ordinal) ? 1 : 2;

		for (int value = 0; value <= maximum; ++value)
		{
			string text = value.ToString(CultureInfo.InvariantCulture).PadLeft(width, '0');
			Assert.Equal(value >= minimum, Regex.IsMatch(text, result.Regex));
		}

		Assert.DoesNotMatch(result.Regex, new string('9', width + 1));
	}

	[Fact]
	public void SqlUsesExactLengthBinaryComparisonsAndSafeNumericConversion()
	{
		ExpressionBuilder builder = new();
		BuiltExpression result = builder.Build(EXAMPLE, "Value");

		Assert.StartsWith("DATALENGTH(CONVERT(nvarchar(max), [Value])) = 36", result.Sql);
		Assert.Contains("COLLATE Latin1_General_100_BIN2", result.Sql);
		Assert.Contains("SUBSTRING(CONVERT(nvarchar(max), [Value]) COLLATE Latin1_General_100_BIN2, 7, 2) IN (N'01', N'03', N'05')", result.Sql);
		Assert.Contains("SUBSTRING(CONVERT(nvarchar(max), [Value]) COLLATE Latin1_General_100_BIN2, 11, 3)", result.Sql);
		Assert.Contains("TRY_CONVERT(int,", result.Sql);
		Assert.Contains(") > 50", result.Sql);
		Assert.Contains("NOT LIKE N'%[^0-9]%'", result.Sql);
		Assert.DoesNotContain("REGEXP", result.Sql);
		Assert.DoesNotContain("EXEC", result.Sql);
	}

	[Fact]
	public void QuotesIdentifiersAndLiteralMetacharactersInsteadOfExecutingThem()
	{
		ExpressionBuilder     builder   = new();
		string                feed      = @"O'Brien.\[x\]\\";
		BuiltExpression       result    = builder.Build(feed, "a]; DROP TABLE t;--");
		PatternValueGenerator generator = new();

		Assert.Matches(result.Regex, @"O'Brien.[x]\");
		Assert.Equal(@"O'Brien.[x]\", generator.Generate(result.Pattern, 1));
		Assert.Contains("N'O''Brien.[x]\\'", result.Sql);
		Assert.Contains("[a]]; DROP TABLE t;--]", result.Sql);
	}

	[Theory]
	[InlineData("")]
	[InlineData("   ")]
	[InlineData("[]")]
	[InlineData("[01, 3]")]
	[InlineData("[01, 01]")]
	[InlineData("[01, X]")]
	[InlineData("[LL]")]
	[InlineData("[001 >= 050]")]
	[InlineData("[001 < 050]")]
	[InlineData("[001 > 999]")]
	[InlineData("[001 > 50]")]
	[InlineData("[001 > 050 > 100]")]
	[InlineData("[001..050]")]
	[InlineData("[0123456789]")]
	[InlineData("[LR")]
	[InlineData("LR]")]
	[InlineData("[[LR]]")]
	[InlineData(@"abc\q")]
	[InlineData("abc\\")]
	[InlineData("[01,\n03]")]
	[InlineData("\u00e9")]
	public void MalformedOrAmbiguousFeedIsRejectedExplicitly(string feed)
	{
		ExpressionBuilder builder = new();
		Assert.Throws<FormatException>(() => builder.Build(feed, "Value"));
	}

	[Fact]
	public void InputLimitsAndColumnValidationAreExplicit()
	{
		ExpressionBuilder builder = new();
		Assert.Throws<FormatException>(() => builder.Build(new string('a', 1025), "Value"));
		Assert.Throws<FormatException>(() => builder.Build("a", ""));
		Assert.Throws<FormatException>(() => builder.Build("a", new string('a', 129)));
		Assert.Throws<FormatException>(() => builder.Build("a", "line\nbreak"));
		Assert.Throws<ArgumentNullException>(() => builder.Build(null!, "Value"));
		Assert.Throws<ArgumentNullException>(() => builder.Build("a", null!));
	}
	#endregion METHODS
}
