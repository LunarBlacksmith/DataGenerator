using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using DataGenerator.Interfaces;
using DataGenerator.Services;

namespace DataGenerator.Infrastructure;

/// <summary>
///	Shows the pattern suggestions of one <see cref="TextBox"/> in a pop-up under the word being typed, without taking
///	the keyboard focus from the text box. See <see cref="PatternAutoComplete"/>.
/// </summary>
internal sealed class PatternCompletionController
{
	#region FIELDS
	private const string POPUP_TEMPLATE_KEY = "PatternCompletionPopupTemplate";

	private readonly TextBox                    _textBox;
	private readonly IPatternCompletionProvider _provider;
	private readonly PatternCompletionSession   _session;

	private Popup?   _popup;
	private ListBox? _list;
	private Window?  _window;
	private bool     _isEditing;
	#endregion FIELDS

	#region PROPERTIES
	/// <summary>
	///	Whether the suggestions are shown, in which case ↑ and ↓ choose a suggestion.
	/// </summary>
	public bool IsOpen => _popup?.IsOpen == true;
	#endregion PROPERTIES

	#region CONSTRUCTOR
	/// <summary>
	///	Creates a controller for one text box and starts listening for text, selection and keyboard changes.
	/// </summary>
	/// <param name="textBox">
	///	The text box that shows suggestions while the user types.
	/// </param>
	/// <param name="provider">
	///	The provider that supplies completions and insertion edits.
	/// </param>
	/// <exception cref="ArgumentNullException">
	///	Thrown when <paramref name="textBox"/> or <paramref name="provider"/> is <see langword="null"/>.
	/// </exception>
	public PatternCompletionController(TextBox textBox, IPatternCompletionProvider provider)
	{
		_popup     = null;
		_list      = null;
		_window    = null;
		_isEditing = false;

		_textBox  = textBox ?? throw new ArgumentNullException(nameof(textBox));
		_provider = provider ?? throw new ArgumentNullException(nameof(provider));
		_session  = new PatternCompletionSession();

		_textBox.TextChanged       += OnTextChanged;
		_textBox.SelectionChanged  += OnSelectionChanged;
		_textBox.PreviewKeyDown    += OnPreviewKeyDown;
		_textBox.LostKeyboardFocus += OnLostKeyboardFocus;
		_textBox.Unloaded          += OnUnloaded;
	}
	#endregion CONSTRUCTOR

	#region METHODS
	#region PUBLIC
	/// <summary>
	///	Stops listening to the text box and closes any open suggestion pop-up.
	/// </summary>
	public void Detach()
	{
		_textBox.TextChanged       -= OnTextChanged;
		_textBox.SelectionChanged  -= OnSelectionChanged;
		_textBox.PreviewKeyDown    -= OnPreviewKeyDown;
		_textBox.LostKeyboardFocus -= OnLostKeyboardFocus;
		_textBox.Unloaded          -= OnUnloaded;

		Close();
	}
	#endregion PUBLIC

	#region PRIVATE
	/// <summary>
	///	Finds the first visual descendant of the requested type below a root.
	/// </summary>
	/// <typeparam name="T">
	///	The descendant type to find.
	/// </typeparam>
	/// <param name="root">
	///	The visual root to search.
	/// </param>
	/// <returns>
	///	The first matching descendant, or <see langword="null"/> when none is found.
	/// </returns>
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

	/// <summary>
	///	Refreshes the suggestions after user typing changes the text.
	/// </summary>
	/// <param name="sender">
	///	The text box that raised the event.
	/// </param>
	/// <param name="e">
	///	The text change event raised by WPF.
	/// </param>
	private void OnTextChanged(object sender, TextChangedEventArgs e)
	{
		// Only typing opens the suggestions, not a value set by a binding.
		if (!_isEditing && _textBox.IsKeyboardFocused)
		{
			Update(allowOpen: true);
		}
	}

	/// <summary>
	///	Refreshes an open suggestion list when the caret moves.
	/// </summary>
	/// <param name="sender">
	///	The text box that raised the event.
	/// </param>
	/// <param name="e">
	///	The selection change event raised by WPF.
	/// </param>
	private void OnSelectionChanged(object sender, RoutedEventArgs e)
	{
		if (!_isEditing && IsOpen)
		{
			Update(allowOpen: false);
		}
	}

	/// <summary>
	///	Handles keys that move, close, arm or accept the current suggestion.
	/// </summary>
	/// <param name="sender">
	///	The text box that raised the key event.
	/// </param>
	/// <param name="e">
	///	The preview key event to handle.
	/// </param>
	private void OnPreviewKeyDown(object sender, KeyEventArgs e)
	{
		if (!IsOpen)
		{
			return;
		}

		switch (e.Key)
		{
			case Key.Down:
			{
				_session.MoveSelection(1);
				ScrollSelectedIntoView();
				e.Handled = true;
				break;
			}

			case Key.Up:
			{
				_session.MoveSelection(-1);
				ScrollSelectedIntoView();
				e.Handled = true;
				break;
			}

			case Key.Escape:
			{
				Close();
				e.Handled = true;
				break;
			}

			case Key.Tab when Keyboard.Modifiers == ModifierKeys.None:
			{
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
	}

	/// <summary>
	///	Closes the suggestion pop-up when the text box loses keyboard focus.
	/// </summary>
	/// <param name="sender">
	///	The text box that lost focus.
	/// </param>
	/// <param name="e">
	///	The keyboard focus change event raised by WPF.
	/// </param>
	private void OnLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e) => Close();

	/// <summary>
	///	Closes the suggestion pop-up when the text box is unloaded.
	/// </summary>
	/// <param name="sender">
	///	The text box that was unloaded.
	/// </param>
	/// <param name="e">
	///	The routed event data supplied by WPF.
	/// </param>
	private void OnUnloaded(object sender, RoutedEventArgs e) => Close();

	/// <summary>
	///	Asks the provider for completions at the caret and opens or refreshes the pop-up when appropriate.
	/// </summary>
	/// <param name="allowOpen">
	///	Whether a closed pop-up may be opened for the current result.
	/// </param>
	private void Update(bool allowOpen)
	{
		PatternCompletionResult? result =
			_textBox.SelectionLength == 0
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

	/// <summary>
	///	Shows the suggestion pop-up under the completed word and schedules a second placement after layout.
	/// </summary>
	/// <param name="wordStart">
	///	The character index where the completed word starts.
	/// </param>
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

	/// <summary>
	///	Hides the suggestion pop-up, clears its session state and removes window-level handlers.
	/// </summary>
	private void Close()
	{
		_session.Clear();

		if (_popup is not null)
		{
			_popup.IsOpen = false;
		}

		UnhookWindow();
	}

	/// <summary>
	///	Inserts the selected suggestion into the text box, or closes the pop-up when nothing can be inserted.
	/// </summary>
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

	/// <summary>
	///	Creates the suggestion pop-up on first use and reuses it afterwards.
	/// </summary>
	/// <returns>
	///	The pop-up used to display the suggestion session.
	/// </returns>
	private Popup EnsurePopup()
	{
		if (_popup is not null)
		{
			return _popup;
		}

		ContentPresenter presenter = new()
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

	/// <summary>
	///	Finds the list box inside the pop-up template and hooks double-click acceptance.
	/// </summary>
	/// <param name="root">
	///	The template root to search.
	/// </param>
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

	/// <summary>
	///	Accepts the selected suggestion when the user double-clicks the list.
	/// </summary>
	/// <param name="sender">
	///	The list box that received the mouse event.
	/// </param>
	/// <param name="e">
	///	The mouse button event raised by WPF.
	/// </param>
	private void OnListMouseDown(object sender, MouseButtonEventArgs e)
	{
		if (e.ClickCount == 2 && _session.SelectedEntry is not null)
		{
			Accept();
			e.Handled = true;
		}
	}

	/// <summary>
	///	Scrolls the selected suggestion into view when the pop-up list exists.
	/// </summary>
	private void ScrollSelectedIntoView()
	{
		if (_list is not null && _session.SelectedEntry is not null)
		{
			_list.ScrollIntoView(_session.SelectedEntry);
		}
	}

	/// <summary>
	///	Calculates the text box rectangle used to place the pop-up under the completed word.
	/// </summary>
	/// <param name="wordStart">
	///	The character index where the completed word starts.
	/// </param>
	/// <returns>
	///	The word rectangle, or a zero-width rectangle spanning the text box height when WPF cannot locate the character.
	/// </returns>
	private Rect GetWordRectangle(int wordStart)
	{
		Rect rectangle = _textBox.GetRectFromCharacterIndex(wordStart);
		return rectangle.IsEmpty ? new Rect(0, 0, 0, _textBox.ActualHeight) : rectangle;
	}

	/// <summary>
	///	Closes the pop-up when the window moves, loses focus, is clicked or scrolls, since the pop-up would otherwise
	///	float away from its text box.
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

	/// <summary>
	///	Removes handlers from the current window, if one is hooked.
	/// </summary>
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

	/// <summary>
	///	Closes the pop-up after a window movement or deactivation.
	/// </summary>
	/// <param name="sender">
	///	The window that raised the event.
	/// </param>
	/// <param name="e">
	///	The event data supplied by WPF.
	/// </param>
	private void OnWindowChanged(object? sender, EventArgs e) => Close();

	/// <summary>
	///	Closes the pop-up when the user clicks elsewhere in the window.
	/// </summary>
	/// <param name="sender">
	///	The window that received the mouse event.
	/// </param>
	/// <param name="e">
	///	The mouse button event raised by WPF.
	/// </param>
	private void OnWindowMouseDown(object sender, MouseButtonEventArgs e) => Close();

	/// <summary>
	///	Closes the pop-up when a parent scroll viewer scrolls away from the text box.
	/// </summary>
	/// <param name="sender">
	///	The object that raised the scroll event.
	/// </param>
	/// <param name="e">
	///	The scroll change event raised by WPF.
	/// </param>
	private void OnWindowScrollChanged(object sender, ScrollChangedEventArgs e)
	{
		bool hasScrolled = e.VerticalChange != 0 || e.HorizontalChange != 0;

		// The text box's own scrolling (while the text grows) keeps the pop-up next to the word.
		if (hasScrolled && !(e.OriginalSource is DependencyObject source && IsWithinTextBox(source)))
		{
			Close();
		}
	}

	/// <summary>
	///	Checks whether a dependency object belongs to the controlled text box's visual tree.
	/// </summary>
	/// <param name="element">
	///	The element whose visual ancestors are searched.
	/// </param>
	/// <returns>
	///	<see langword="true"/> when <paramref name="element"/> is inside the text box; otherwise
	///	<see langword="false"/>.
	/// </returns>
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
	#endregion PRIVATE
	#endregion METHODS
}