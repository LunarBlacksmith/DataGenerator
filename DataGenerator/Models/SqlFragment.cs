namespace DataGenerator.Models;

/// <summary>
/// A raw T-SQL expression that is resolved by SQL Server when a generated script runs
/// (for example a value captured from an earlier INSERT).
/// </summary>
public sealed record SqlFragment(string Sql);