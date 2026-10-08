using System.Collections.ObjectModel;
using System.IO;
using System.Runtime.InteropServices;
using DataGenerator.Infrastructure;
using DataGenerator.Interfaces;
using DataGenerator.Models;

namespace DataGenerator.ViewModels;

/// <summary>
///	Builds, saves and recalls named expressions without executing any generated SQL.
/// </summary>
public sealed class ExpressionBuilderViewModel : ObservableObject
{
	#region FIELDS
	private readonly IExpressionBuilder      _builder;
	private readonly IExpressionLibraryStore _store;
	private readonly IClipboardService       _clipboard;
	private readonly IDialogService          _dialogs;

	private bool             _libraryAvailable;
	private string           _feed;
	private string           _columnName;
	private string           _name;
	private string           _search;
	private string           _message;
	private BuiltExpression? _result;
	private BuiltExpression? _selectedEntry;
	#endregion FIELDS

	#region PROPERTIES
	public ObservableCollection<BuiltExpression>     Entries       { get; }

	public RelayCommand                              BuildCommand  { get; }
	public RelayCommand                              SaveCommand   { get; }
	public RelayCommand                              DeleteCommand { get; }
	public RelayCommand                              CopyCommand   { get; }

	public string                                    Feed
	{
		get => _feed;
		set
		{
			if (SetProperty(ref _feed, value ?? string.Empty))
			{
				SetResult(null);
			}
		}
	}

	public string                                    ColumnName
	{
		get => _columnName;
		set
		{
			if (SetProperty(ref _columnName, value ?? string.Empty))
			{
				SetResult(null);
			}
		}
	}

	public string                                    Name
	{
		get => _name;
		set => SetProperty(ref _name, value ?? string.Empty);
	}

	public string                                    Search
	{
		get => _search;
		set
		{
			if (SetProperty(ref _search, value ?? string.Empty))
			{
				OnPropertyChanged(nameof(VisibleEntries));
			}
		}
	}

	public IReadOnlyList<BuiltExpression>            VisibleEntries
		=> Entries
		.Where(entry => entry.Name.Contains(Search, StringComparison.OrdinalIgnoreCase)
			|| entry.Feed.Contains(Search, StringComparison.OrdinalIgnoreCase))
		.OrderByDescending(entry => entry.SavedAt)
		.ToList();

	public BuiltExpression?                          SelectedEntry
	{
		get => _selectedEntry;
		set
		{
			if (SetProperty(ref _selectedEntry, value))
			{
				if (value is not null)
				{
					Feed = value.Feed;
					ColumnName = value.ColumnName;
					Name = value.Name;
					SetResult(value);
					Message = "Recalled saved input and expressions. Edit the input, then build again to save a revision.";
				}

				DeleteCommand.NotifyCanExecuteChanged();
			}
		}
	}

	public string                                    Description => _result?.Description ?? string.Empty;
	public string                                    Pattern     => _result?.Pattern ?? string.Empty;
	public string                                    Regex       => _result?.Regex ?? string.Empty;
	public string                                    Sql         => _result?.Sql ?? string.Empty;
	public string                                    Message
	{
		get => _message;
		private set => SetProperty(ref _message, value);
	}
	#endregion PROPERTIES

	#region CONSTRUCTOR
	public ExpressionBuilderViewModel(
		IExpressionBuilder builder,
		IExpressionLibraryStore store,
		IClipboardService clipboard,
		IDialogService dialogs)
	{
		_libraryAvailable = false;
		_feed             = "MS[10]-Y[01, 03, 05]-X[001 > 050]-[LR]-D[1]";
		_columnName       = "Value";
		_name             = string.Empty;
		_search           = string.Empty;
		_message          = string.Empty;
		_result           = null;
		_selectedEntry    = null;
		Entries           = [];

		_builder = builder ?? throw new ArgumentNullException(nameof(builder));
		_store = store ?? throw new ArgumentNullException(nameof(store));
		_clipboard = clipboard ?? throw new ArgumentNullException(nameof(clipboard));
		_dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
		BuildCommand  = new RelayCommand(_ => Build());
		SaveCommand   = new RelayCommand(_ => Save(), _ => _result is not null && _libraryAvailable);
		DeleteCommand = new RelayCommand(_ => Delete(), _ => SelectedEntry is not null && _libraryAvailable);
		CopyCommand   = new RelayCommand(Copy, parameter => !string.IsNullOrEmpty(GetOutput(parameter)));

		try
		{
			foreach (BuiltExpression entry in _store.Load())
			{
				Entries.Add(entry);
			}

			_libraryAvailable = true;
		}
		catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
		{
			ShowError("The expression library could not be loaded. Saving is disabled to preserve the existing file.", exception);
		}
	}
	#endregion CONSTRUCTOR

	#region METHODS
	#region PRIVATE
	private void   Build()
	{
		try
		{
			SetResult(_builder.Build(Feed, ColumnName));
			Message = "Expressions built. No SQL was executed.";

			if (_libraryAvailable)
			{
				Save();
			}
			else
			{
				Message = "Expressions built, but not saved: the expression library could not be loaded. You can still copy the outputs.";
			}
		}
		catch (FormatException exception)
		{
			SetResult(null);
			Message = exception.Message;
		}
	}

	private void   Save()
	{
		if (_result is null || !_libraryAvailable)
		{
			return;
		}

		string name = string.IsNullOrWhiteSpace(Name) ? Feed : Name.Trim();
		BuiltExpression? existing = Entries.FirstOrDefault(entry => entry.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

		if (existing is not null && !_dialogs.Confirm("Replace saved expression?", $"Replace '{name}' with the current input and expressions?"))
		{
			Message = "Expressions built, but the saved entry was not replaced.";
			return;
		}

		BuiltExpression saved = new()
		{
			Name = name,
			Feed = Feed,
			ColumnName = ColumnName,
			Description = Description,
			Pattern = Pattern,
			Regex = Regex,
			Sql = Sql,
			SavedAt = DateTimeOffset.Now
		};
		List<BuiltExpression> updated = Entries.Where(entry => !ReferenceEquals(entry, existing)).ToList();
		updated.Add(saved);

		try
		{
			_store.Save(updated);

			if (existing is not null)
			{
				Entries.Remove(existing);
			}

			Entries.Add(saved);
			OnPropertyChanged(nameof(VisibleEntries));
			SelectedEntry = saved;
			Message = $"Saved '{name}'. All four outputs and the feed text are available for reuse.";
		}
		catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
		{
			ShowError("The expressions were built, but could not be saved. The library list has not changed.", exception);
		}
	}

	private void   Delete()
	{
		BuiltExpression? selected = SelectedEntry;

		if (selected is null || !_dialogs.Confirm("Delete saved expression?", $"Delete '{selected.Name}' from the expression library?"))
		{
			return;
		}

		try
		{
			_store.Save(Entries.Where(entry => !ReferenceEquals(entry, selected)).ToList());
			SelectedEntry = null;
			Entries.Remove(selected);
			OnPropertyChanged(nameof(VisibleEntries));
			Message = $"Deleted '{selected.Name}'. The current expressions remain available to copy.";
		}
		catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
		{
			ShowError("The saved expression could not be deleted.", exception);
		}
	}

	private void   SetResult(BuiltExpression? result)
	{
		_result = result;
		OnPropertyChanged(nameof(Description));
		OnPropertyChanged(nameof(Pattern));
		OnPropertyChanged(nameof(Regex));
		OnPropertyChanged(nameof(Sql));
		SaveCommand.NotifyCanExecuteChanged();
		CopyCommand.NotifyCanExecuteChanged();
	}

	private string GetOutput(object? parameter) => (parameter as string) switch
	{
		"Description" => Description,
		"Pattern" => Pattern,
		"Regex" => Regex,
		"Sql" => Sql,
		_ => string.Empty
	};

	private void   Copy(object? parameter)
	{
		string output = GetOutput(parameter);

		if (output.Length == 0)
		{
			return;
		}

		try
		{
			_clipboard.SetText(output);
			Message = "Expression copied.";
		}
		catch (ExternalException exception)
		{
			ShowError("The expression could not be copied to the clipboard.", exception);
		}
	}

	private void   ShowError(string message, Exception exception)
	{
		Message = $"{message}{Environment.NewLine}{exception.Message}";
		_dialogs.ShowError("Expression builder", Message);
	}
	#endregion PRIVATE
	#endregion METHODS
}
