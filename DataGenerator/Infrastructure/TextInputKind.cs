namespace DataGenerator.Infrastructure;

/// <summary>
/// The kind of value a text box accepts. Used by <see cref="TextInputFilter"/> to block characters that can never be valid.
/// </summary>
public enum TextInputKind
{
	Any         = 0,
	Integer     = 1,
	Decimal     = 2,
	Boolean     = 3,
	DateTime    = 4,
	Time        = 5,
	Guid        = 6,
	Hexadecimal = 7
}