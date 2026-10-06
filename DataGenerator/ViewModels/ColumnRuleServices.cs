using DataGenerator.Interfaces;
using DataGenerator.Models;
using DataGenerator.Services;

namespace DataGenerator.ViewModels;

/// <summary>
/// Services and shared settings used by every <see cref="ColumnRuleViewModel"/>.
/// </summary>
public sealed class ColumnRuleServices
{
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