using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using DataGenerator.Infrastructure;
using DataGenerator.Interfaces;
using DataGenerator.Models;
using DataGenerator.Services;
using Microsoft.Data.SqlClient;

namespace DataGenerator.ViewModels;

public sealed class MainViewModel : ObservableObject
{
	private const string READY_STATUS            = "Ready.";
	private const string FAILED_STATUS           = "Operation failed. See the error panel for what happened and where.";
	private const string CANCELLED_STATUS        = "Generation cancelled.";
	private const string MASTER_DATABASE         = "master";
	private const string APPLICATION_NAME        = "DataGenerator";
	private const int    CONNECT_TIMEOUT_SECONDS = 15;
	private const int    MAXIMUM_LISTED_ITEMS    = 12;

	private readonly ISqlMetadataService      _metadataService;
	private readonly IDataGenerationService   _generationService;
	private readonly IFileDialogService       _fileDialogService;
	private readonly IDialogService           _dialogService;
	private readonly IShellService            _shellService;
	private readonly IClipboardService        _clipboardService;
	private readonly IHelpService             _helpService;
	private readonly IExceptionFormatter      _exceptionFormatter;
	private readonly IForeignTableKeyResolver _foreignTableKeyResolver;
	private readonly ITableCatalog            _tableCatalog;

	private CancellationTokenSource? _cancellationTokenSource;
	private string                   _serverName;
	private string                   _userName;
	private string                   _password;
	private bool                     _encryptConnection;
	private bool                     _trustServerCertificate;
	private string                   _outputFilePath;
	private bool                     _usesDefaultOutputPath;
	private string?                  _lastGeneratedFilePath;
	private GenerationMode           _generationMode;
	private bool                     _confirmTestDatabase;
	private bool                     _clearExistingData;
	private DataCleanupScope         _cleanupScope;
	private bool                     _resetIdentitySeeds;
	private bool                     _isBusy;
	private string                   _statusMessage;
	private ErrorReport?             _error;

	#region PROPERTIES
	#region PUBLIC
	public DatabaseExplorerViewModel  Explorer       { get; }
	public ThemeViewModel             Theme          { get; }
	public PostGenerationSqlViewModel PostGeneration { get; }

	public AsyncRelayCommand LoadMetadataCommand     { get; }
	public RelayCommand      BrowseOutputCommand     { get; }
	public RelayCommand      OpenOutputFolderCommand { get; }
	public AsyncRelayCommand GenerateCommand         { get; }
	public RelayCommand      CancelCommand           { get; }
	public RelayCommand      ShowPatternHelpCommand  { get; }
	public RelayCommand      CopyErrorCommand        { get; }
	public RelayCommand      DismissErrorCommand     { get; }

	public string           ServerName
	{
		get => _serverName;
		set
		{
			if (SetProperty(ref _serverName, value ?? string.Empty))
			{
				RefreshCommands();
			}
		}
	}
	public string           UserName
	{
		get => _userName;
		set
		{
			if (SetProperty(ref _userName, value ?? string.Empty))
			{
				RefreshCommands();
			}
		}
	}
	public string           Password
	{
		get => _password;
		set
		{
			if (SetProperty(ref _password, value ?? string.Empty))
			{
				RefreshCommands();
			}
		}
	}
	public bool             EncryptConnection
	{
		get => _encryptConnection;
		set => SetProperty(ref _encryptConnection, value);
	}
	public bool             TrustServerCertificate
	{
		get => _trustServerCertificate;
		set => SetProperty(ref _trustServerCertificate, value);
	}
	public GenerationMode   GenerationMode
	{
		get => _generationMode;
		set
		{
			if (SetProperty(ref _generationMode, value))
			{
				OnPropertyChanged(nameof(IsSqlFileMode));
				OnPropertyChanged(nameof(IsDirectInsertMode));
				RefreshCommands();
			}
		}
	}
	public bool             IsSqlFileMode
	{
		get => _generationMode == GenerationMode.SqlFile;
		set
		{
			if (value)
			{
				GenerationMode = GenerationMode.SqlFile;
			}
		}
	}
	public bool             IsDirectInsertMode
	{
		get => _generationMode == GenerationMode.DirectInsert;
		set
		{
			if (value)
			{
				GenerationMode = GenerationMode.DirectInsert;
			}
		}
	}
	public string           OutputFilePath
	{
		get => _outputFilePath;
		set
		{
			if (SetProperty(ref _outputFilePath, value ?? string.Empty))
			{
				_usesDefaultOutputPath = false;
				RefreshCommands();
			}
		}
	}
	public bool             ConfirmTestDatabase
	{
		get => _confirmTestDatabase;
		set
		{
			if (SetProperty(ref _confirmTestDatabase, value))
			{
				RefreshCommands();
			}
		}
	}
	public bool             ClearExistingData
	{
		get => _clearExistingData;
		set => SetProperty(ref _clearExistingData, value);
	}
	public DataCleanupScope CleanupScope
	{
		get => _cleanupScope;
		set
		{
			if (SetProperty(ref _cleanupScope, value))
			{
				OnPropertyChanged(nameof(ClearIncludedTablesOnly));
				OnPropertyChanged(nameof(ClearAllTablesInDatabases));
			}
		}
	}
	public bool             ClearIncludedTablesOnly
	{
		get => _cleanupScope == DataCleanupScope.IncludedTables;
		set
		{
			if (value)
			{
				CleanupScope = DataCleanupScope.IncludedTables;
			}
		}
	}
	public bool             ClearAllTablesInDatabases
	{
		get => _cleanupScope == DataCleanupScope.AllTablesInDatabases;
		set
		{
			if (value)
			{
				CleanupScope = DataCleanupScope.AllTablesInDatabases;
			}
		}
	}
	public bool             ResetIdentitySeeds
	{
		get => _resetIdentitySeeds;
		set => SetProperty(ref _resetIdentitySeeds, value);
	}
	public bool             IsBusy
	{
		get => _isBusy;
		private set
		{
			if (SetProperty(ref _isBusy, value))
			{
				OnPropertyChanged(nameof(IsIdle));
				RefreshCommands();
			}
		}
	}
	public string           StatusMessage
	{
		get         => _statusMessage;
		private set => SetProperty(ref _statusMessage, value);
	}

	public bool   IsIdle        => !_isBusy;
	public bool   HasError      => _error is not null;
	public string ErrorSummary  => _error?.Summary ?? string.Empty;
	public string ErrorLocation => _error?.Location ?? string.Empty;
	public string ErrorDetails  => _error?.Details ?? string.Empty;

	public string GenerationSummary
		=> Explorer.IncludedTableCount == 0
			? "Include at least one table in the database explorer."
			: $"{Explorer.IncludedRowCount:N0} rows for {Explorer.IncludedTableCount:N0} table(s)";
	#endregion PUBLIC
	#endregion PROPERTIES

	public MainViewModel(
		ISqlMetadataService        metadataService,
		IDataGenerationService     generationService,
		IFileDialogService         fileDialogService,
		IDialogService             dialogService,
		IShellService              shellService,
		IClipboardService          clipboardService,
		IHelpService               helpService,
		IExceptionFormatter        exceptionFormatter,
		IForeignTableKeyResolver   foreignTableKeyResolver,
		ITableCatalog              tableCatalog,
		DatabaseExplorerViewModel  explorer,
		ThemeViewModel             theme,
		PostGenerationSqlViewModel postGeneration
	)
	{
		_cancellationTokenSource = null;
		_serverName              = string.Empty;
		_userName                = string.Empty;
		_password                = string.Empty;
		_encryptConnection       = true;
		_trustServerCertificate  = true;
		_outputFilePath          = CreateDefaultOutputFilePath();
		_usesDefaultOutputPath   = true;
		_lastGeneratedFilePath   = null;
		_generationMode          = GenerationMode.SqlFile;
		_confirmTestDatabase     = false;
		_clearExistingData       = false;
		_cleanupScope            = DataCleanupScope.IncludedTables;
		_resetIdentitySeeds      = true;
		_isBusy                  = false;
		_statusMessage           = READY_STATUS;
		_error                   = null;

		_metadataService         = metadataService ?? throw new ArgumentNullException(nameof(metadataService));
		_generationService       = generationService ?? throw new ArgumentNullException(nameof(generationService));
		_fileDialogService       = fileDialogService ?? throw new ArgumentNullException(nameof(fileDialogService));
		_dialogService           = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
		_shellService            = shellService ?? throw new ArgumentNullException(nameof(shellService));
		_clipboardService        = clipboardService ?? throw new ArgumentNullException(nameof(clipboardService));
		_helpService             = helpService ?? throw new ArgumentNullException(nameof(helpService));
		_exceptionFormatter      = exceptionFormatter ?? throw new ArgumentNullException(nameof(exceptionFormatter));
		_foreignTableKeyResolver = foreignTableKeyResolver ?? throw new ArgumentNullException(nameof(foreignTableKeyResolver));
		_tableCatalog            = tableCatalog ?? throw new ArgumentNullException(nameof(tableCatalog));
		Explorer                 = explorer ?? throw new ArgumentNullException(nameof(explorer));
		Theme                    = theme ?? throw new ArgumentNullException(nameof(theme));
		PostGeneration           = postGeneration ?? throw new ArgumentNullException(nameof(postGeneration));

		LoadMetadataCommand     = new AsyncRelayCommand(LoadMetadataAsync, CanLoadMetadata);
		BrowseOutputCommand     = new RelayCommand(BrowseOutputFile, _ => !IsBusy && IsSqlFileMode);
		OpenOutputFolderCommand = new RelayCommand(OpenOutputFolder);
		GenerateCommand         = new AsyncRelayCommand(GenerateAsync, CanGenerate);
		CancelCommand           = new RelayCommand(Cancel, _ => IsBusy);
		ShowPatternHelpCommand  = new RelayCommand(parameter => _helpService.ShowPatternLanguageHelp(parameter as string));
		CopyErrorCommand        = new RelayCommand(CopyError, _ => HasError);
		DismissErrorCommand     = new RelayCommand(_ => ClearError(), _ => HasError);

		Explorer.GenerationSettingsChanged += OnGenerationSettingsChanged;
		PostGeneration.Changed             += OnPostGenerationChanged;
	}

	/// <summary>
	/// Shows an exception in the error panel with what happened, where it happened and the full details.
	/// </summary>
	public void ReportError(Exception exception)
	{
		ArgumentNullException.ThrowIfNull(exception);

		ShowError(_exceptionFormatter.Format(exception));
	}

	private bool CanLoadMetadata(object? parameter) => !IsBusy && HasCompleteConnectionDetails();

	private async Task LoadMetadataAsync(object? parameter)
	{
		bool discardsSettings = Explorer.IncludedTableCount > 0;

		if (	discardsSettings
				&& !_dialogService.Confirm(
						"Reload metadata",
						"Reloading the metadata clears the included tables, row sets and column rules. Continue?"
					)
		)
		{
			return;
		}

		await RunBusyOperationAsync(
			async cancellationToken =>
			{
				StatusMessage = "Connecting to SQL Server and loading metadata…";

				string           connectionString = BuildConnectionString(MASTER_DATABASE);
				Progress<string> progress         = new(message => StatusMessage = message);

				IReadOnlyList<DatabaseModel> databases = await _metadataService.LoadMetadataAsync(
					connectionString,
					progress,
					cancellationToken
				);

				int inferredKeyCount = _foreignTableKeyResolver.ResolveInferredKeys(databases);

				// Filled before the explorer creates the column rules, so "Value from table" settings can be resolved.
				_tableCatalog.SetTables(databases.SelectMany(database => database.Tables));
				Explorer.Load(databases);
				PostGeneration.SetDatabases(databases.Select(database => database.Name));

				StatusMessage = $"Loaded {Explorer.TotalTableCount:N0} table(s) from {databases.Count:N0} database(s)"
					+ (inferredKeyCount > 0
						? $" and linked {inferredKeyCount:N0} FTK column(s) to the tables they refer to."
						: ".");
			}
		);
	}

	private void BrowseOutputFile(object? parameter)
	{
		string? selectedPath = _fileDialogService.SelectSqlOutputFile();

		if (!string.IsNullOrWhiteSpace(selectedPath))
		{
			OutputFilePath = selectedPath;
		}
	}

	private void OpenOutputFolder(object? parameter)
	{
		try
		{
			string  outputFilePath = _outputFilePath.Trim();
			string? fileToSelect   = File.Exists(outputFilePath) ? outputFilePath : _lastGeneratedFilePath;

			_shellService.OpenFolder(GetOutputFolder(), File.Exists(fileToSelect) ? fileToSelect : null);
		}
		catch (Exception exception) when (	exception is IOException
														or UnauthorizedAccessException
														or ArgumentException
														or NotSupportedException
														or Win32Exception
		)
		{
			ReportError(exception);
		}
	}

	private bool CanGenerate(object? parameter)
	{
		bool outputIsConfigured =
			IsSqlFileMode
				? !string.IsNullOrWhiteSpace(OutputFilePath)
				: ConfirmTestDatabase && HasCompleteConnectionDetails();

		return	!IsBusy
					&& Explorer.IncludedTableCount > 0
					&& outputIsConfigured
					&& !PostGeneration.HasProblem;
	}

	private async Task GenerateAsync(object? parameter)
	{
		ClearError();

		IReadOnlyList<TableNodeViewModel>? includedTables = PrepareIncludedTables();

		if (includedTables is null)
		{
			return;
		}

		await RunBusyOperationAsync(
			async cancellationToken =>
			{
				GenerationRequest request  = CreateRequest(includedTables);
				Progress<string>  progress = new(message => StatusMessage = message);
				long              rowCount = request.Plans.Sum(plan => (long)plan.TotalRowCount);

				await _generationService.GenerateAsync(
					request,
					progress,
					cancellationToken
				);

				if (request.Mode == GenerationMode.DirectInsert)
				{
					StatusMessage = $"Inserted {rowCount:N0} row(s) into {request.Plans.Count:N0} table(s) and committed the transaction.";
					return;
				}

				_lastGeneratedFilePath = Path.GetFullPath(request.OutputFilePath!);
				StatusMessage          = $"Wrote {rowCount:N0} row(s) for {request.Plans.Count:N0} table(s) to "
					+ $"{Path.GetFileName(_lastGeneratedFilePath)}. Click 'Open output folder' to find it.";

				if (_usesDefaultOutputPath)
				{
					SetDefaultOutputFilePath();
				}
			}
		);
	}

	/// <summary>
	/// Resolves keys taken from tables that are not included, validates every rule and confirms data cleanup.
	/// Returns the tables to generate, or <see langword="null"/> when generation should not start.
	/// </summary>
	private IReadOnlyList<TableNodeViewModel>? PrepareIncludedTables()
	{
		IReadOnlyList<TableNodeViewModel> includedTables = Explorer.GetIncludedTables();

		if (includedTables.Count == 0)
		{
			StatusMessage = "Include at least one table in the database explorer.";
			return null;
		}

		IReadOnlyList<MissingReference> missingReferences = GenerationPreflight.FindMissingReferences(Explorer, includedTables);

		if (missingReferences.Count > 0)
		{
			DialogChoice choice = _dialogService.AskYesNoCancel(
				"Generate data for referenced tables?",
				BuildMissingReferenceMessage(missingReferences)
			);

			switch (choice)
			{
				case DialogChoice.Yes:
				{
					Explorer.IncludeTables(missingReferences.Select(reference => reference.ReferencedTable).Distinct());
					includedTables = Explorer.GetIncludedTables();
					break;
				}

				case DialogChoice.No:
				{
					UseExistingKeys(missingReferences, includedTables);
					break;
				}

				default:
				{
					StatusMessage = CANCELLED_STATUS;
					return null;
				}
			}
		}

		IReadOnlyList<RuleProblem> problems = GenerationPreflight.FindRuleProblems(includedTables);

		if (problems.Count > 0)
		{
			ShowRuleProblems(problems);
			return null;
		}

		if (IsDirectInsertMode && ClearExistingData && !ConfirmCleanup(includedTables))
		{
			StatusMessage = CANCELLED_STATUS;
			return null;
		}

		return includedTables;
	}

	private static void UseExistingKeys(IReadOnlyList<MissingReference> missingReferences, IReadOnlyList<TableNodeViewModel> includedTables)
	{
		HashSet<TableNodeViewModel> included = [.. includedTables];

		foreach (MissingReference reference in missingReferences.Where(reference => included.Contains(reference.ReferencingTable)))
		{
			reference.Rule.GenerationMode = ValueGenerationMode.ExistingForeignKey;
		}
	}

	private static string BuildMissingReferenceMessage(IReadOnlyList<MissingReference> missingReferences)
	{
		StringBuilder builder = new("Some columns take their values from rows generated for tables that are not included:");
		List<IGrouping<TableNodeViewModel, MissingReference>> groups = [.. missingReferences.GroupBy(reference => reference.ReferencedTable)];

		_ = builder.AppendLine();

		foreach (IGrouping<TableNodeViewModel, MissingReference> group in groups.Take(MAXIMUM_LISTED_ITEMS))
		{
			string columns = string.Join(
				", ",
				group.Select(reference => $"{reference.ReferencingTable.Model.Name}.{reference.Rule.Name}{(reference.Rule.Reference!.IsInferred ? " (FTK)" : string.Empty)}")
					.Distinct(StringComparer.OrdinalIgnoreCase)
			);

			_ = builder.AppendLine()
				.Append($"•  [{group.Key.Database.DisplayName}] {group.Key.DisplayName}  ←  {columns}");
		}

		if (groups.Count > MAXIMUM_LISTED_ITEMS)
		{
			_ = builder.AppendLine().Append($"…and {groups.Count - MAXIMUM_LISTED_ITEMS:N0} more table(s).");
		}

		_ = builder.AppendLine()
			.AppendLine()
			.AppendLine("(FTK) marks columns linked by their name ending in FTK rather than by a SQL Server foreign key.")
			.AppendLine()
			.AppendLine("Yes:  also generate data for these tables (their row counts can be changed in the explorer).")
			.AppendLine("No:  use keys that already exist in these tables instead.")
			.Append("Cancel:  do not generate yet.");

		return builder.ToString();
	}

	private void ShowRuleProblems(IReadOnlyList<RuleProblem> problems)
	{
		StringBuilder summary = new($"{problems.Count:N0} setting(s) must be fixed before generating:");

		foreach (RuleProblem problem in problems.Take(MAXIMUM_LISTED_ITEMS))
		{
			_ = summary.AppendLine().Append($"•  {problem.Location}: {problem.Message}");
		}

		if (problems.Count > MAXIMUM_LISTED_ITEMS)
		{
			_ = summary.AppendLine().Append($"…and {problems.Count - MAXIMUM_LISTED_ITEMS:N0} more (see Details).");
		}

		ShowError(
			new ErrorReport
			{
				Summary  = summary.ToString(),
				Location = problems[0].Location,
				Details  = string.Join(Environment.NewLine, problems.Select(problem => $"{problem.Location}: {problem.Message}"))
			}
		);

		Explorer.Reveal(problems[0].Table, problems[0].RowSet);
		StatusMessage = "Fix the column rules marked in red, then generate again.";
	}

	private bool ConfirmCleanup(IReadOnlyList<TableNodeViewModel> includedTables)
	{
		IReadOnlyList<TableModel> tablesToClear = GetTablesToClear(includedTables);
		string                    databases     = string.Join(
			", ",
			tablesToClear.Select(table => table.DatabaseName).Distinct(StringComparer.OrdinalIgnoreCase)
		);

		return _dialogService.Confirm(
			"Delete existing data?",
			$"All existing rows will be deleted from {tablesToClear.Count:N0} table(s) in {databases} before the new data is inserted."
				+ $"{Environment.NewLine}{Environment.NewLine}"
				+ "The deletes run in the same transaction as the inserts, so nothing is deleted if generation fails. Continue?"
		);
	}

	private GenerationRequest CreateRequest(IReadOnlyList<TableNodeViewModel> includedTables)
		=> new GenerationRequest
		{
			Plans              = [.. includedTables.Select(table => table.CreatePlan())],
			Mode               = _generationMode,
			OutputFilePath     = IsSqlFileMode ? _outputFilePath.Trim() : null,
			ConnectionString   = IsDirectInsertMode ? BuildConnectionString(MASTER_DATABASE) : null,
			TablesToClear      = _clearExistingData ? GetTablesToClear(includedTables) : [],
			ResetIdentitySeeds = _clearExistingData && _resetIdentitySeeds,
			PostGeneration     = PostGeneration.CreateScript()
		};

	private IReadOnlyList<TableModel> GetTablesToClear(IReadOnlyList<TableNodeViewModel> includedTables)
		=> _cleanupScope == DataCleanupScope.AllTablesInDatabases
			? [.. includedTables.Select(table => table.Database).Distinct().SelectMany(database => database.Tables).Select(table => table.Model)]
			: [.. includedTables.Select(table => table.Model)];

	private bool HasCompleteConnectionDetails()
		=>	!string.IsNullOrWhiteSpace(ServerName)
			&& !string.IsNullOrWhiteSpace(UserName)
			&& !string.IsNullOrWhiteSpace(Password);

	private string BuildConnectionString(string databaseName)
	{
		SqlConnectionStringBuilder builder = new()
		{
			DataSource               = ServerName.Trim(),
			InitialCatalog           = databaseName,
			UserID                   = UserName,
			Password                 = Password,
			IntegratedSecurity       = false,
			Encrypt                  = EncryptConnection,
			TrustServerCertificate   = TrustServerCertificate,
			PersistSecurityInfo      = false,
			MultipleActiveResultSets = false,
			ConnectTimeout           = CONNECT_TIMEOUT_SECONDS,
			ApplicationName          = APPLICATION_NAME
		};

		return builder.ConnectionString;
	}

	private string GetOutputFolder()
	{
		string outputFilePath = _outputFilePath.Trim();

		if (outputFilePath.Length > 0)
		{
			try
			{
				string? directory = Path.GetDirectoryName(Path.GetFullPath(outputFilePath));

				if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory))
				{
					return directory;
				}
			}
			catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
			{
				// An unusable path falls back to the default output folder.
			}
		}

		return SecurePathService.GetGeneratedDataDirectory();
	}

	private void SetDefaultOutputFilePath()
	{
		_outputFilePath        = CreateDefaultOutputFilePath();
		_usesDefaultOutputPath = true;
		OnPropertyChanged(nameof(OutputFilePath));
	}

	private static string CreateDefaultOutputFilePath()
		=> Path.Combine(SecurePathService.GetGeneratedDataDirectory(), SecurePathService.CreateDefaultSqlFileName());

	private void Cancel(object? parameter)
	{
		_cancellationTokenSource?.Cancel();
		StatusMessage = "Cancellation requested…";
	}

	private async Task RunBusyOperationAsync(Func<CancellationToken, Task> operation)
	{
		ArgumentNullException.ThrowIfNull(operation);

		ClearError();
		IsBusy                   = true;
		_cancellationTokenSource = new CancellationTokenSource();

		try
		{
			await operation(_cancellationTokenSource.Token);
		}
		catch (OperationCanceledException)
		{
			StatusMessage = "Operation cancelled.";
		}
		catch (Exception exception)
		{
			ReportError(exception);
			StatusMessage = FAILED_STATUS;
		}
		finally
		{
			_cancellationTokenSource.Dispose();
			_cancellationTokenSource = null;
			IsBusy                   = false;
		}
	}

	private void ShowError(ErrorReport report)
	{
		_error = report;
		OnErrorChanged();
	}

	private void ClearError()
	{
		if (_error is null)
		{
			return;
		}

		_error = null;
		OnErrorChanged();
	}

	private void OnErrorChanged()
	{
		OnPropertyChanged(nameof(HasError));
		OnPropertyChanged(nameof(ErrorSummary));
		OnPropertyChanged(nameof(ErrorLocation));
		OnPropertyChanged(nameof(ErrorDetails));
		CopyErrorCommand.NotifyCanExecuteChanged();
		DismissErrorCommand.NotifyCanExecuteChanged();
	}

	private void CopyError(object? parameter)
	{
		if (_error is null)
		{
			return;
		}

		string text = $"{_error.Summary}{Environment.NewLine}{Environment.NewLine}Where:{Environment.NewLine}{_error.Location}"
			+ $"{Environment.NewLine}{Environment.NewLine}Details:{Environment.NewLine}{_error.Details}";

		try
		{
			_clipboardService.SetText(text);
			StatusMessage = "Error details copied to the clipboard.";
		}
		catch (ExternalException)
		{
			StatusMessage = "The clipboard is in use by another application. Try copying again.";
		}
	}

	private void OnGenerationSettingsChanged(object? sender, EventArgs e)
	{
		TableNodeViewModel? firstIncludedTable = Explorer.AllTables.FirstOrDefault(table => table.IsIncluded);

		PostGeneration.SuggestDatabase(firstIncludedTable?.Database.Model.Name);
		OnPropertyChanged(nameof(GenerationSummary));
		GenerateCommand.NotifyCanExecuteChanged();
	}

	private void OnPostGenerationChanged(object? sender, EventArgs e) => GenerateCommand.NotifyCanExecuteChanged();

	private void RefreshCommands()
	{
		LoadMetadataCommand.NotifyCanExecuteChanged();
		BrowseOutputCommand.NotifyCanExecuteChanged();
		GenerateCommand.NotifyCanExecuteChanged();
		CancelCommand.NotifyCanExecuteChanged();
	}
}