using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Windows.Markup;
using DataGenerator.Interfaces;
using DataGenerator.Models;
using Microsoft.Data.SqlClient;

namespace DataGenerator.Services;

public sealed class ExceptionFormatter : IExceptionFormatter
{
	#region FIELDS
	#region PRIVATE
	private const string APPLICATION_NAMESPACE = "DataGenerator";

	private static readonly string[] FRAMEWORK_NAMESPACES;
	#endregion PRIVATE
	#endregion FIELDS

	#region CONSTRUCTORS
	#region STATIC
	/// <summary>
	///	Sets the default values of the static fields and properties of <see cref="ExceptionFormatter"/>.
	/// </summary>
	static ExceptionFormatter()
	{
		FRAMEWORK_NAMESPACES = ["System", "Microsoft", "MS"];
	}
	#endregion STATIC

	#region PUBLIC
	/// <summary>
	///	Creates a new <see cref="ExceptionFormatter"/>.
	/// </summary>
	public ExceptionFormatter()
	{
	}
	#endregion PUBLIC
	#endregion CONSTRUCTORS

	#region METHODS
	#region PUBLIC
	/// <summary>
	///	Builds a user-facing error report from an exception and its inner exceptions.
	/// </summary>
	/// <param name="exception">
	///	The exception to format.
	/// </param>
	/// <returns>
	///	An error report containing a summary, location hints and full details.
	/// </returns>
	/// <exception cref="ArgumentNullException">
	///	Thrown when <paramref name="exception"/> is <see langword="null"/>.
	/// </exception>
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
	#endregion PUBLIC

	#region PRIVATE
	/// <summary>
	///	Removes single-exception wrapper exceptions to reveal the primary failure.
	/// </summary>
	/// <param name="exception">
	///	The exception to unwrap.
	/// </param>
	/// <returns>
	///	The innermost non-wrapper exception.
	/// </returns>
	private static Exception Unwrap(Exception exception)
	{
		Exception current = exception;

		while (true)
		{
			switch (current)
			{
				case AggregateException { InnerExceptions.Count: 1 } aggregate:
				{
					current = aggregate.InnerExceptions[0];
					continue;
				}

				case TargetInvocationException { InnerException: Exception innerException }:
				{
					current = innerException;
					continue;
				}

				default:
				{
					return current;
				}
			}
		}
	}

	/// <summary>
	///	Builds a bounded list of an exception and its inner exception chain.
	/// </summary>
	/// <param name="exception">
	///	The first exception in the chain.
	/// </param>
	/// <returns>
	///	The exception chain, limited to twenty entries.
	/// </returns>
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

	/// <summary>
	///	Builds the summary text from the primary exception, unique inner messages and SQL Server metadata.
	/// </summary>
	/// <param name="primary">
	///	The primary exception whose message starts the summary.
	/// </param>
	/// <param name="chain">
	///	The exception chain used to append cause messages.
	/// </param>
	/// <param name="sqlException">
	///	The SQL Server exception in the chain, or <see langword="null"/> when none was found.
	/// </param>
	/// <returns>
	///	The combined summary text.
	/// </returns>
	private static string BuildSummary(Exception primary, List<Exception> chain, SqlException? sqlException)
	{
		StringBuilder builder = new(primary.Message);

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

	/// <summary>
	///	Formats SQL Server error number, procedure, line and server information.
	/// </summary>
	/// <param name="sqlException">
	///	The SQL Server exception to describe.
	/// </param>
	/// <returns>
	///	A location string for the SQL Server failure.
	/// </returns>
	private static string DescribeSqlLocation(SqlException sqlException)
	{
		StringBuilder builder = new($"SQL Server: error {sqlException.Number}");

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
	///	Finds the deepest stack frame that belongs to this application.
	/// </summary>
	/// <param name="chain">
	///	The exception chain to search from innermost to outermost.
	/// </param>
	/// <returns>
	///	The formatted application stack frame, or <see langword="null"/> when none is available.
	/// </returns>
	private static string? FindApplicationLocation(List<Exception> chain)
	{
		for (int index = chain.Count - 1; index >= 0; --index)
		{
			StackTrace stackTrace = new(chain[index], true);

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
	///	Finds the stack frame that directly threw the innermost traced exception, skipping throw helpers.
	/// </summary>
	/// <param name="chain">
	///	The exception chain to search from innermost to outermost.
	/// </param>
	/// <returns>
	///	The formatted throwing stack frame, or <see langword="null"/> when no stack trace is available.
	/// </returns>
	private static string? FindThrowLocation(List<Exception> chain)
	{
		for (int index = chain.Count - 1; index >= 0; --index)
		{
			StackTrace stackTrace = new(chain[index], true);

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

	/// <summary>
	///	Checks whether a type belongs to the DataGenerator namespace.
	/// </summary>
	/// <param name="type">
	///	The type to check.
	/// </param>
	/// <returns>
	///	<see langword="true"/> when the type namespace starts with DataGenerator; otherwise <see langword="false"/>.
	/// </returns>
	private static bool IsApplicationType(Type type)
		=> type.Namespace?.StartsWith(APPLICATION_NAMESPACE, StringComparison.Ordinal) == true;

	/// <summary>
	///	Checks whether a type belongs to a framework namespace whose throw helpers should be skipped.
	/// </summary>
	/// <param name="type">
	///	The type to check.
	/// </param>
	/// <returns>
	///	<see langword="true"/> when the type is in a known framework namespace; otherwise <see langword="false"/>.
	/// </returns>
	private static bool IsFrameworkType(Type type)
	{
		string typeNamespace = type.Namespace ?? string.Empty;

		return
			FRAMEWORK_NAMESPACES
				.Any(
					frameworkNamespace => typeNamespace == frameworkNamespace
						|| typeNamespace.StartsWith(frameworkNamespace + ".", StringComparison.Ordinal)
				);
	}

	/// <summary>
	///	Checks whether a frame is a generic throw helper rather than the useful caller frame.
	/// </summary>
	/// <param name="method">
	///	The method represented by the stack frame.
	/// </param>
	/// <param name="declaringType">
	///	The type that declares <paramref name="method"/>.
	/// </param>
	/// <returns>
	///	<see langword="true"/> when the frame should be skipped as a throw helper; otherwise <see langword="false"/>.
	/// </returns>
	private static bool IsThrowHelper(MethodBase method, Type declaringType)
		=> declaringType.Name.Contains("ThrowHelper", StringComparison.Ordinal)
			|| (IsFrameworkType(declaringType) && method.Name.StartsWith("Throw", StringComparison.Ordinal));

	/// <summary>
	///	Formats the type, method and optional source line for a stack frame.
	/// </summary>
	/// <param name="frame">
	///	The stack frame to describe.
	/// </param>
	/// <param name="method">
	///	The method represented by the frame.
	/// </param>
	/// <param name="declaringType">
	///	The type that declares <paramref name="method"/>.
	/// </param>
	/// <returns>
	///	The formatted frame location, including file name and line number when available.
	/// </returns>
	private static string DescribeFrame(StackFrame frame, MethodBase method, Type declaringType)
	{
		string typeName = GetFriendlyTypeName(declaringType);

		if (!IsApplicationType(declaringType) && !string.IsNullOrEmpty(declaringType.Namespace))
		{
			typeName = $"{declaringType.Namespace}.{typeName}";
		}

		string  location = $"{typeName}.{GetFriendlyMethodName(method, declaringType)}";
		string? file     = frame.GetFileName();

		return
			string.IsNullOrEmpty(file)
				? location
				: $"{location} in {Path.GetFileName(file)}, line {frame.GetFileLineNumber()}";
	}

	/// <summary>
	///	Gets a readable type name, unwrapping compiler-generated async and iterator types.
	/// </summary>
	/// <param name="type">
	///	The type to format.
	/// </param>
	/// <returns>
	///	The type name without generic arity suffixes.
	/// </returns>
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

	/// <summary>
	///	Gets a readable method name, extracting the original member from compiler-generated names when possible.
	/// </summary>
	/// <param name="method">
	///	The method to format.
	/// </param>
	/// <param name="declaringType">
	///	The type that declares <paramref name="method"/>.
	/// </param>
	/// <returns>
	///	The friendly method name.
	/// </returns>
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
	#endregion PRIVATE
	#endregion METHODS
}