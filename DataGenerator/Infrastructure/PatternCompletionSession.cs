using System.Collections.ObjectModel;
using DataGenerator.Services;

namespace DataGenerator.Infrastructure;

/// <summary>
/// What the pattern suggestion pop-up shows: the matching functions and keywords, the selected one and whether the
/// next Tab inserts it.
/// </summary>
public sealed class PatternCompletionSession : ObservableObject
{
	private const string CHOOSE_HINT = "Tab twice to insert  ·  ↑ ↓ to choose  ·  Esc to close";
	private const string ARMED_HINT  = "Press Tab again to insert {0}";

	private PatternLanguageEntry?    _selectedEntry;
	private PatternCompletionResult? _result;
	private bool                     _isArmed;

	public ObservableCollection<PatternLanguageEntry> Items { get; } = [];

	public PatternLanguageEntry? SelectedEntry
	{
		get => _selectedEntry;
		set
		{
			if (SetProperty(ref _selectedEntry, value))
			{
				// Choosing another suggestion needs two new presses of Tab.
				IsArmed = false;
				OnPropertyChanged(nameof(KeyHintText));
			}
		}
	}

	/// <summary>
	/// Whether Tab has been pressed once, so the next Tab inserts the selected suggestion.
	/// </summary>
	public bool IsArmed
	{
		get => _isArmed;
		set
		{
			if (SetProperty(ref _isArmed, value))
			{
				OnPropertyChanged(nameof(KeyHintText));
			}
		}
	}

	public string KeyHintText => _isArmed && _selectedEntry is not null ? string.Format(ARMED_HINT, _selectedEntry.Name) : CHOOSE_HINT;

	public PatternCompletionResult? Result => _result;

	/// <summary>
	/// Shows the suggestions of <paramref name="result"/>, keeping the selected one when it still matches.
	/// </summary>
	public void Update(PatternCompletionResult result)
	{
		ArgumentNullException.ThrowIfNull(result);

		bool isSameWord = _result is not null
			&& _result.WordStart == result.WordStart
			&& string.Equals(_result.Prefix, result.Prefix, StringComparison.Ordinal);

		_result = result;

		if (!Items.SequenceEqual(result.Entries))
		{
			PatternLanguageEntry? previous = _selectedEntry;

			Items.Clear();

			foreach (PatternLanguageEntry entry in result.Entries)
			{
				Items.Add(entry);
			}

			SelectedEntry = previous is not null && Items.Contains(previous) ? previous : Items.FirstOrDefault();
		}

		if (!isSameWord)
		{
			IsArmed = false;
		}
	}

	public void MoveSelection(int offset)
	{
		if (Items.Count == 0)
		{
			return;
		}

		int index = _selectedEntry is null ? 0 : Items.IndexOf(_selectedEntry) + offset;

		// Moving past either end wraps around to the other.
		SelectedEntry = Items[((index % Items.Count) + Items.Count) % Items.Count];
	}

	public void Clear()
	{
		_result = null;
		IsArmed = false;
	}
}