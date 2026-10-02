using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using DataGenerator.Services;

namespace DataGenerator.Infrastructure;

/// <summary>
/// Shows the pattern suggestions of one <see cref="TextBox"/> in a pop-up under the word being typed, without taking
/// the keyboard focus from the text box. See <see cref="PatternAutoComplete"/>.
/// </summary>
internal sealed class PatternCompletionController
{
	private const string POPUP_TEMPLATE_KEY = "PatternCompletionPopupTemplate";

	private readonly TextBox                    _textBox;
	private readonly IPatternCompletionProvider _provider;
	private readonly PatternCompletionSession   _session;

	private Popup?   _popup;
	private ListBox? _list;
	private Window?  _window;
	private bool     _isEditing;

	public PatternCompletionController(TextBox textBox, IPatternCompletionProvider provider)
	{
		_textBox  = textBox ?? throw new ArgumentNullException(nameof(textBox));
		_provider = provider ?? throw new ArgumentNullException(nameof(provider));
		_session  = new PatternCompletionSession();

		_textBox.TextChanged       += OnTextChanged;
		_textBox.SelectionChanged  += OnSelectionChanged;
		_textBox.PreviewKeyDown    += OnPreviewKeyDown;
		_textBox.LostKeyboardFocus += OnLostKeyboardFocus;
		_textBox.Unloaded          += OnUnloaded;
	}

	private bool IsOpen => _popup?.IsOpen == true;

	public void Detach()
	{
		_textBox.TextChanged       -= OnTextChanged;
		_textBox.SelectionChanged  -= OnSelectionChanged;
		_textBox.PreviewKeyDown    -= OnPreviewKeyDown;
		_textBox.LostKeyboardFocus -= OnLostKeyboardFocus;
		_textBox.Unloaded          -= OnUnloaded;

		Close();
	}

	private void OnTextChanged(object sender, TextChangedEventArgs e)
	{
		// Only typing opens the suggestions, not a value set by a binding.
		if (!_isEditing && _textBox.IsKeyboardFocused)
		{
			Update(allowOpen: true);
		}
	}

	private void OnSelectionChanged(object sender, RoutedEventArgs e)
	{
		if (!_isEditing && IsOpen)
		{
			Update(allowOpen: false);
		}
	}

	private void OnPreviewKeyDown(object sender, KeyEventArgs e)
	{
		if (!IsOpen)
		{
			return;
		}

		switch (e.Key)
		{
			case Key.Down:
				_session.MoveSelection(1);
				ScrollSelectedIntoView();
				e.Handled = true;
				break;

			case Key.Up:
				_session.MoveSelection(-1);
				ScrollSelectedIntoView();
				e.Handled = true;
				break;

			case Key.Escape:
				Close();
				e.Handled = true;
				break;

			case Key.Tab when Keyboard.Modifiers == ModifierKeys.None:
				// Tab is pressed twice to insert, so a single Tab can never replace a word by accident.
				if (_session.IsArmed)
				{
					Accept();
				}
				else
				{
					_session.IsArmed = true;
				}

				e.Handled = true;
				break;
		}
	}

	private void OnLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e) => Close();

	private void OnUnloaded(object sender, RoutedEventArgs e) => Close();

	private void Update(bool allowOpen)
	{
		PatternCompletionResult? result = _textBox.SelectionLength == 0
			? _provider.GetCompletions(_textBox.Text, _textBox.CaretIndex)
			: null;

		if (result is null)
		{
			Close();
			return;
		}

		if (!allowOpen && !IsOpen)
		{
			return;
		}

		_session.Update(result);
		Open(result.WordStart);
	}

	private void Open(int wordStart)
	{
		Popup popup = EnsurePopup();

		popup.PlacementRectangle = GetWordRectangle(wordStart);

		if (!popup.IsOpen)
		{
			popup.IsOpen = true;
			HookWindow();
		}

		// The text box may not have finished its layout yet (e.g. when the text has just wrapped), so the pop-up is
		// placed again once it has.
		_ = _textBox.Dispatcher.InvokeAsync(() =>
		{
			if (IsOpen && _session.Result is not null)
			{
				popup.PlacementRectangle = GetWordRectangle(_session.Result.WordStart);
				ScrollSelectedIntoView();
			}
		}, DispatcherPriority.Loaded);
	}

	private void Close()
	{
		_session.Clear();

		if (_popup is not null)
		{
			_popup.IsOpen = false;
		}

		UnhookWindow();
	}

	private void Accept()
	{
		PatternCompletionResult? result = _session.Result;
		PatternLanguageEntry?    entry  = _session.SelectedEntry;

		if (result is null || entry is null)
		{
			Close();
			return;
		}

		PatternCompletionEdit edit = _provider.CreateEdit(_textBox.Text, result, entry);

		_isEditing = true;

		try
		{
			// Replacing the selected text keeps the insertion in the text box's undo history.
			_textBox.Select(edit.Start, edit.Length);
			_textBox.SelectedText = edit.Text;
			_textBox.CaretIndex   = edit.CaretIndex;
		}
		finally
		{
			_isEditing = false;
		}

		Close();
	}

	private Popup EnsurePopup()
	{
		if (_popup is not null)
		{
			return _popup;
		}

		ContentPresenter presenter = new ContentPresenter
		{
			Content         = _session,
			ContentTemplate = (DataTemplate)_textBox.FindResource(POPUP_TEMPLATE_KEY)
		};

		_popup = new Popup
		{
			Child              = presenter,
			PlacementTarget    = _textBox,
			Placement          = PlacementMode.Bottom,
			StaysOpen          = true,
			AllowsTransparency = true,
			Focusable          = false
		};

		_popup.Opened += (_, _) => FindList(presenter);
		return _popup;
	}

	private void FindList(DependencyObject root)
	{
		if (_list is not null)
		{
			return;
		}

		_list = FindDescendant<ListBox>(root);

		if (_list is not null)
		{
			_list.PreviewMouseLeftButtonDown += OnListMouseDown;
		}
	}

	private void OnListMouseDown(object sender, MouseButtonEventArgs e)
	{
		if (e.ClickCount == 2 && _session.SelectedEntry is not null)
		{
			Accept();
			e.Handled = true;
		}
	}

	private void ScrollSelectedIntoView()
	{
		if (_list is not null && _session.SelectedEntry is not null)
		{
			_list.ScrollIntoView(_session.SelectedEntry);
		}
	}

	private Rect GetWordRectangle(int wordStart)
	{
		Rect rectangle = _textBox.GetRectFromCharacterIndex(wordStart);
		return rectangle.IsEmpty ? new Rect(0, 0, 0, _textBox.ActualHeight) : rectangle;
	}

	/// <summary>
	/// Closes the pop-up when the window moves, loses focus, is clicked or scrolls, since the pop-up would otherwise
	/// float away from its text box.
	/// </summary>
	private void HookWindow()
	{
		UnhookWindow();

		_window = Window.GetWindow(_textBox);

		if (_window is null)
		{
			return;
		}

		_window.LocationChanged  += OnWindowChanged;
		_window.Deactivated      += OnWindowChanged;
		_window.PreviewMouseDown += OnWindowMouseDown;
		_window.AddHandler(ScrollViewer.ScrollChangedEvent, new ScrollChangedEventHandler(OnWindowScrollChanged));
	}

	private void UnhookWindow()
	{
		if (_window is null)
		{
			return;
		}

		_window.LocationChanged  -= OnWindowChanged;
		_window.Deactivated      -= OnWindowChanged;
		_window.PreviewMouseDown -= OnWindowMouseDown;
		_window.RemoveHandler(ScrollViewer.ScrollChangedEvent, new ScrollChangedEventHandler(OnWindowScrollChanged));
		_window = null;
	}

	private void OnWindowChanged(object? sender, EventArgs e) => Close();

	private void OnWindowMouseDown(object sender, MouseButtonEventArgs e) => Close();

	private void OnWindowScrollChanged(object sender, ScrollChangedEventArgs e)
	{
		bool hasScrolled = e.VerticalChange != 0 || e.HorizontalChange != 0;

		// The text box's own scrolling (while the text grows) keeps the pop-up next to the word.
		if (hasScrolled && !(e.OriginalSource is DependencyObject source && IsWithinTextBox(source)))
		{
			Close();
		}
	}

	private bool IsWithinTextBox(DependencyObject element)
	{
		for (DependencyObject? current = element; current is not null; current = VisualTreeHelper.GetParent(current))
		{
			if (ReferenceEquals(current, _textBox))
			{
				return true;
			}
		}

		return false;
	}

	private static T? FindDescendant<T>(DependencyObject root) where T : DependencyObject
	{
		int childCount = VisualTreeHelper.GetChildrenCount(root);

		for (int index = 0; index < childCount; ++index)
		{
			DependencyObject child = VisualTreeHelper.GetChild(root, index);

			if (child is T match)
			{
				return match;
			}

			T? descendant = FindDescendant<T>(child);

			if (descendant is not null)
			{
				return descendant;
			}
		}

		return null;
	}
}