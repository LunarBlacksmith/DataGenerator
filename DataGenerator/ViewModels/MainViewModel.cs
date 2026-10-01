using DataGenerator.Infrastructure;
using DataGenerator.Models;
using DataGenerator.Services;
using Microsoft.Data.SqlClient;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;

namespace DataGenerator.ViewModels;

public sealed class MainViewModel : ObservableObject
{
	private readonly ISqlMetadataService    _metadataService;
	private readonly IDataGenerationService _generationService;
	private readonly IFileDialogService     _fileDialogService;

	private CancellationTokenSource? _cancellationTokenSource;
	private string                   _serverName;
	private string                   _userName;
	private string                   _password;
	private bool                     _encryptConnection;
	private bool                     _trustServerCertificate;
	private DatabaseModel?           _selectedDatabase;
	private TableModel?              _selectedTable;
	private string                   _outputFilePath;
	private GenerationMode           _generationMode;
	private bool                     _confirmTestDatabase;
	private bool                     _isBusy;
	private string                   _statusMessage;
	private string                   _errorMessage;

	#region PROPERTIES
	#region PUBLIC
	public ObservableCollection<DatabaseModel> Databases     { get; }
	public ObservableCollection<RegexProfile>  RegexProfiles { get; }

	public IReadOnlyList<ValueGenerationMode>  ValueGenerationModes { get; }
	public IReadOnlyList<GenerationMode>       GenerationModes      { get; }

	public AsyncRelayCommand LoadMetadataCommand { get; }
	public RelayCommand      BrowseOutputCommand { get; }
	public AsyncRelayCommand GenerateCommand     { get; }
	public RelayCommand      CancelCommand       { get; }

	public string         ServerName
	{
		get => _serverName;
		set
		{
			if (SetProperty(ref _serverName, value))
			{
				RefreshCommands();
			}
		}
	}
	public string         UserName
	{
		get => _userName;
		set
		{
			if (SetProperty(ref _userName, value))
			{
				RefreshCommands();
			}
		}
	}
	public string         Password
	{
		get => _password;
		set
		{
			if (SetProperty(ref _password, value))
			{
				RefreshCommands();
			}
		}
	}
	public bool           EncryptConnection
	{
		get => _encryptConnection;
		set => SetProperty(ref _encryptConnection, value);
	}
	public bool           TrustServerCertificate
	{
		get => _trustServerCertificate;
		set => SetProperty(ref _trustServerCertificate, value);
	}
	public DatabaseModel? SelectedDatabase
	{
		get => _selectedDatabase;
		set
		{
			if (SetProperty(ref _selectedDatabase, value))
			{
				SelectedTable = value?.Tables.FirstOrDefault();
			}
		}
	}
	public TableModel?    SelectedTable
	{
		get => _selectedTable;
		set => SetProperty(ref _selectedTable, value);
	}
	public GenerationMode GenerationMode
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
	public bool           IsSqlFileMode      => GenerationMode == GenerationMode.SqlFile;
	public bool           IsDirectInsertMode => GenerationMode == GenerationMode.DirectInsert;
	public string         OutputFilePath
	{
		get => _outputFilePath;
		set
		{
			if (SetProperty(ref _outputFilePath, value))
			{
				RefreshCommands();
			}
		}
	}
	public bool           ConfirmTestDatabase
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
	public bool           IsBusy
	{
		get => _isBusy;
		private set
		{
			if (SetProperty(ref _isBusy, value))
			{
				RefreshCommands();
			}
		}
	}
	public string         StatusMessage
	{
		get         => _statusMessage;
		private set => SetProperty(ref _statusMessage, value);
	}
	public string         ErrorMessage
	{
		get         => _errorMessage;
		private set => SetProperty(ref _errorMessage, value);
	}
	#endregion PUBLIC
	#endregion PROPERTIES

	public MainViewModel(
		ISqlMetadataService    metadataService,
		IDataGenerationService generationService,
		IFileDialogService     fileDialogService
	)
	{
		_cancellationTokenSource = null;
		_serverName              = string.Empty;
		_userName                = string.Empty;
		_password                = string.Empty;
		_encryptConnection       = true;
		_trustServerCertificate  = false;
		_selectedDatabase        = null;
		_selectedTable           = null;
		_outputFilePath          = string.Empty;
		_generationMode          = GenerationMode.SqlFile;
		_confirmTestDatabase     = false;
		_isBusy                  = false;
		_statusMessage           = "Ready.";
		_errorMessage            = string.Empty;

		_metadataService    = metadataService   ?? throw new ArgumentNullException(nameof(metadataService));
		_generationService  = generationService ?? throw new ArgumentNullException(nameof(generationService));
		_fileDialogService  = fileDialogService ?? throw new ArgumentNullException(nameof(fileDialogService));

		Databases            = [];
		ValueGenerationModes = Enum.GetValues<ValueGenerationMode>();
		GenerationModes      = Enum.GetValues<GenerationMode>();
		LoadMetadataCommand  = new AsyncRelayCommand(LoadMetadataAsync,CanLoadMetadata);
		BrowseOutputCommand  = new RelayCommand(BrowseOutputFile, CanBrowseOutputFile);
		GenerateCommand      = new AsyncRelayCommand(GenerateAsync, CanGenerate);
		CancelCommand        = new RelayCommand(Cancel, CanCancel);
		RegexProfiles =
		[
			new RegexProfile
				{
					Name    = "Uppercase code",
					Pattern = "[A-Z]{8}"
				},
				new RegexProfile
				{
					Name    = "Asset reference",
					Pattern = "ASSET-[0-9]{6}"
				},
				new RegexProfile
				{
					Name    = "Bin location",
					Pattern = "[A-Z]{2}-[0-9]{3}-[A-Z]{1}"
				},
				new RegexProfile
				{
					Name    = "Australian test mobile",
					Pattern = "04[0-9]{8}"
				}
		];
	}

	
	private bool CanLoadMetadata(object? parameter)
		=> !IsBusy
			&& !string.IsNullOrWhiteSpace(ServerName)
			&& !string.IsNullOrWhiteSpace(UserName)
			&& !string.IsNullOrWhiteSpace(Password);

	private async Task LoadMetadataAsync(object? parameter)
		=> await RunBusyOperationAsync(async cancellationToken
			=> {
				StatusMessage = "Connecting to SQL Server and loading metadata...";

				string           connectionString = BuildConnectionString("master");
				Progress<string> progress         = new(message => StatusMessage = message);

				IReadOnlyList<DatabaseModel> databases = await
					_metadataService
						.LoadMetadataAsync(
							connectionString,
							progress,
							cancellationToken
						);

				UnsubscribeFromTableChanges();
				Databases.Clear();

				foreach (DatabaseModel database in databases)
				{
					Databases.Add(database);
				}

				SubscribeToTableChanges();

				SelectedDatabase = Databases.FirstOrDefault();
				StatusMessage    = $"Loaded metadata for {Databases.Count} database(s).";

				RefreshCommands();
			}
		);

	private bool CanBrowseOutputFile(object? parameter) => !IsBusy && IsSqlFileMode;

	private void BrowseOutputFile(object? parameter)
	{
		string? selectedPath = _fileDialogService.SelectSqlOutputFile();

		if (!string.IsNullOrWhiteSpace(selectedPath))
		{
			OutputFilePath = selectedPath;
		}
	}

	private bool CanGenerate(object? parameter)
	{
		bool hasSelectedTables =
			Databases
				.SelectMany(database => database.Tables)
				.Any(table => table.IsSelected);

		bool outputIsConfigured =
			IsSqlFileMode
				? !string.IsNullOrWhiteSpace(OutputFilePath)
				: ConfirmTestDatabase && HasCompleteConnectionDetails();

		return	!IsBusy
					&& hasSelectedTables
					&& outputIsConfigured;
	}

	private async Task GenerateAsync(object? parameter)
		=> await RunBusyOperationAsync(async cancellationToken
				=> {
					List<TableModel> selectedTables =
						[.. Databases
								.SelectMany(database => database.Tables)
								.Where(table => table.IsSelected)
						];

					ValidateForeignKeySelections(selectedTables);

					GenerationRequest request = new()
					{
						Tables           = selectedTables,
						Mode             = GenerationMode,
						OutputFilePath   = IsSqlFileMode ? OutputFilePath : null,
						ConnectionString = IsDirectInsertMode ? BuildConnectionString("master") : null
					};

					Progress<string> progress = new(message => StatusMessage = message);

					await _generationService.GenerateAsync(
						request,
						progress,
						cancellationToken
					);

					StatusMessage = IsSqlFileMode
											? "SQL script generation completed successfully."
											: "Direct database insertion completed successfully.";
				}
			);

	private static void ValidateForeignKeySelections(IReadOnlyCollection<TableModel> selectedTables)
	{
		HashSet<string> selectedTableKeys =
			selectedTables
				.Select(
						table => CreateTableKey(
										table.DatabaseName,
										table.SchemaName,
										table.Name
									)
				)
				.ToHashSet(StringComparer.OrdinalIgnoreCase);

		foreach (TableModel table in selectedTables)
		{
			foreach (ForeignKeyModel foreignKey in table.ForeignKeys)
			{
				ColumnModel? foreignKeyColumn =
					table
						.Columns
						.FirstOrDefault(
							column =>
								string.Equals(
									column.Name,
									foreignKey.ParentColumn,
									StringComparison.OrdinalIgnoreCase
								)
							)
						?? throw new InvalidOperationException(
								$"Foreign key '{foreignKey.Name}' references column '{foreignKey.ParentColumn}', but that "
								+ "column was not found in the loaded metadata."
							);

				if (foreignKeyColumn.GenerationMode == ValueGenerationMode.Fixed || foreignKeyColumn.GenerationMode == ValueGenerationMode.Null)
				{
					continue;
				}

				string referencedTableKey =
					CreateTableKey(
						foreignKey.ReferencedDatabase,
						foreignKey.ReferencedSchema,
						foreignKey.ReferencedTable
					);

				if (!selectedTableKeys.Contains(referencedTableKey))
				{
					throw new InvalidOperationException(
						$"{table.FullyQualifiedName}.[{foreignKeyColumn.Name}] references "
							+ $"[{foreignKey.ReferencedDatabase}].[{foreignKey.ReferencedSchema}].[{foreignKey.ReferencedTable}]. "
							+ $"Select that referenced table for generation, or configure the foreign-key column with a fixed existing value."
					);
				}
			}
		}
	}

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
			ConnectTimeout           = 15,
			ApplicationName          = "DataGenerator"
		};

		return builder.ConnectionString;
	}

	private bool CanCancel(object? parameter) => IsBusy;

	private void Cancel(object? parameter)
	{
		_cancellationTokenSource?.Cancel();
		StatusMessage = "Cancellation requested.";
	}

	private async Task RunBusyOperationAsync(Func<CancellationToken, Task> operation)
	{
		ArgumentNullException.ThrowIfNull(operation);

		ErrorMessage             = string.Empty;
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
		catch (SqlException exception)
		{
			ErrorMessage  = $"SQL Server error {exception.Number}: {exception.Message}";
			StatusMessage = "Operation failed.";
		}
		catch (IOException exception)
		{
			ErrorMessage  = $"File error: {exception.Message}";
			StatusMessage = "Operation failed.";
		}
		catch (UnauthorizedAccessException exception)
		{
			ErrorMessage  = $"Access denied: {exception.Message}";
			StatusMessage = "Operation failed.";
		}
		catch (Exception exception)
		{
			ErrorMessage  = exception.Message;
			StatusMessage = "Operation failed.";
		}
		finally
		{
			_cancellationTokenSource.Dispose();
			_cancellationTokenSource = null;
			IsBusy = false;
		}
	}

	private void SubscribeToTableChanges()
	{
		foreach (TableModel table in Databases.SelectMany(database => database.Tables))
		{
			table.PropertyChanged += OnTablePropertyChanged;
		}
	}

	private void UnsubscribeFromTableChanges()
	{
		foreach (TableModel table in Databases.SelectMany(database => database.Tables))
		{
			table.PropertyChanged -= OnTablePropertyChanged;
		}
	}

	private void OnTablePropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (	e.PropertyName == nameof(TableModel.IsSelected)
				|| e.PropertyName == nameof(TableModel.RowCount)
		)
		{
			RefreshCommands();
		}
	}

	private static string CreateTableKey(string databaseName, string schemaName, string tableName)
		=> $"{databaseName}.{schemaName}.{tableName}";

	private void RefreshCommands()
	{
		LoadMetadataCommand.NotifyCanExecuteChanged();
		BrowseOutputCommand.NotifyCanExecuteChanged();
		GenerateCommand.NotifyCanExecuteChanged();
		CancelCommand.NotifyCanExecuteChanged();
	}
}