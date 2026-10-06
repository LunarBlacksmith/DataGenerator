using DataGenerator.Models;

namespace DataGenerator.ViewModels;

/// <summary>
///	One entry of the generation mode drop-down, with the text shown to the user.
/// </summary>
public sealed class GenerationModeOption
{
	#region FIELDS
	#region PUBLIC
	public static readonly IReadOnlyList<GenerationModeOption> ALL_OPTIONS;
	#endregion PUBLIC
	#endregion FIELDS

	#region PROPERTIES
	#region PUBLIC
	public ValueGenerationMode Mode        { get; }
	public string              DisplayName { get; }
	public string              Description { get; }
	#endregion PUBLIC
	#endregion PROPERTIES

	#region CONSTRUCTORS
	#region STATIC
	/// <summary>
	///	Sets the default values of the static fields and properties of <see cref="GenerationModeOption"/>.
	/// </summary>
	static GenerationModeOption()
	{
		ALL_OPTIONS =
		[
			new GenerationModeOption(
				ValueGenerationMode.KeepCurrent,
				"Keep current value",
				"Update sets only: the column is not changed; the row keeps the value it already has."
			),
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
				ValueGenerationMode.TableLookup,
				"Value from table",
				"A value of a column of any loaded table (or this one), taken from the rows already in the database and/or the rows "
				+ "generated earlier in this run, e.g. dbo.Shirt.ShirtID UNIQUE FROM GENERATED. Click ▾ to build it."
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
	}
	#endregion STATIC

	#region PRIVATE
	/// <summary>
	///	Creates a generation mode option with the text shown in the UI.
	/// </summary>
	/// <param name="mode">
	///	The generation mode the option chooses.
	/// </param>
	/// <param name="displayName">
	///	The short label shown in the mode list.
	/// </param>
	/// <param name="description">
	///	The explanatory text shown for the mode.
	/// </param>
	private GenerationModeOption(ValueGenerationMode mode, string displayName, string description)
	{
		Mode        = mode;
		DisplayName = displayName;
		Description = description;
	}
	#endregion PRIVATE
	#endregion CONSTRUCTORS

	#region METHODS
	#region PUBLIC
	/// <summary>
	///	Finds the option for a generation mode.
	/// </summary>
	/// <param name="mode">
	///	The generation mode to find.
	/// </param>
	/// <returns>
	///	The matching option.
	/// </returns>
	public static GenerationModeOption Get(ValueGenerationMode mode) => ALL_OPTIONS.First(option => option.Mode == mode);

	/// <summary>
	///	Returns the text shown for this generation mode.
	/// </summary>
	/// <returns>
	///	The display name.
	/// </returns>
	public override string ToString() => DisplayName;
	#endregion PUBLIC
	#endregion METHODS
}