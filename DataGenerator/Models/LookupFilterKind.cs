namespace DataGenerator.Models;

/// <summary>
/// How the WHERE part of a "Value from table" rule selects the values that may be used.
/// </summary>
public enum LookupFilterKind
{
	None    = 0,
	Pattern = 1,
	Regex   = 2,
	Sql     = 3
}
