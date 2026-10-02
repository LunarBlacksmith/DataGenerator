namespace DataGenerator.Services;

/// <summary>
/// The functions and keywords of the pattern language. The parser, the pattern language reference window and the
/// suggestions shown while a pattern is typed all use this list, so a new function only needs to be described here.
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
		)
	];

	private static PatternLanguageEntry Function(string name, string signature, string description, string example)
		=> new PatternLanguageEntry(name, signature, description, example, PatternLanguageEntryKind.Function);

	private static PatternLanguageEntry Keyword(string name, string signature, string description, string example)
		=> new PatternLanguageEntry(name, signature, description, example, PatternLanguageEntryKind.Keyword);
}