using System.IO;
using DataGenerator.Interfaces;
using DataGenerator.Models;
using DataGenerator.Services;
using DataGenerator.Services.Patterns;
using Xunit;

namespace DataGenerator.Tests;

public sealed class PatternExpressionTests
{
	#region FIELDS
	private readonly PatternValueGenerator _generator;
	#endregion FIELDS

	#region CONSTRUCTOR
	public PatternExpressionTests()
	{
		_generator = new();
	}
	#endregion CONSTRUCTOR

	#region METHODS
	#region PUBLIC
	[Theory]
	[InlineData("IF(EQ('L', 'L'), 1, 2)", "1")]
	[InlineData("IF(EQ('R', 'L'), 1, 2)", "2")]
	[InlineData("IF(TRUE, 'chosen', COL(Unavailable))", "chosen")]
	[InlineData("IF(FALSE, COL(Unavailable), 'chosen')", "chosen")]
	[InlineData("IF(TRUE, 1, IF(TRUE, COL(Unavailable), 3))", "1")]
	[InlineData("IF(FALSE, 1, IF(EQ(2, 2), 'two', 3))", "two")]
	[InlineData("IF(EQ(1, '01'), TRUE, FALSE)", "true")]
	[InlineData("EQ('01', '1')", "false")]
	[InlineData("EQ('L', 'l')", "false")]
	[InlineData("NE('L', 'R')", "true")]
	[InlineData("GT('2', '10')", "true")]
	[InlineData("GT(2, '10')", "false")]
	[InlineData("GE(2, 2)", "true")]
	[InlineData("LT(-2, 1)", "true")]
	[InlineData("LE(2.5, '2.5')", "true")]
	[InlineData("EQ(NULL, NULL)", "true")]
	[InlineData("EQ(NULL, '')", "false")]
	[InlineData("NE(NULL, '')", "true")]
	[InlineData("IS_NULL(NULL)", "true")]
	[InlineData("IS_NULL('NULL')", "false")]
	[InlineData("IS_NULL('')", "false")]
	[InlineData("IF(TRUE, NULL, 'other')", "")]
	[InlineData("EQ(TRUE, 'true')", "true")]
	[InlineData("NE(TRUE, FALSE)", "true")]
	[InlineData("NOT(FALSE)", "true")]
	[InlineData("AND(TRUE, EQ(2, 2), NOT(FALSE))", "true")]
	[InlineData("AND(FALSE, EQ(COL(Unavailable), 'x'))", "false")]
	[InlineData("OR(FALSE, FALSE, TRUE)", "true")]
	[InlineData("OR(TRUE, EQ(COL(Unavailable), 'x'))", "true")]
	[InlineData("CONTAINS('P100', '100')", "true")]
	[InlineData("STARTS_WITH('P100', 'p')", "false")]
	[InlineData("ENDS_WITH('P100', '100')", "true")]
	[InlineData("SUBSTRING('P100', 2, 3)", "100")]
	[InlineData("SUBSTRING(12345, 2, 3)", "234")]
	[InlineData("SUBSTRING(-123.5, 2, 3)", "123")]
	[InlineData("SUBSTRING('P100', 1, 1)", "P")]
	[InlineData("SUBSTRING('P100', 2, 1000)", "100")]
	[InlineData("SUBSTRING('P100', 5, 1)", "")]
	[InlineData("SUBSTRING('P100', 2147483647, 2147483647)", "")]
	[InlineData("SUBSTRING('', 1, 3)", "")]
	[InlineData("SUBSTRING(NULL, 1, 3)", "")]
	[InlineData("SUBSTRING('P100', 1, 0)", "")]
	[InlineData("SUBSTRING(UPPER('p100'), 1, 1)", "P")]
	[InlineData("SUBSTRING(IF(TRUE, 'P100', COL(Unavailable)), 2, 3)", "100")]
	[InlineData("IF(EQ(SUBSTRING('P100', 2, 3), '100'), 'yes', 'no')", "yes")]
	[InlineData("'ID-' + IF(TRUE, SUBSTRING('P100', 2, 3), 0)", "ID-100")]
	[InlineData("UPPER(IF(FALSE, COL(Unavailable), 'safe'))", "SAFE")]
	public void GeneratesBoundedExpressions(string expression, string expected)
	{
		Assert.Equal(expected, _generator.Generate(expression, 0));
	}

	[Theory]
	[InlineData("IF(TRUE, 1)")]
	[InlineData("IF(1, 1, 2)")]
	[InlineData("IF(NULL, 1, 2)")]
	[InlineData("EQ(1)")]
	[InlineData("AND(TRUE)")]
	[InlineData("OR()")]
	[InlineData("NOT(1)")]
	[InlineData("SUBSTRING('P100', 0, 3)")]
	[InlineData("SUBSTRING('P100', -1, 3)")]
	[InlineData("SUBSTRING('P100', 2, -1)")]
	[InlineData("SUBSTRING('P100', 1.5, 1)")]
	[InlineData("SUBSTRING('P100', 1, 0.5)")]
	[InlineData("SUBSTRING('P100', 2147483648, 1)")]
	[InlineData("SUBSTRING('P100', TRUE, 1)")]
	[InlineData("SUBSTRING('P100', NULL, 1)")]
	[InlineData("IF(condition=TRUE, 1, 2)")]
	[InlineData("EQ(1-2, 1)")]
	public void RejectsInvalidSyntaxAndLiteralBounds(string expression)
	{
		Assert.False(_generator.TryValidate(expression, out string error));
		Assert.NotEmpty(error);
		Assert.Throws<PatternSyntaxException>(() => _generator.Generate(expression, 0));
	}

	[Theory]
	[InlineData("GT(NULL, 1)")]
	[InlineData("GE(TRUE, FALSE)")]
	[InlineData("EQ(TRUE, 1)")]
	[InlineData("EQ(1, 'text')")]
	[InlineData("CONTAINS(NULL, 'x')")]
	[InlineData("STARTS_WITH(123, '1')")]
	[InlineData("IF(SUBSTRING('x', 1, 1), 1, 2)")]
	public void RejectsIncompatibleValuesExplicitly(string expression)
	{
		Assert.Throws<PatternSyntaxException>(() => _generator.Generate(expression, 0));
	}

	[Theory]
	[InlineData("L", "1")]
	[InlineData("R", "2")]
	public void UsesSameRowColumnValues(string side, string expected)
	{
		Assert.Equal(expected, _generator.Generate("IF(EQ(COL(Side), 'L'), 1, 2)", 0, 1, _ => side));
		Assert.Equal("100", _generator.Generate("SUBSTRING(COL(Code), 2, 3)", 0, 1, _ => "P100"));
		Assert.Equal("100", _generator.Generate("SUBSTRING(COL(Number), 2, 3)", 0, 1, _ => "9100", _ => 9100));
	}

	[Fact]
	public void PreservesTypedColumnNullBooleanAndDateValues()
	{
		Assert.Equal("true", _generator.Generate("IS_NULL(COL(Optional))", 0, 1, _ => "", _ => null));
		Assert.Equal("false", _generator.Generate("IS_NULL(COL(Optional))", 0, 1, _ => "", _ => ""));
		Assert.Equal("true", _generator.Generate("EQ(COL(Flag), TRUE)", 0, 1, _ => "1", _ => true));
		Assert.Equal("true", _generator.Generate("GT(COL(Count), '09')", 0, 1, _ => "10", _ => 10));
		Assert.Equal("true", _generator.Generate("GT(COL(Later), COL(Earlier))", 0, 1, _ => "",
			name => name == "Later" ? new DateTime(2026, 1, 2) : new DateTime(2026, 1, 1)));
		Assert.Throws<PatternSyntaxException>(() => _generator.Generate("EQ(COL(Date), 1)", 0, 1, _ => "",
			_ => new DateTime(2026, 1, 1)));
		Assert.Throws<InvalidOperationException>(() => _generator.Generate("EQ(COL(Deferred), 1)", 0, 1, _ => "",
			_ => new SqlFragment("unsafe")));
	}

	[Fact]
	public void EvaluatesOnlySelectedBranchAndShortCircuitsConditions()
	{
		int calls = 0;
		Func<string, string> values = name =>
		{
			++calls;
			Assert.Equal("Chosen", name);
			return "selected";
		};

		Assert.Equal("selected", _generator.Generate("IF(TRUE, COL(Chosen), COL(Unchosen))", 0, 1, values));
		Assert.Equal(1, calls);
		Assert.Equal("true", _generator.Generate("OR(TRUE, EQ(COL(Unchosen), 1))", 0, 1, values));
		Assert.Equal("false", _generator.Generate("AND(FALSE, EQ(COL(Unchosen), 1))", 0, 1, values));
		Assert.Equal(1, calls);
	}

	[Fact]
	public void CollectsReferencesFromAllBranchesForExistingDependencyValidation()
	{
		IReadOnlyList<string> references = _generator.GetColumnReferences(
			"IF(EQ(COL(Side), 'L'), SUBSTRING(COL(Code), 2, 3), COL(Other))");

		Assert.Equal(["Side", "Code", "Other"], references);
	}

	[Theory]
	[InlineData("IF(TRUE, 1, 2)")]
	[InlineData("SUBSTRING('P100', 2, 3)")]
	[InlineData("EQ(1, 1)")]
	public void RejectsExpressionFunctionsInLookupSqlFilters(string expression)
	{
		PatternSqlTranslator translator = new();

		Assert.False(translator.TryValidate(expression, out string error));
		Assert.Contains("cannot be used to find existing values", error);
		Assert.Throws<PatternSyntaxException>(() => translator.ToSqlCondition(expression, "[Value]"));
	}

	[Theory]
	[InlineData("SUBSTRING('P100', 2, 3)", false)]
	[InlineData("SUBSTRING(COL(Column1), 2, 3)", true)]
	[InlineData("IF(EQ(COL(Side), 'L'), 100, 200)", true)]
	public void ConvertsPatternOutputToTargetIntUsingExistingGenerationPath(string expression, bool references)
	{
		SqlValueConverter    converter = new();
		ColumnValueGenerator generator = new(converter, new RegexValueGenerator(), _generator, new ColumnValueCaster(converter));
		ColumnModel          column    = new() { Name = "Column2", SqlType = "int" };
		ColumnRule           rule      = new() { Column = column, GenerationMode = ValueGenerationMode.Pattern, PatternExpression = expression };

		object? value = generator.Generate(rule, 0, 1, references ? new RowValues() : null);

		Assert.Equal(100, Assert.IsType<int>(value));
		Assert.Equal("100", converter.ToSqlLiteral(column, value));
	}

	[Fact]
	public void EscapesSelectedBranchForSqlOutput()
	{
		SqlValueConverter converter = new();
		ColumnModel column = new() { Name = "Text", SqlType = "nvarchar", MaximumLength = 100 };
		string value = _generator.Generate("IF(TRUE, 'O''Brien', 'unused')", 0);

		Assert.Equal("N'O''Brien'", converter.ToSqlLiteral(column, value));
	}

	[Fact]
	public void InvalidColumnAndDynamicBoundsAreNotSilentlyDefaulted()
	{
		Assert.Throws<InvalidOperationException>(() => _generator.Generate("SUBSTRING(COL(Missing), 1, 3)", 0, 1,
			_ => throw new InvalidOperationException("Unknown column")));
		Assert.Throws<PatternSyntaxException>(() => _generator.Generate("SUBSTRING('P100', COL(Start), 3)", 0, 1, _ => "0"));
	}

	[Fact]
	public void BoundsNestedFunctionsAndAcceptsManyLogicalAlternatives()
	{
		string tooDeep = string.Concat(Enumerable.Repeat("NOT(", 65)) + "TRUE" + new string(')', 65);
		string manyConditions = "OR(" + string.Join(", ", Enumerable.Repeat("FALSE", 999)) + ", TRUE)";

		Assert.False(_generator.TryValidate(tooDeep, out string error));
		Assert.Contains("64 levels", error);
		Assert.Equal("true", _generator.Generate(manyConditions, 0));
	}

	[Fact]
	public void PreservesExistingChoiceAndColumnFormatting()
	{
		string choice = _generator.Generate("(L OR R)", 0);

		Assert.Contains(choice, new[] { "L", "R" });
		Assert.Equal("legacy-text", _generator.Generate("COL(Source)", 0, 1, _ => "legacy-text", _ => 100));
		Assert.Equal("true", _generator.Generate("EQ(COL(Source), 100)", 0, 1, _ => "legacy-text", _ => 100));
	}

	[Theory]
	[InlineData("IF(FALSE, COL(Missing), 1)", "has no column")]
	[InlineData("IF(FALSE, COL(Column2), 1)", "own value")]
	public async Task ValidatesEvenUnselectedColumnReferencesBeforeWritingSql(string expression, string expectedError)
	{
		GenerationRequest request = CreateRequest(expression, Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.sql"));
		DataGenerationService service = CreateService();

		try
		{
			DataGenerationException exception = await Assert.ThrowsAsync<DataGenerationException>(
				() => service.GenerateAsync(request, null, CancellationToken.None));

			Assert.Contains(expectedError, exception.Message);
			Assert.False(File.Exists(request.OutputFilePath));
		}
		finally
		{
			File.Delete(request.OutputFilePath!);
		}
	}

	[Fact]
	public async Task WritesSqlWithDependencyOrderedExtractionAndConditionalValues()
	{
		GenerationRequest request = CreateRequest("IF(EQ(COL(Side), 'L'), SUBSTRING(COL(Column1), 2, 3), 200)",
			Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.sql"));
		DataGenerationService service = CreateService();

		try
		{
			await service.GenerateAsync(request, null, CancellationToken.None);

			string sql = await File.ReadAllTextAsync(request.OutputFilePath!);

			Assert.Contains("(100, N'P100', N'L', N'O''Brien')", sql);
			Assert.Contains("(200, N'P100', N'R', N'O''Brien')", sql);
			Assert.DoesNotContain("IF(", sql);
			Assert.DoesNotContain("SUBSTRING(", sql);
			Assert.Contains("COMMIT", sql);
		}
		finally
		{
			File.Delete(request.OutputFilePath!);
		}
	}
	#endregion PUBLIC
	#region PRIVATE
	private static GenerationRequest CreateRequest(string expression, string path)
	{
		ColumnModel column1 = new() { Name = "Column1", SqlType = "nvarchar", MaximumLength = 100 };
		ColumnModel column2 = new() { Name = "Column2", SqlType = "int" };
		ColumnModel side    = new() { Name = "Side", SqlType = "nvarchar", MaximumLength = 1 };
		ColumnModel text    = new() { Name = "Text", SqlType = "nvarchar", MaximumLength = 100 };
		TableModel  table   = new() { DatabaseName = "Test", SchemaName = "dbo", Name = "Items" };

		table.Columns.Add(column1);
		table.Columns.Add(column2);
		table.Columns.Add(side);
		table.Columns.Add(text);

		return new GenerationRequest
		{
			Mode = GenerationMode.SqlFile,
			OutputFilePath = path,
			Plans =
			[
				new TableGenerationPlan
				{
					Table = table,
					RowSets =
					[
						new RowSetPlan
						{
							Name = "Set 1",
							RowCount = 2,
							Rules =
							[
								new ColumnRule { Column = column2, GenerationMode = ValueGenerationMode.Pattern, PatternExpression = expression },
								new ColumnRule { Column = column1, GenerationMode = ValueGenerationMode.Fixed, FixedValue = "P100" },
								new ColumnRule { Column = side, GenerationMode = ValueGenerationMode.Pattern, PatternExpression = "CYCLE('L', 'R')" },
								new ColumnRule { Column = text, GenerationMode = ValueGenerationMode.Pattern, PatternExpression = "IF(TRUE, 'O''Brien', 'unused')" }
							]
						}
					]
				}
			]
		};
	}

	private DataGenerationService CreateService()
	{
		SqlValueConverter converter = new();
		ColumnValueGenerator generator = new(converter, new RegexValueGenerator(), _generator, new ColumnValueCaster(converter));

		return new DataGenerationService(converter, generator, new PatternSqlTranslator());
	}
	#endregion PRIVATE
	#endregion METHODS

	#region TYPES
	private sealed class RowValues : IRowValueLookup
	{
		#region CONSTRUCTOR
		public RowValues()
		{
		}
		#endregion CONSTRUCTOR

		#region METHODS
		public object? GetValue(string columnName) => columnName switch
		{
			"Column1" => "P100",
			"Side"    => "L",
			_         => throw new InvalidOperationException($"Unknown column: {columnName}")
		};
		#endregion METHODS
	}
	#endregion TYPES
}
