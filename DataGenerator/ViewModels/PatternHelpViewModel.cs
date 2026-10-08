using System.Collections.ObjectModel;
using DataGenerator.Infrastructure;
using DataGenerator.Interfaces;
using DataGenerator.Services;

namespace DataGenerator.ViewModels;

/// <summary>
///	The pattern language reference window: syntax, functions, examples and a box to try expressions out.
/// </summary>
public sealed class PatternHelpViewModel : ObservableObject
{
	#region FIELDS
	private const int    SAMPLE_COUNT       = 6;
	private const string DEFAULT_EXPRESSION = "P FOLLOWED BY SEQ(1-1000, 1) FOLLOWED BY (X OR Y) FOLLOWED BY RAND_NUM(0, 99, 2)";

	private readonly IPatternValueGenerator _patternGenerator;

	private string _expression;
	private string _errorMessage;
	#endregion FIELDS

	#region PROPERTIES
	public ObservableCollection<PatternSample> Samples           { get; }
	public RelayCommand                        UseExampleCommand { get; }

	public IReadOnlyList<PatternSyntaxHelp> Operators { get; }

	public IReadOnlyList<PatternSyntaxHelp> Functions { get; }

	public IReadOnlyList<PatternExample> Examples { get; }

	public string Expression
	{
		get => _expression;
		set
		{
			if (SetProperty(ref _expression, value ?? string.Empty))
			{
				RefreshSamples();
			}
		}
	}

	public string ErrorMessage
	{
		get => _errorMessage;
		private set
		{
			if (SetProperty(ref _errorMessage, value))
			{
				OnPropertyChanged(nameof(HasError));
			}
		}
	}

	public bool HasError => _errorMessage.Length > 0;
	#endregion PROPERTIES

	#region CONSTRUCTOR
	/// <summary>
	///	Creates the pattern help view model and generates the first sample values.
	/// </summary>
	/// <param name="patternGenerator">
	///	The generator used to validate and sample pattern expressions.
	/// </param>
	/// <exception cref="ArgumentNullException">
	///	Thrown when <paramref name="patternGenerator"/> is <see langword="null"/>.
	/// </exception>
	public PatternHelpViewModel(IPatternValueGenerator patternGenerator)
	{
		Operators =
		[
			new PatternSyntaxHelp("A FOLLOWED BY B", "A then B. THEN and + mean the same.", "'ID-' + RAND_DIGITS(4)"),
			new PatternSyntaxHelp("A OR B", "Picks A or B at random for each row. | means the same.", "(Red OR Green OR Blue)"),
			new PatternSyntaxHelp("A REPEATED n TIMES", "A repeated n times. REPEATED 2 TO 4 TIMES picks a count at random; TIMES is optional.", "(X OR Y) REPEATED 3 TIMES"),
			new PatternSyntaxHelp("( … )", "Groups parts of a pattern.", "(A OR B) FOLLOWED BY 1"),
			new PatternSyntaxHelp("Text", "Single words and numbers can be written as they are. Quote anything with spaces, symbols or keywords.", "P + 'ORDER-' + \"O'Brien\" + 'OR'")
		];


		Functions = [
			.. PatternLanguageReference
				.FUNCTIONS
				.Select(
					entry => new PatternSyntaxHelp(entry.Signature, entry.Description, entry.Example)
				),
			new PatternSyntaxHelp("Nested functions", "A function can be an argument of another function; it is evaluated for each row first. Ranges need the comma form.", "RAND_DATE(TODAY(format='yyyy-MM-dd'), '2030-12-31')")
		];


		Examples =
		[
			new PatternExample("Product code", DEFAULT_EXPRESSION),
			new PatternExample("Asset tag", "'ASSET-' FOLLOWED BY SEQ(1-999999, digits=6)"),
			new PatternExample("Bin location", "RAND_LETTERS(2) + '-' + RAND_DIGITS(3) + '-' + (A OR B OR C)"),
			new PatternExample("Shirt size", "ONE_OF('XS', 'S', 'M', 'L', 'XL')"),
			new PatternExample("Order number", "'ORD' + RAND_DATE('2024-01-01', '2024-12-31', 'yyyyMMdd') + '-' + SEQ(1-9999)"),
			new PatternExample("Batch of today", "'BATCH-' + TODAY(format='yyyyMMdd') + '-' + SEQ(1-999)"),
			new PatternExample("Created today", "TODAY(ANY)"),
			new PatternExample("Due from today", "RAND_DATE(TODAY(format='yyyy-MM-dd'), '2030-12-31')"),
			new PatternExample("Based on a column", "COL(Colour) THEN '-' THEN SEQ(1-999)"),
			new PatternExample("E-mail address", "RAND_LETTERS(5-8, LOWER) + '.' + RAND_LETTERS(6, LOWER) + '@example.com'"),
			new PatternExample("Australian mobile", "'04' FOLLOWED BY RAND_DIGITS(8)"),
			new PatternExample("Licence plate", "RAND_LETTERS(3) + '-' + (RAND_DIGITS(1) OR RAND_LETTERS(1)) REPEATED 3 TIMES")
		];

		_patternGenerator = patternGenerator ?? throw new ArgumentNullException(nameof(patternGenerator));
		_expression       = DEFAULT_EXPRESSION;
		_errorMessage     = string.Empty;

		Samples           = [];
		UseExampleCommand = new RelayCommand(UseExample);

		RefreshSamples();
	}
	#endregion CONSTRUCTOR

	#region METHODS
	/// <summary>
	///	Copies a ready-made example into the editable expression box.
	/// </summary>
	/// <param name="parameter">
	///	The pattern example chosen by the command; other values are ignored.
	/// </param>
	private void UseExample(object? parameter)
	{
		if (parameter is PatternExample example)
		{
			Expression = example.Expression;
		}
	}

	/// <summary>
	///	Validates the current expression and rebuilds the sample rows shown in the help window.
	/// </summary>
	private void RefreshSamples()
	{
		Samples.Clear();

		if (!_patternGenerator.TryValidate(_expression, out string validationError))
		{
			ErrorMessage = validationError;
			return;
		}

		try
		{
			for (int rowIndex = 0; rowIndex < SAMPLE_COUNT; ++rowIndex)
			{
				// The try-it box has no row, so COL(name) shows the column's name in brackets.
				Samples.Add(new PatternSample(rowIndex + 1, _patternGenerator.Generate(_expression, rowIndex, SAMPLE_COUNT, columnName => $"[{columnName}]")));
			}

			ErrorMessage = string.Empty;
		}
		catch (Exception exception) when (exception is FormatException or OverflowException or ArgumentException or InvalidOperationException)
		{
			Samples.Clear();
			ErrorMessage = exception.Message;
		}
	}
	#endregion METHODS
}

/// <summary>
///	One line of the pattern language reference.
/// </summary>
public sealed record PatternSyntaxHelp
{
	#region PROPERTIES
	public string Syntax      { get; init; }
	public string Description { get; init; }
	public string Example     { get; init; }
	#endregion PROPERTIES

	/// <summary>
	///	Creates a new <see cref="PatternSyntaxHelp"/> from the supplied values.
	/// </summary>
	/// <param name="syntax">
	///	The value of <see cref="Syntax"/>.
	/// </param>
	/// <param name="description">
	///	The value of <see cref="Description"/>.
	/// </param>
	/// <param name="example">
	///	The value of <see cref="Example"/>.
	/// </param>
	public PatternSyntaxHelp(string syntax, string description, string example)
	{
		Syntax      = syntax;
		Description = description;
		Example     = example;
	}
}

/// <summary>
///	A ready-made pattern that can be loaded into the try-it box.
/// </summary>
public sealed record PatternExample
{
	#region PROPERTIES
	public string Title      { get; init; }
	public string Expression { get; init; }
	#endregion PROPERTIES

	/// <summary>
	///	Creates a new <see cref="PatternExample"/> from the supplied values.
	/// </summary>
	/// <param name="title">
	///	The value of <see cref="Title"/>.
	/// </param>
	/// <param name="expression">
	///	The value of <see cref="Expression"/>.
	/// </param>
	public PatternExample(string title, string expression)
	{
		Title      = title;
		Expression = expression;
	}
}

/// <summary>
///	A value generated by the try-it box for a one-based row number.
/// </summary>
public sealed record PatternSample
{
	#region PROPERTIES
	public int    RowNumber { get; init; }
	public string Value     { get; init; }
	#endregion PROPERTIES

	/// <summary>
	///	Creates a new <see cref="PatternSample"/> from the supplied values.
	/// </summary>
	/// <param name="rowNumber">
	///	The value of <see cref="RowNumber"/>.
	/// </param>
	/// <param name="value">
	///	The value of <see cref="Value"/>.
	/// </param>
	public PatternSample(int rowNumber, string value)
	{
		RowNumber = rowNumber;
		Value     = value;
	}
}