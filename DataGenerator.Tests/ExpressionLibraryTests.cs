using System.IO;
using DataGenerator.Interfaces;
using DataGenerator.Models;
using DataGenerator.Services;
using DataGenerator.ViewModels;
using Xunit;

namespace DataGenerator.Tests;

public sealed class ExpressionLibraryTests
{
	#region CONSTRUCTOR
	public ExpressionLibraryTests()
	{
	}
	#endregion CONSTRUCTOR

	#region METHODS
	#region PUBLIC
	[Fact]
	public void BuildSavesEveryOutputAndCanRecallAfterReopening()
	{
		string directory = Path.Combine(Path.GetTempPath(), "DataGenerator-expression-tests-" + Guid.NewGuid().ToString("N"));
		string path = Path.Combine(directory, "SavedExpressions.json");

		try
		{
			JsonExpressionLibraryStore store = new(path);
			FakeDialogs dialogs = new();
			FakeClipboard clipboard = new();
			ExpressionBuilderViewModel viewModel = new(new ExpressionBuilder(), store, clipboard, dialogs)
			{
				Name = "Warehouse location",
				ColumnName = "Location"
			};
			viewModel.BuildCommand.Execute(null);
			BuiltExpression entry = Assert.Single(viewModel.Entries);
			Assert.True(File.Exists(path));
			Assert.Contains("\"formatVersion\": 1", File.ReadAllText(path));
			Assert.Contains("\"expressions\"", File.ReadAllText(path));
			Assert.False(File.Exists(path + ".tmp"));
			Assert.Equal(viewModel.Feed, entry.Feed);
			Assert.Equal(viewModel.Description, entry.Description);
			Assert.Equal(viewModel.Pattern, entry.Pattern);
			Assert.Equal(viewModel.Regex, entry.Regex);
			Assert.Equal(viewModel.Sql, entry.Sql);
			Assert.NotEqual(default, entry.SavedAt);

			ExpressionBuilderViewModel reopened = new(new ExpressionBuilder(), store, clipboard, dialogs);
			reopened.SelectedEntry = Assert.Single(reopened.Entries);
			Assert.Equal("Warehouse location", reopened.Name);
			Assert.Equal("Location", reopened.ColumnName);
			Assert.Equal(entry.Feed, reopened.Feed);
			Assert.Equal(entry.Description, reopened.Description);
			Assert.Equal(entry.Pattern, reopened.Pattern);
			Assert.Equal(entry.Regex, reopened.Regex);
			Assert.Equal(entry.Sql, reopened.Sql);
			reopened.CopyCommand.Execute("Regex");
			Assert.Equal(entry.Regex, clipboard.Text);
			reopened.Search = "warehouse";
			Assert.Single(reopened.VisibleEntries);
			reopened.Search = "unmatched";
			Assert.Empty(reopened.VisibleEntries);
			Assert.Empty(dialogs.Errors);
		}
		finally
		{
			if (File.Exists(path))
			{
				File.Delete(path);
			}

			if (Directory.Exists(directory))
			{
				Directory.Delete(directory);
			}
		}
	}

	[Fact]
	public void EditingInputClearsStaleOutputsAndInvalidFeedIsNotSaved()
	{
		FakeStore store = new();
		ExpressionBuilderViewModel viewModel = Create(store);
		viewModel.BuildCommand.Execute(null);
		Assert.Single(store.Saved);
		viewModel.Feed = "[bad format]";
		Assert.Empty(viewModel.Pattern);
		Assert.Empty(viewModel.Regex);
		Assert.False(viewModel.SaveCommand.CanExecute(null));
		Assert.False(viewModel.CopyCommand.CanExecute("Pattern"));
		viewModel.BuildCommand.Execute(null);
		Assert.Single(store.Saved);
		Assert.Contains("Unsupported segment", viewModel.Message);
		viewModel.Feed = "CODE[01]";
		viewModel.Name = string.Empty;
		viewModel.BuildCommand.Execute(null);
		Assert.Equal(2, store.Saved.Count);
		viewModel.ColumnName = "NewColumn";
		Assert.Empty(viewModel.Sql);
	}

	[Fact]
	public void SaveAsReplaceAndDeleteRespectConfirmation()
	{
		FakeStore store = new();
		FakeDialogs dialogs = new();
		ExpressionBuilderViewModel viewModel = new(new ExpressionBuilder(), store, new FakeClipboard(), dialogs) { Name = "First" };
		viewModel.BuildCommand.Execute(null);
		viewModel.Name = "Second";
		viewModel.SaveCommand.Execute(null);
		Assert.Equal(2, store.Saved.Count);
		viewModel.Name = "First";
		viewModel.Feed = "[01]";
		dialogs.AllowConfirmation = false;
		viewModel.BuildCommand.Execute(null);
		Assert.Equal(2, store.Saved.Count);
		Assert.NotEqual("[01]", store.Saved[0].Feed);
		Assert.Contains("not replaced", viewModel.Message);
		dialogs.AllowConfirmation = true;
		viewModel.SaveCommand.Execute(null);
		Assert.Equal(2, store.Saved.Count);
		Assert.Equal("[01]", viewModel.SelectedEntry?.Feed);
		dialogs.AllowConfirmation = false;
		viewModel.DeleteCommand.Execute(null);
		Assert.Equal(2, store.Saved.Count);
		dialogs.AllowConfirmation = true;
		viewModel.DeleteCommand.Execute(null);
		Assert.Single(store.Saved);
		Assert.Null(viewModel.SelectedEntry);
	}

	[Fact]
	public void SaveFailureDoesNotPretendTheLibraryWasUpdated()
	{
		FakeStore store = new() { FailSave = true };
		FakeDialogs dialogs = new();
		ExpressionBuilderViewModel viewModel = new(new ExpressionBuilder(), store, new FakeClipboard(), dialogs);
		viewModel.BuildCommand.Execute(null);
		Assert.Empty(viewModel.Entries);
		Assert.Empty(store.Saved);
		Assert.NotEmpty(viewModel.Regex);
		Assert.Contains("could not be saved", viewModel.Message);
		Assert.Single(dialogs.Errors);
	}

	[Fact]
	public void LoadFailurePreservesFileAndDisablesWritesButAllowsBuilding()
	{
		FakeStore store = new() { FailLoad = true };
		FakeDialogs dialogs = new();
		ExpressionBuilderViewModel viewModel = new(new ExpressionBuilder(), store, new FakeClipboard(), dialogs);
		Assert.Single(dialogs.Errors);
		viewModel.BuildCommand.Execute(null);
		Assert.NotEmpty(viewModel.Pattern);
		Assert.False(viewModel.SaveCommand.CanExecute(null));
		Assert.False(viewModel.DeleteCommand.CanExecute(null));
		Assert.Empty(store.Saved);
		Assert.Contains("not saved", viewModel.Message);
	}

	[Theory]
	[InlineData("{invalid")]
	[InlineData("{\"formatVersion\": 2, \"expressions\": []}")]
	[InlineData("{\"formatVersion\": 1, \"expressions\": [null]}")]
	[InlineData("{\"formatVersion\": 1, \"expressions\": [{\"name\":\"Empty\"}]}")]
	[InlineData("{\"formatVersion\": 1}")]
	[InlineData("{\"expressions\": []}")]
	public void CorruptOrUnsupportedLibraryIsRejectedWithoutChangingIt(string text)
	{
		string path = Path.Combine(Path.GetTempPath(), "DataGenerator-expression-tests-" + Guid.NewGuid().ToString("N") + ".json");

		try
		{
			File.WriteAllText(path, text);
			JsonExpressionLibraryStore store = new(path);
			Assert.Throws<InvalidDataException>(() => store.Load());
			Assert.Equal(text, File.ReadAllText(path));
		}
		finally
		{
			File.Delete(path);
		}
	}

	[Fact]
	public void IncompleteOrDuplicateEntriesCannotOverwriteASavedLibrary()
	{
		string path = Path.Combine(Path.GetTempPath(), "DataGenerator-expression-tests-" + Guid.NewGuid().ToString("N") + ".json");

		try
		{
			JsonExpressionLibraryStore store = new(path);
			BuiltExpression entry = new ExpressionBuilder().Build("A[1]", "Code");
			entry.Name = "Entry";
			store.Save([entry]);
			string saved = File.ReadAllText(path);
			Assert.Throws<InvalidDataException>(() => store.Save([new BuiltExpression { Name = "Incomplete" }]));
			Assert.Throws<InvalidDataException>(() => store.Save([entry, entry]));
			Assert.Equal(saved, File.ReadAllText(path));
		}
		finally
		{
			File.Delete(path);
		}
	}

	#endregion PUBLIC

	#region PRIVATE
	private static ExpressionBuilderViewModel Create(FakeStore store) => new(new ExpressionBuilder(), store, new FakeClipboard(), new FakeDialogs());
	#endregion PRIVATE
	#endregion METHODS

	#region TYPES
	private sealed class FakeStore : IExpressionLibraryStore
	{
		#region FIELDS
		private List<BuiltExpression> _saved;
		private bool                  _failLoad;
		private bool                  _failSave;
		#endregion FIELDS

		#region PROPERTIES
		public List<BuiltExpression> Saved
		{
			get => _saved;
			private set => _saved = value;
		}

		public bool                  FailLoad
		{
			get => _failLoad;
			set => _failLoad = value;
		}

		public bool                  FailSave
		{
			get => _failSave;
			set => _failSave = value;
		}
		#endregion PROPERTIES

		#region CONSTRUCTOR
		public FakeStore()
		{
			_saved    = [];
			_failLoad = false;
			_failSave = false;
		}
		#endregion CONSTRUCTOR

		#region METHODS
		public IReadOnlyList<BuiltExpression> Load() => FailLoad ? throw new IOException("Unreadable test file.") : Saved;
		public void Save(IReadOnlyList<BuiltExpression> expressions)
		{
			if (FailSave)
			{
				throw new IOException("Read-only test file.");
			}

			Saved = expressions.ToList();
		}
		#endregion METHODS
	}

	private sealed class FakeClipboard : IClipboardService
	{
		#region FIELDS
		private string _text;
		#endregion FIELDS

		#region PROPERTIES
		public string Text
		{
			get => _text;
			private set => _text = value;
		}
		#endregion PROPERTIES

		#region CONSTRUCTOR
		public FakeClipboard()
		{
			_text = string.Empty;
		}
		#endregion CONSTRUCTOR

		#region METHODS
		public void SetText(string text) => Text = text;
		#endregion METHODS
	}

	private sealed class FakeDialogs : IDialogService
	{
		#region FIELDS
		private List<string> _errors;
		private bool         _allowConfirmation;
		#endregion FIELDS

		#region PROPERTIES
		public List<string> Errors
		{
			get => _errors;
		}

		public bool         AllowConfirmation
		{
			get => _allowConfirmation;
			set => _allowConfirmation = value;
		}
		#endregion PROPERTIES

		#region CONSTRUCTOR
		public FakeDialogs()
		{
			_errors            = [];
			_allowConfirmation = true;
		}
		#endregion CONSTRUCTOR

		#region METHODS
		public bool         Confirm(string title, string message) => AllowConfirmation;
		public DialogChoice AskYesNoCancel(string title, string message) => DialogChoice.Cancel;
		public void         ShowError(string title, string message) => Errors.Add(message);
		#endregion METHODS
	}
	#endregion TYPES
}
