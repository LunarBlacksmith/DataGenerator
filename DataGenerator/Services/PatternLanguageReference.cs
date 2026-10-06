namespace DataGenerator.Services;

/// <summary>
///	The functions and keywords of the pattern language. The parser, the pattern language reference window and the
///	suggestions shown while a pattern is typed all use this list, so a new function only needs to be described here.
/// </summary>
public static class PatternLanguageReference
{
	public static readonly IReadOnlyList<PatternLanguageEntry> FUNCTIONS =
	[
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

	public static readonly IReadOnlyList<PatternLanguageEntry> KEYWORDS =
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
			"Only numbers above n. Works with NUM and RAND_NUM, and can be combined, e.g. GREATER THAN 10 LESS THAN 20.",
			"NUM(digits=3) GREATER THAN 50"
		),
		Keyword(
			"LESS THAN",
			"NUM(…) LESS THAN n",
			"Only numbers below n. Works with NUM and RAND_NUM.",
			"RAND_NUM(0, 999) LESS THAN 100"
		),
		Keyword(
			"AT LEAST",
			"NUM(…) AT LEAST n",
			"Only numbers of n or more. Works with NUM and RAND_NUM.",
			"NUM(digits=4) AT LEAST 1000"
		),
		Keyword(
			"AT MOST",
			"NUM(…) AT MOST n",
			"Only numbers of n or less. Works with NUM and RAND_NUM.",
			"NUM() AT MOST 500"
		)
	];

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
}