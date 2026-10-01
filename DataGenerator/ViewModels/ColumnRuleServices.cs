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
		IReadOnlyList<RegexProfile> regexProfiles
	)
	{
		Converter        = converter ?? throw new ArgumentNullException(nameof(converter));
		ValueGenerator   = valueGenerator ?? throw new ArgumentNullException(nameof(valueGenerator));
		PatternGenerator = patternGenerator ?? throw new ArgumentNullException(nameof(patternGenerator));
		RegexProfiles    = regexProfiles ?? throw new ArgumentNullException(nameof(regexProfiles));
	}

	public ISqlValueConverter          Converter        { get; }
	public IColumnValueGenerator       ValueGenerator   { get; }
	public IPatternValueGenerator      PatternGenerator { get; }
	public IReadOnlyList<RegexProfile> RegexProfiles    { get; }
}