using DataGenerator.Interfaces;
using DataGenerator.Models;
using DataGenerator.Services;

namespace DataGenerator.ViewModels;

/// <summary>
///	Services and shared settings used by every <see cref="ColumnRuleViewModel"/>.
/// </summary>
public sealed class ColumnRuleServices
{
	/// <summary>
	///	Groups the services and shared settings needed by column rule view models.
	/// </summary>
	/// <param name="converter">
	///	Converts typed values to and from SQL text.
	/// </param>
	/// <param name="valueGenerator">
	///	Generates sample values for random, fixed and sequence modes.
	/// </param>
	/// <param name="patternGenerator">
	///	Validates and generates values from pattern expressions.
	/// </param>
	/// <param name="regexProfiles">
	///	The built-in regular-expression examples offered to the user.
	/// </param>
	/// <param name="savedSettings">
	///	The saved column settings library.
	/// </param>
	/// <param name="savedSettingsWindows">
	///	The window service used to save and manage column settings.
	/// </param>
	/// <param name="tableCatalog">
	///	The loaded tables available to lookup rules.
	/// </param>
	/// <param name="lookupParser">
	///	The parser used to validate and format lookup expressions.
	/// </param>
	/// <exception cref="ArgumentNullException">
	///	Thrown when any service or settings argument is <see langword="null"/>.
	/// </exception>
	public ColumnRuleServices(
		ISqlValueConverter          converter,
		IColumnValueGenerator       valueGenerator,
		IPatternValueGenerator      patternGenerator,
		IReadOnlyList<RegexProfile> regexProfiles,
		SavedSettingsLibrary        savedSettings,
		ISavedSettingsWindowService savedSettingsWindows,
		ITableCatalog               tableCatalog,
		ILookupExpressionParser     lookupParser
	)
	{
		Converter            = converter ?? throw new ArgumentNullException(nameof(converter));
		ValueGenerator       = valueGenerator ?? throw new ArgumentNullException(nameof(valueGenerator));
		PatternGenerator     = patternGenerator ?? throw new ArgumentNullException(nameof(patternGenerator));
		RegexProfiles        = regexProfiles ?? throw new ArgumentNullException(nameof(regexProfiles));
		SavedSettings        = savedSettings ?? throw new ArgumentNullException(nameof(savedSettings));
		SavedSettingsWindows = savedSettingsWindows ?? throw new ArgumentNullException(nameof(savedSettingsWindows));
		TableCatalog         = tableCatalog ?? throw new ArgumentNullException(nameof(tableCatalog));
		LookupParser         = lookupParser ?? throw new ArgumentNullException(nameof(lookupParser));
	}

	public ISqlValueConverter          Converter            { get; }
	public IColumnValueGenerator       ValueGenerator       { get; }
	public IPatternValueGenerator      PatternGenerator     { get; }
	public IReadOnlyList<RegexProfile> RegexProfiles        { get; }
	public SavedSettingsLibrary        SavedSettings        { get; }
	public ISavedSettingsWindowService SavedSettingsWindows { get; }
	public ITableCatalog               TableCatalog         { get; }
	public ILookupExpressionParser     LookupParser         { get; }
}