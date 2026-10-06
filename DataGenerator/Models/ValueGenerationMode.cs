namespace DataGenerator.Models;

public enum ValueGenerationMode
{
	Random              = 0,
	Fixed               = 1,
	Sequence            = 2,
	Regex               = 3,
	ExistingForeignKey  = 4,
	GeneratedForeignKey = 5,
	DatabaseGenerated   = 6,
	Null                = 7,
	Pattern             = 8,
	CopyColumn          = 9,
	TableLookup         = 10,
	KeepCurrent         = 11
}