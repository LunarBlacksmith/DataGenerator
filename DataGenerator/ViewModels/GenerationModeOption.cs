using DataGenerator.Models;

namespace DataGenerator.ViewModels;

/// <summary>
/// One entry of the generation mode drop-down, with the text shown to the user.
/// </summary>
public sealed class GenerationModeOption
{
	public static readonly IReadOnlyList<GenerationModeOption> ALL_OPTIONS =
	[
		new GenerationModeOption(
			ValueGenerationMode.Random,
			"Random",
			"A random value that suits the column's SQL type."
		),
		new GenerationModeOption(
			ValueGenerationMode.Fixed,
			"Fixed value",
			"The same value in every row."
		),
		new GenerationModeOption(
			ValueGenerationMode.Sequence,
			"Sequence",
			"Counts from a start value by a fixed step: start, start + step, start + 2 × step, … Restarts for every row set."
		),
		new GenerationModeOption(
			ValueGenerationMode.Pattern,
			"Pattern",
			"Builds each value from a plain-English pattern, e.g. P FOLLOWED BY SEQ(1-1000) FOLLOWED BY (X OR Y). Click ? for the reference."
		),
		new GenerationModeOption(
			ValueGenerationMode.Regex,
			"Regex",
			"Random text that matches a simple regular expression, e.g. [A-Z]{3}-[0-9]{4}."
		),
		new GenerationModeOption(
			ValueGenerationMode.CopyColumn,
			"Copy of column",
			"The value of another column of the same row, converted to this column's type (e.g. text copied into a number column keeps only its digits)."
		),
		new GenerationModeOption(
			ValueGenerationMode.GeneratedForeignKey,
			"Generated key",
			"A key of a row generated for the referenced table in the same run. The referenced table must be included."
		),
		new GenerationModeOption(
			ValueGenerationMode.ExistingForeignKey,
			"Existing key",
			"A key that already exists in the referenced table."
		),
		new GenerationModeOption(
			ValueGenerationMode.DatabaseGenerated,
			"Database generated",
			"Leaves the column out of the INSERT so SQL Server supplies the value (identity, computed, rowversion or default)."
		),
		new GenerationModeOption(
			ValueGenerationMode.Null,
			"NULL",
			"NULL in every row."
		)
	];

	private GenerationModeOption(ValueGenerationMode mode, string displayName, string description)
	{
		Mode        = mode;
		DisplayName = displayName;
		Description = description;
	}

	public ValueGenerationMode Mode        { get; }
	public string              DisplayName { get; }
	public string              Description { get; }

	public static GenerationModeOption Get(ValueGenerationMode mode) => ALL_OPTIONS.First(option => option.Mode == mode);

	public override string ToString() => DisplayName;
}