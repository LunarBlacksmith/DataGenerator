namespace DataGenerator.Services;

public interface IRegexValueGenerator
{
	string Generate(string pattern, int maximumLength);
}