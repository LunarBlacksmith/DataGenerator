using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Windows.Markup;
using DataGenerator.Models;
using Microsoft.Data.SqlClient;

namespace DataGenerator.Services;

public sealed class ExceptionFormatter : IExceptionFormatter
{
	private const string APPLICATION_NAMESPACE = "DataGenerator";

	private static readonly string[] FRAMEWORK_NAMESPACES = ["System", "Microsoft", "MS"];

	public ErrorReport Format(Exception exception)
	{
		ArgumentNullException.ThrowIfNull(exception);

		Exception       primary   = Unwrap(exception);
		List<Exception> chain     = GetChain(primary);
		List<string>    locations = [];

		DataGenerationException? generationException = chain.OfType<DataGenerationException>().FirstOrDefault();
		SqlException?            sqlException        = chain.OfType<SqlException>().FirstOrDefault();
		XamlParseException?      xamlException       = chain.OfType<XamlParseException>().FirstOrDefault();

		if (generationException is not null)
		{
			locations.Add($"Data: {generationException.DataLocation}");
		}

		if (sqlException is not null)
		{
			locations.Add(DescribeSqlLocation(sqlException));
		}

		if (xamlException is { LineNumber: > 0 })
		{
			locations.Add($"XAML: {xamlException.BaseUri?.ToString() ?? "markup"}, line {xamlException.LineNumber}, position {xamlException.LinePosition}");
		}

		string? codeLocation  = FindApplicationLocation(chain);
		string? throwLocation = FindThrowLocation(chain);

		if (codeLocation is not null)
		{
			locations.Add($"Code: {codeLocation}");
		}

		// The frame that actually threw is often inside WPF or .NET; it is shown when it differs from this application's frame.
		if (throwLocation is not null && throwLocation != codeLocation)
		{
			locations.Add($"Thrown in: {throwLocation}");
		}

		return new ErrorReport
		{
			Summary  = BuildSummary(primary, chain, sqlException),
			Location = locations.Count == 0 ? "Unknown" : string.Join(Environment.NewLine, locations),
			Details  = exception.ToString()
		};
	}

	private static Exception Unwrap(Exception exception)
	{
		Exception current = exception;

		while (true)
		{
			switch (current)
			{
				case AggregateException { InnerExceptions.Count: 1 } aggregate:
					current = aggregate.InnerExceptions[0];
					continue;

				case TargetInvocationException { InnerException: Exception innerException }:
					current = innerException;
					continue;

				default:
					return current;
			}
		}
	}

	private static List<Exception> GetChain(Exception exception)
	{
		List<Exception> chain   = [];
		Exception?      current = exception;

		while (current is not null && chain.Count < 20)
		{
			chain.Add(current);
			current = current.InnerException;
		}

		return chain;
	}

	private static string BuildSummary(Exception primary, List<Exception> chain, SqlException? sqlException)
	{
		StringBuilder builder = new StringBuilder(primary.Message);

		foreach (Exception inner in chain.Skip(1))
		{
			if (!builder.ToString().Contains(inner.Message, StringComparison.Ordinal))
			{
				_ = builder.Append(Environment.NewLine).Append("Caused by: ").Append(inner.Message);
			}
		}

		if (sqlException is not null && !builder.ToString().Contains($"SQL Server error {sqlException.Number}", StringComparison.Ordinal))
		{
			_ = builder.Append(Environment.NewLine).Append($"(SQL Server error {sqlException.Number}, severity {sqlException.Class})");
		}

		return builder.ToString();
	}

	private static string DescribeSqlLocation(SqlException sqlException)
	{
		StringBuilder builder = new StringBuilder($"SQL Server: error {sqlException.Number}");

		if (!string.IsNullOrWhiteSpace(sqlException.Procedure))
		{
			_ = builder.Append($" in {sqlException.Procedure}");
		}

		if (sqlException.LineNumber > 0)
		{
			_ = builder.Append($", line {sqlException.LineNumber}");
		}

		if (!string.IsNullOrWhiteSpace(sqlException.Server))
		{
			_ = builder.Append($" on {sqlException.Server}");
		}

		return builder.ToString();
	}

	/// <summary>
	/// The deepest frame of this application's code, searching from the innermost exception outwards.
	/// </summary>
	private static string? FindApplicationLocation(List<Exception> chain)
	{
		for (int index = chain.Count - 1; index >= 0; --index)
		{
			StackTrace stackTrace = new StackTrace(chain[index], true);

			foreach (StackFrame frame in stackTrace.GetFrames())
			{
				MethodBase? method = frame.GetMethod();

				if (method?.DeclaringType is Type declaringType && IsApplicationType(declaringType))
				{
					return DescribeFrame(frame, method, declaringType);
				}
			}
		}

		return null;
	}

	/// <summary>
	/// The frame that threw the innermost exception that has a stack trace, skipping throw helpers.
	/// </summary>
	private static string? FindThrowLocation(List<Exception> chain)
	{
		for (int index = chain.Count - 1; index >= 0; --index)
		{
			StackTrace stackTrace = new StackTrace(chain[index], true);

			foreach (StackFrame frame in stackTrace.GetFrames())
			{
				MethodBase? method = frame.GetMethod();

				if (method?.DeclaringType is Type declaringType && !IsThrowHelper(method, declaringType))
				{
					return DescribeFrame(frame, method, declaringType);
				}
			}
		}

		return null;
	}

	private static bool IsApplicationType(Type type)
		=> type.Namespace?.StartsWith(APPLICATION_NAMESPACE, StringComparison.Ordinal) == true;

	private static bool IsFrameworkType(Type type)
	{
		string typeNamespace = type.Namespace ?? string.Empty;

		return FRAMEWORK_NAMESPACES.Any(
			frameworkNamespace => typeNamespace == frameworkNamespace
				|| typeNamespace.StartsWith(frameworkNamespace + ".", StringComparison.Ordinal)
		);
	}

	/// <summary>
	/// Helpers such as ThrowHelper.ThrowArgumentException or ArgumentNullException.ThrowIfNull only raise the exception; the caller is the useful frame.
	/// </summary>
	private static bool IsThrowHelper(MethodBase method, Type declaringType)
		=> declaringType.Name.Contains("ThrowHelper", StringComparison.Ordinal)
			|| (IsFrameworkType(declaringType) && method.Name.StartsWith("Throw", StringComparison.Ordinal));

	/// <summary>
	/// Type and method of a frame (namespace-qualified outside this application), plus the source file and line when known.
	/// </summary>
	private static string DescribeFrame(StackFrame frame, MethodBase method, Type declaringType)
	{
		string typeName = GetFriendlyTypeName(declaringType);

		if (!IsApplicationType(declaringType) && !string.IsNullOrEmpty(declaringType.Namespace))
		{
			typeName = $"{declaringType.Namespace}.{typeName}";
		}

		string  location = $"{typeName}.{GetFriendlyMethodName(method, declaringType)}";
		string? file     = frame.GetFileName();

		return string.IsNullOrEmpty(file)
			? location
			: $"{location} in {Path.GetFileName(file)}, line {frame.GetFileLineNumber()}";
	}

	private static string GetFriendlyTypeName(Type type)
	{
		Type current = type;

		while (current.Name.StartsWith('<') && current.DeclaringType is not null)
		{
			current = current.DeclaringType;
		}

		int arityIndex = current.Name.IndexOf('`');

		return arityIndex > 0 ? current.Name[..arityIndex] : current.Name;
	}

	private static string GetFriendlyMethodName(MethodBase method, Type declaringType)
	{
		string name = method.Name;

		if (name == "MoveNext" && declaringType.Name.StartsWith('<'))
		{
			name = declaringType.Name;
		}

		int start = name.IndexOf('<');
		int end   = name.IndexOf('>');

		return start >= 0 && end > start + 1 ? name[(start + 1)..end] : name;
	}
}