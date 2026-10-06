namespace DataGenerator.Interfaces;

public interface IRegexValueGenerator
{
	string Generate(string pattern, int maximumLength);
}