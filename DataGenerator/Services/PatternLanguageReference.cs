namespace DataGenerator.Services;

/// <summary>
///	The functions and keywords of the pattern language. The parser, the pattern language reference window and the
///	suggestions shown while a pattern is typed all use this list, so a new function only needs to be described here.
/// </summary>
public static class PatternLanguageReference
{
	#region FIELDS
	public static readonly IReadOnlyList<PatternLanguageEntry> FUNCTIONS;

	public static readonly IReadOnlyList<PatternLanguageEntry> KEYWORDS;
	#endregion FIELDS

	#region CONSTRUCTOR
	/// <summary>
	///	Sets the default values of the static fields and properties of <see cref="PatternLanguageReference"/>.
	/// </summary>
	static PatternLanguageReference()
	{
		FUNCTIONS =
		[
			Function("IF", "IF(condition, thenValue, elseValue)",
				"Evaluates a boolean condition and only the selected branch. Branches can be values, COL calls or nested functions.",
				"IF(EQ(COL(Side), 'L'), 1, 2)"),
			Function("SUBSTRING", "SUBSTRING(text, startIndex, length)",
				"Extracts text using a 1-based index (first character is 1). Short results are truncated; past the end or NULL gives empty text. Converted to the destination column type during generation.",
				"SUBSTRING('P100', 2, 3)"),
			Function("EQ", "EQ(left, right)", "Equality: ordinal case-sensitive text; numeric values allow invariant numeric text. NULL equals only NULL.", "IF(EQ('L', 'L'), 1, 2)"),
			Function("NE", "NE(left, right)", "Not equal; the inverse of EQ. NULL is distinct from empty text.", "NE('L', 'R')"),
			Function("GT", "GT(left, right)", "Greater than for compatible non-null values. Two text operands compare ordinally; numeric operands compare numerically.", "GT(100, 50)"),
			Function("GE", "GE(left, right)", "Greater than or equal for compatible non-null values.", "GE(100, 100)"),
			Function("LT", "LT(left, right)", "Less than for compatible non-null values.", "LT(1, 2)"),
			Function("LE", "LE(left, right)", "Less than or equal for compatible non-null values.", "LE(1, 1)"),
			Function("AND", "AND(condition, condition, ...)", "TRUE only when every condition is true. Stops at the first false condition.", "AND(GT(100, 50), LT(100, 200))"),
			Function("OR", "OR(condition, condition, ...)", "TRUE when any condition is true. Stops at the first true condition. Unlike infix OR, this is not a random choice.", "OR(EQ('L', 'L'), EQ('L', 'R'))"),
			Function("NOT", "NOT(condition)", "Inverts a boolean condition. Numeric and NULL truth values are not accepted.", "NOT(FALSE)"),
			Function("IS_NULL", "IS_NULL(value)", "Tests explicit NULL or a NULL column value without treating empty text as NULL.", "IS_NULL(NULL)"),
			Function("CONTAINS", "CONTAINS(text, part)", "Ordinal case-sensitive containment of two non-null text values.", "CONTAINS('P100', '100')"),
			Function("STARTS_WITH", "STARTS_WITH(text, part)", "Ordinal case-sensitive prefix test of two non-null text values.", "STARTS_WITH('P100', 'P')"),
			Function("ENDS_WITH", "ENDS_WITH(text, part)", "Ordinal case-sensitive suffix test of two non-null text values.", "ENDS_WITH('P100', '100')"),
			Function(
				"SEQ",
				"SEQ(min-max, start, step, digits)",
				"Counts through min-max for each row, wrapping back to min after max. start (default min), step (default 1) and digits (default: the digits of max, zero-padded; 0 = no padding) are optional.",
				"SEQ(1-1000, 1) → 0001, 0002, …"
			),
			Function(
				"RAND_NUM",
				"RAND_NUM(min, max, digits)",
				"Random whole number from min to max inclusive. digits pads it with leading zeros (no padding by default).",
				"RAND_NUM(0, 99, 2) → 07"
			),
			Function(
				"ODD",
				"ODD(min, max, digits)",
				"Random odd whole number from min to max inclusive. digits pads it with leading zeros (no padding by default). The range must contain an odd number.",
				"ODD(0, 10, 2) → 07"
			),
			Function(
				"EVEN",
				"EVEN(min, max, digits)",
				"Random even whole number from min to max inclusive. digits pads it with leading zeros (no padding by default). The range must contain an even number.",
				"EVEN(1, 10, 2) → 04"
			),
			Function(
				"NUM",
				"NUM(min, max, digits)",
				"Random whole number with every parameter optional: NUM() is 0 to 999,999,999, NUM(digits=5) is 00000 to 99999 and NUM(1, 50) is the same as RAND_NUM(1, 50). Add GREATER THAN, LESS THAN, AT LEAST or AT MOST to narrow it.",
				"NUM(digits=5) GREATER THAN 50 → 08214"
			),
			Function(
				"ROW",
				"ROW(digits)",
				"The number of the row within its row set (1, 2, 3, …). digits pads it with leading zeros (no padding by default).",
				"'ITEM-' THEN ROW(4) → ITEM-0001"
			),
			Function(
				"CYCLE",
				"CYCLE(a, b, …)",
				"Takes the listed values in turn: the first row gets a, the second b, and so on, starting again after the last value.",
				"CYCLE('S', 'M', 'L') → S, M, L, S, …"
			),
			Function(
				"FIRST",
				"FIRST(value) or FIRST(n, v1, …, vn, else='…')",
				"The value for the first row of the row set, or one value for each of the first n rows (a single value is used for all n). Other rows get else (empty by default).",
				"FIRST(2, 'A', 'B', else='-') → A, B, -, -, …"
			),
			Function(
				"LAST",
				"LAST(value) or LAST(n, v1, …, vn, else='…')",
				"The value for the last row of the row set, or one value for each of the last n rows, in order (a single value is used for all n). Other rows get else (empty by default).",
				"LAST(2, 'Y', 'Z', else='-') → …, -, Y, Z"
			),
			Function(
				"UPPER",
				"UPPER(text)",
				"The text in capital letters. Most useful with COL(...) or another function.",
				"UPPER(COL(Colour)) → RED"
			),
			Function(
				"LOWER",
				"LOWER(text)",
				"The text in small letters. Most useful with COL(...) or another function.",
				"LOWER(COL(Colour)) → red"
			),
			Function(
				"LEFT",
				"LEFT(text, length)",
				"The first length characters of the text (all of it when it is shorter).",
				"LEFT(COL(Colour), 3) → Red"
			),
			Function(
				"RIGHT",
				"RIGHT(text, length)",
				"The last length characters of the text (all of it when it is shorter).",
				"RIGHT(COL(Code), 2) → 42"
			),
			Function(
				"PAD",
				"PAD(text, length, character)",
				"Puts character (default 0) in front of the text until it is length characters long.",
				"PAD(COL(Number), 6) → 000042"
			),
			Function(
				"RAND_DECIMAL",
				"RAND_DECIMAL(min, max, decimals)",
				"Random number with the given decimal places (default 2).",
				"RAND_DECIMAL(0, 100) → 42.17"
			),
			Function(
				"RAND_LETTERS",
				"RAND_LETTERS(length, case)",
				"Random letters. length can be a range such as 3-6; case is UPPER (default), LOWER or MIXED.",
				"RAND_LETTERS(3) → QXT"
			),
			Function(
				"RAND_DIGITS",
				"RAND_DIGITS(length)",
				"Random digits, leading zeros allowed.",
				"RAND_DIGITS(4) → 0381"
			),
			Function(
				"RAND_ALPHANUM",
				"RAND_ALPHANUM(length, case)",
				"Random letters and digits.",
				"RAND_ALPHANUM(8) → K3P9Z0QA"
			),
			Function(
				"RAND_DATE",
				"RAND_DATE('from', 'to', 'format')",
				"Random date between two quoted dates. format defaults to 'yyyy-MM-dd'.",
				"RAND_DATE('2024-01-01', '2024-12-31')"
			),
			Function(
				"TODAY",
				"TODAY(time, 'format')",
				"Today's date. time is NOW (default: the time the value is generated) or ANY (a random time of today). format defaults to 'yyyy-MM-dd HH:mm:ss'.",
				"TODAY(ANY, 'yyyy-MM-dd HH:mm')"
			),
			Function(
				"ONE_OF",
				"ONE_OF(a, b, …)",
				"Picks one of the listed values at random.",
				"ONE_OF('S', 'M', 'L', 'XL')"
			),
			Function(
				"GUID",
				"GUID(case)",
				"A new GUID. case is UPPER (default) or LOWER.",
				"GUID('LOWER')"
			),
			Function(
				"COL",
				"COL(name)",
				"The value of another column of the same row (shown as [name] in the reference window). The value is used as text; the result is converted to this column's type.",
				"COL(Colour) THEN '-' THEN 2"
			)
		];


		KEYWORDS =
		[
			Keyword(
				"FOLLOWED BY",
				"A FOLLOWED BY B",
				"A then B. THEN and + mean the same.",
				"'ID-' FOLLOWED BY RAND_DIGITS(4)"
			),
			Keyword(
				"THEN",
				"A THEN B",
				"A then B. FOLLOWED BY and + mean the same.",
				"'ID-' THEN RAND_DIGITS(4)"
			),
			Keyword(
				"REPEATED",
				"A REPEATED n TIMES",
				"A repeated n times. REPEATED 2 TO 4 TIMES picks a count at random; TIMES is optional.",
				"(X OR Y) REPEATED 3 TIMES"
			),
			Keyword(
				"TIMES",
				"A REPEATED n TIMES",
				"The optional word after the count of REPEATED.",
				"X REPEATED 2 TO 4 TIMES"
			),
			Keyword(
				"GREATER THAN",
				"NUM(…) GREATER THAN n",
				"Only numbers above n. Works with NUM, RAND_NUM, ODD and EVEN, and can be combined, e.g. GREATER THAN 10 LESS THAN 20.",
				"NUM(digits=3) GREATER THAN 50"
			),
			Keyword(
				"LESS THAN",
				"NUM(…) LESS THAN n",
				"Only numbers below n. Works with NUM, RAND_NUM, ODD and EVEN.",
				"RAND_NUM(0, 999) LESS THAN 100"
			),
			Keyword(
				"AT LEAST",
				"NUM(…) AT LEAST n",
				"Only numbers of n or more. Works with NUM, RAND_NUM, ODD and EVEN.",
				"NUM(digits=4) AT LEAST 1000"
			),
			Keyword(
				"AT MOST",
				"NUM(…) AT MOST n",
				"Only numbers of n or less. Works with NUM, RAND_NUM, ODD and EVEN.",
				"NUM() AT MOST 500"
			)
		];
	}
	#endregion CONSTRUCTOR

	#region METHODS
	/// <summary>
	///	Creates a pattern-language function entry for the shared reference list.
	/// </summary>
	/// <param name="name">
	///	The function name shown in completions.
	/// </param>
	/// <param name="signature">
	///	The signature shown in the reference window.
	/// </param>
	/// <param name="description">
	///	The user-facing description of what the function generates.
	/// </param>
	/// <param name="example">
	///	An example pattern or result for the function.
	/// </param>
	/// <returns>
	///	A function entry with the supplied display text.
	/// </returns>
	private static PatternLanguageEntry Function(string name, string signature, string description, string example)
		=> new PatternLanguageEntry(name, signature, description, example, PatternLanguageEntryKind.Function);

	/// <summary>
	///	Creates a pattern-language keyword entry for the shared reference list.
	/// </summary>
	/// <param name="name">
	///	The keyword text shown in completions.
	/// </param>
	/// <param name="signature">
	///	The usage form shown in the reference window.
	/// </param>
	/// <param name="description">
	///	The user-facing description of what the keyword does.
	/// </param>
	/// <param name="example">
	///	An example pattern that uses the keyword.
	/// </param>
	/// <returns>
	///	A keyword entry with the supplied display text.
	/// </returns>
	private static PatternLanguageEntry Keyword(string name, string signature, string description, string example)
		=> new PatternLanguageEntry(name, signature, description, example, PatternLanguageEntryKind.Keyword);
	#endregion METHODS
}