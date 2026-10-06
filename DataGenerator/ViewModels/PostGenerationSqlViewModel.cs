using DataGenerator.Infrastructure;
using DataGenerator.Interfaces;
using DataGenerator.Models;

namespace DataGenerator.ViewModels;

/// <summary>
///	The stored procedures or SQL that run at the end of a generation, inside its transaction and just before the commit,
///	and the database they run in.
/// </summary>
public sealed class PostGenerationSqlViewModel : ObservableObject
{
	#region FIELDS
	#region PRIVATE
	private readonly IPostGenerationSqlParser _parser;

	private string                                 _text;
	private string?                                _databaseName;
	private bool                                   _isDatabaseChosen;
	private IReadOnlyList<string>                  _databaseNames;
	private IReadOnlyList<PostGenerationStatement> _statements;
	private string?                                _parseError;
	#endregion PRIVATE
	#endregion FIELDS

	#region PROPERTIES
	#region PUBLIC
	/// <summary>
	///	One stored procedure name per line, or SQL that is run as typed.
	/// </summary>
	public string Text
	{
		get => _text;
		set
		{
			if (SetProperty(ref _text, value ?? string.Empty))
			{
				Parse();
			}
		}
	}

	/// <summary>
	///	The databases of the loaded metadata.
	/// </summary>
	public IReadOnlyList<string> DatabaseNames
	{
		get         => _databaseNames;
		private set => SetProperty(ref _databaseNames, value);
	}

	/// <summary>
	///	The database the statements run in, so names without a database (e.g. dbo.RebuildTotals) are found there.
	/// </summary>
	public string? DatabaseName
	{
		get => _databaseName;
		set
		{
			if (SetProperty(ref _databaseName, value))
			{
				_isDatabaseChosen = value is not null;
				OnStateChanged();
			}
		}
	}

	public bool HasStatements => _statements.Count > 0;

	/// <summary>
	///	True when the text cannot be run, or when there are statements but no database to run them in.
	/// </summary>
	public bool HasProblem => _parseError is not null || (HasStatements && string.IsNullOrEmpty(_databaseName));

	/// <summary>
	///	Whether a summary or problem is shown under the text (nothing is shown while the text is empty).
	/// </summary>
	public bool HasSummary => HasStatements || _parseError is not null;

	public string Summary
	{
		get
		{
			if (_parseError is not null)
			{
				return _parseError;
			}

			if (!HasStatements)
			{
				return string.Empty;
			}

			if (string.IsNullOrEmpty(_databaseName))
			{
				return "Choose the database the post-generation SQL runs in.";
			}

			int          procedureCount =
				_statements
					.Count(statement => statement.Kind == PostGenerationStatementKind.StoredProcedure);
			int          sqlCount       = _statements.Count - procedureCount;
			List<string> parts          = [];

			if (procedureCount > 0)
			{
				parts.Add(procedureCount == 1 ? "1 stored procedure" : $"{procedureCount:N0} stored procedures");
			}

			if (sqlCount > 0)
			{
				parts.Add(sqlCount == 1 ? "1 block of SQL" : $"{sqlCount:N0} blocks of SQL");
			}

			return $"Runs {string.Join(" and ", parts)} in [{_databaseName}] after the inserts, before the commit. "
				+ "If any of it fails, nothing is saved.";
		}
	}
	#endregion PUBLIC
	#endregion PROPERTIES

	#region EVENTS
	#region PUBLIC
	/// <summary>
	///	Raised whenever the statements, their validity or the database change.
	/// </summary>
	public event EventHandler? Changed;
	#endregion PUBLIC
	#endregion EVENTS

	#region CONSTRUCTORS
	#region PUBLIC
	/// <summary>
	///	Creates the post-generation SQL editor with no statements and no chosen database.
	/// </summary>
	/// <param name="parser">
	///	The parser used to split and validate the text.
	/// </param>
	/// <exception cref="ArgumentNullException">
	///	Thrown when <paramref name="parser"/> is <see langword="null"/>.
	/// </exception>
	public PostGenerationSqlViewModel(IPostGenerationSqlParser parser)
	{
		_parser           = parser ?? throw new ArgumentNullException(nameof(parser));
		_text             = string.Empty;
		_databaseName     = null;
		_isDatabaseChosen = false;
		_databaseNames    = [];
		_statements       = [];
		_parseError       = null;
	}
	#endregion PUBLIC
	#endregion CONSTRUCTORS

	#region METHODS
	#region PUBLIC
	/// <summary>
	///	Replaces the databases that can be chosen, keeping the chosen one when it still exists.
	/// </summary>
	/// <param name="databaseNames">
	///	The database names from the loaded metadata.
	/// </param>
	/// <exception cref="ArgumentNullException">
	///	Thrown when <paramref name="databaseNames"/> is <see langword="null"/>.
	/// </exception>
	public void SetDatabases(IEnumerable<string> databaseNames)
	{
		ArgumentNullException.ThrowIfNull(databaseNames);

		List<string> names = [.. databaseNames.Distinct(StringComparer.OrdinalIgnoreCase)];
		string?      kept  =
			names
				.FirstOrDefault(
					name => string.Equals(name, _databaseName, StringComparison.OrdinalIgnoreCase)
				);

		DatabaseNames = names;

		if (kept is null)
		{
			_isDatabaseChosen = false;
		}

		SetDatabaseName(kept ?? names.FirstOrDefault());
	}

	/// <summary>
	///	Follows the database of the included tables until the user chooses a database.
	/// </summary>
	/// <param name="databaseName">
	///	The database name suggested by the current table selection; <see langword="null"/> leaves the choice unchanged.
	/// </param>
	public void SuggestDatabase(string? databaseName)
	{
		if (_isDatabaseChosen || databaseName is null)
		{
			return;
		}

		string? match =
			_databaseNames
				.FirstOrDefault(
					name => string.Equals(name, databaseName, StringComparison.OrdinalIgnoreCase)
				);

		if (match is not null)
		{
			SetDatabaseName(match);
		}
	}

	/// <summary>
	///	Creates the statements to run. Only call while <see cref="HasProblem"/> is <see langword="false"/>.
	/// </summary>
	/// <returns>
	///	The script to run, or <see langword="null"/> when there are no statements or the current state has a problem.
	/// </returns>
	public PostGenerationScript? CreateScript()
		=>
			HasStatements && !HasProblem
				? new PostGenerationScript
				{
					DatabaseName = _databaseName!,
					Statements   = _statements
				}
				: null;
	#endregion PUBLIC

	#region PRIVATE
	/// <summary>
	///	Changes the selected database without marking it as explicitly chosen by the user.
	/// </summary>
	/// <param name="databaseName">
	///	The database name to select, or <see langword="null"/> when none can be selected.
	/// </param>
	private void SetDatabaseName(string? databaseName)
	{
		if (SetProperty(ref _databaseName, databaseName, nameof(DatabaseName)))
		{
			OnStateChanged();
		}
	}

	/// <summary>
	///	Parses the entered text into post-generation statements and refreshes the summary state.
	/// </summary>
	private void Parse()
	{
		if (_parser.TryParse(_text, out IReadOnlyList<PostGenerationStatement> statements, out string? error))
		{
			_statements = statements;
			_parseError = null;
		}
		else
		{
			_statements = [];
			_parseError = error;
		}

		OnPropertyChanged(nameof(HasStatements));
		OnStateChanged();
	}

	/// <summary>
	///	Refreshes the derived problem and summary properties and raises the changed event.
	/// </summary>
	private void OnStateChanged()
	{
		OnPropertyChanged(nameof(HasProblem));
		OnPropertyChanged(nameof(HasSummary));
		OnPropertyChanged(nameof(Summary));
		Changed?.Invoke(this, EventArgs.Empty);
	}
	#endregion PRIVATE
	#endregion METHODS
}