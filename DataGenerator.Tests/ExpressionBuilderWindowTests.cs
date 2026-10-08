using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using DataGenerator.Interfaces;
using DataGenerator.Models;
using DataGenerator.Services;
using DataGenerator.ViewModels;
using DataGenerator.Views;
using Xunit;

namespace DataGenerator.Tests;

public sealed class ExpressionBuilderWindowTests
{
	#region CONSTRUCTOR
	public ExpressionBuilderWindowTests()
	{
	}
	#endregion CONSTRUCTOR

	#region METHODS
	#region PUBLIC
	[Fact]
	public void WindowLoadsThemedControlsAndBoundBuildAndCopyActions()
	{
		Exception? failure = null;
		Thread thread = new(() =>
		{
			Application? application = null;
			ExpressionBuilderWindow? window = null;

			try
			{
				application = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };

				foreach (string resource in new[] { "Brushes.Light.xaml", "Hints.xaml", "Controls.xaml" })
				{
					application.Resources.MergedDictionaries.Add(new ResourceDictionary
					{
						Source = new Uri($"/DataGenerator;component/Themes/{resource}", UriKind.Relative)
					});
				}

				MemoryStore store = new();
				TestClipboard clipboard = new();
				ExpressionBuilderViewModel viewModel = new(new ExpressionBuilder(), store, clipboard, new TestDialogs());
				window = new ExpressionBuilderWindow { DataContext = viewModel, ShowInTaskbar = false };
				window.Show();
				window.UpdateLayout();
				window.Dispatcher.Invoke(() => { }, DispatcherPriority.DataBind);
				Button build = Assert.Single(Descendants(window).OfType<Button>(), button => Equals(button.Content, "Build and save"));
				Assert.NotNull(build.Command);
				Assert.True(build.Command.CanExecute(build.CommandParameter));
				build.Command.Execute(build.CommandParameter);
				window.Dispatcher.Invoke(() => { }, DispatcherPriority.DataBind);
				Assert.Single(store.Entries);
				ListBox saved = Assert.Single(Descendants(window).OfType<ListBox>());
				Assert.Single(saved.Items.Cast<object>());
				Assert.Equal(application.TryFindResource("RowBrush"), saved.Background);
				Assert.Equal(application.TryFindResource("TextBrush"), saved.Foreground);
				TabControl outputs = Assert.Single(Descendants(window).OfType<TabControl>());
				Assert.Equal(4, outputs.Items.Count);
				outputs.SelectedIndex = 1;
				window.UpdateLayout();
				window.Dispatcher.Invoke(() => { }, DispatcherPriority.DataBind);
				Button copy = Assert.Single(Descendants(window).OfType<Button>(), button => Equals(button.Content, "Copy Pattern"));
				Assert.NotNull(copy.Command);
				copy.Command.Execute(copy.CommandParameter);
				Assert.Equal(viewModel.Pattern, clipboard.Text);
				Assert.NotEmpty(clipboard.Text);
			}
			catch (Exception exception)
			{
				failure = exception;
			}
			finally
			{
				window?.Close();
				application?.Shutdown();
			}
		});
		thread.SetApartmentState(ApartmentState.STA);
		thread.IsBackground = true;
		thread.Start();
		Assert.True(thread.Join(TimeSpan.FromSeconds(20)), "The bounded WPF smoke test timed out.");
		Assert.Null(failure);
	}
	#endregion PUBLIC

	#region PRIVATE
	private static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
	{
		for (int index = 0; index < VisualTreeHelper.GetChildrenCount(parent); ++index)
		{
			DependencyObject child = VisualTreeHelper.GetChild(parent, index);
			yield return child;

			foreach (DependencyObject descendant in Descendants(child))
			{
				yield return descendant;
			}
		}
	}
	#endregion PRIVATE
	#endregion METHODS

	#region TYPES
	private sealed class MemoryStore : IExpressionLibraryStore
	{
		#region FIELDS
		private IReadOnlyList<BuiltExpression> _entries;
		#endregion FIELDS

		#region PROPERTIES
		public IReadOnlyList<BuiltExpression> Entries
		{
			get => _entries;
			private set => _entries = value;
		}
		#endregion PROPERTIES

		#region CONSTRUCTOR
		public MemoryStore()
		{
			_entries = [];
		}
		#endregion CONSTRUCTOR

		#region METHODS
		public IReadOnlyList<BuiltExpression> Load() => Entries;
		public void Save(IReadOnlyList<BuiltExpression> expressions) => Entries = expressions;
		#endregion METHODS
	}

	private sealed class TestClipboard : IClipboardService
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
		public TestClipboard()
		{
			_text = string.Empty;
		}
		#endregion CONSTRUCTOR

		#region METHODS
		public void SetText(string text) => Text = text;
		#endregion METHODS
	}

	private sealed class TestDialogs : IDialogService
	{
		#region CONSTRUCTOR
		public TestDialogs()
		{
		}
		#endregion CONSTRUCTOR

		#region METHODS
		public bool         Confirm(string title, string message) => true;
		public DialogChoice AskYesNoCancel(string title, string message) => DialogChoice.Cancel;
		public void         ShowError(string title, string message) => throw new InvalidOperationException(message);
		#endregion METHODS
	}
	#endregion TYPES
}
