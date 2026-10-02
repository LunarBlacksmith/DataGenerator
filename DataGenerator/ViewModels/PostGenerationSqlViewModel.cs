using DataGenerator.Infrastructure;
using DataGenerator.Models;
using DataGenerator.Services;

namespace DataGenerator.ViewModels;

/// <summary>
/// The stored procedures or SQL that run at the end of a generation, inside its transaction and just before the commit,
/// and the database they run in.
/// </summary>
public sealed class PostGenerationSqlViewModel : ObservableObject
{
	private readonly IPostGenerationSqlParser _parser;

	private string                                 _text;
	private string?                                _databaseName;
	private bool                                   _isDatabaseChosen;
	private IReadOnlyList<string>                  _databaseNames;
	private IReadOnlyList<PostGenerationStatement> _statements;
	private string?                                _parseError;

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

	/// <summary>
	/// Raised whenever the statements, their validity or the database change.
	/// </summary>
	public event EventHandler? Changed;

	/// <summary>
	/// One stored procedure name per line, or SQL that is run as typed.
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
	/// The databases of the loaded metadata.
	/// </summary>
	public IReadOnlyList<string> DatabaseNames
	{
		get         => _databaseNames;
		private set => SetProperty(ref _databaseNames, value);
	}

	/// <summary>
	/// The database the statements run in, so names without a database (e.g. dbo.RebuildTotals) are found there.
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
	/// True when the text cannot be run, or when there are statements but no database to run them in.
	/// </summary>
	public bool HasProblem => _parseError is not null || (HasStatements && string.IsNullOrEmpty(_databaseName));

	/// <summary>
	/// Whether a summary or problem is shown under the text (nothing is shown while the text is empty).
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

			int          procedureCount = _statements.Count(statement => statement.Kind == PostGenerationStatementKind.StoredProcedure);
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

	/// <summary>
	/// Replaces the databases that can be chosen, keeping the chosen one when it still exists.
	/// </summary>
	public void SetDatabases(IEnumerable<string> databaseNames)
	{
		ArgumentNullException.ThrowIfNull(databaseNames);

		List<string> names = [.. databaseNames.Distinct(StringComparer.OrdinalIgnoreCase)];
		string?      kept  = names.FirstOrDefault(name => string.Equals(name, _databaseName, StringComparison.OrdinalIgnoreCase));

		DatabaseNames = names;

		if (kept is null)
		{
			_isDatabaseChosen = false;
		}

		SetDatabaseName(kept ?? names.FirstOrDefault());
	}

	/// <summary>
	/// Follows the database of the included tables until the user chooses a database.
	/// </summary>
	public void SuggestDatabase(string? databaseName)
	{
		if (_isDatabaseChosen || databaseName is null)
		{
			return;
		}

		string? match = _databaseNames.FirstOrDefault(name => string.Equals(name, databaseName, StringComparison.OrdinalIgnoreCase));

		if (match is not null)
		{
			SetDatabaseName(match);
		}
	}

	/// <summary>
	/// The statements to run, or null when there are none. Only call while <see cref="HasProblem"/> is false.
	/// </summary>
	public PostGenerationScript? CreateScript()
		=> HasStatements && !HasProblem
			? new PostGenerationScript
			{
				DatabaseName = _databaseName!,
				Statements   = _statements
			}
			: null;

	private void SetDatabaseName(string? databaseName)
	{
		if (SetProperty(ref _databaseName, databaseName, nameof(DatabaseName)))
		{
			OnStateChanged();
		}
	}

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

	private void OnStateChanged()
	{
		OnPropertyChanged(nameof(HasProblem));
		OnPropertyChanged(nameof(HasSummary));
		OnPropertyChanged(nameof(Summary));
		Changed?.Invoke(this, EventArgs.Empty);
	}
}