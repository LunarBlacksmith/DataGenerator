using DataGenerator.Infrastructure;

namespace DataGenerator.ViewModels;

/// <summary>
///	Common state of the rows in the database explorer tree. Selection, filtering and striping are managed by
///	<see cref="DatabaseExplorerViewModel"/> so that they work across the whole tree.
/// </summary>
public abstract class TreeNodeViewModel : ObservableObject
{
	#region FIELDS
	private bool _isSelected;
	private bool _isHidden;
	private bool _isExpanded;
	private bool _isVisibleInTree;
	private bool _isAlternate;
	#endregion FIELDS

	#region PROPERTIES
	public abstract string DisplayName { get; }

	public bool IsSelected
	{
		get          => _isSelected;
		internal set => SetProperty(ref _isSelected, value);
	}

	/// <summary>
	///	Greys the node out (and collapses a database) so the tables being worked on stand out. Hidden tables are still
	///	generated when they are included.
	/// </summary>
	public bool IsHidden
	{
		get => _isHidden;
		set
		{
			if (SetProperty(ref _isHidden, value))
			{
				OnPropertyChanged(nameof(IsDimmed));
				OnPropertyChanged(nameof(VisibilityToolTip));
				OnHiddenChanged();
			}
		}
	}

	public virtual bool IsDimmed => _isHidden;

	public string VisibilityToolTip
		=>
			_isHidden
				? $"Show {DisplayName} again. With several rows selected, shows all of them."
				: $"Hide {DisplayName}: greys it out{(this is DatabaseNodeViewModel ? " and collapses it" : string.Empty)}. "
					+ "With several rows selected, hides all of them.";

	public bool IsExpanded
	{
		get => _isExpanded;
		set => SetProperty(ref _isExpanded, value);
	}

	/// <summary>
	///	False when the node is filtered out by the explorer's search text or "included only" option.
	/// </summary>
	public bool IsVisibleInTree
	{
		get          => _isVisibleInTree;
		internal set => SetProperty(ref _isVisibleInTree, value);
	}

	public bool IsAlternate
	{
		get          => _isAlternate;
		internal set => SetProperty(ref _isAlternate, value);
	}
	#endregion PROPERTIES

	#region CONSTRUCTOR
	/// <summary>
	///	Creates a new <see cref="TreeNodeViewModel"/> and sets the default values of its fields and properties.
	/// </summary>
	protected TreeNodeViewModel()
	{
		_isSelected      = false;
		_isHidden        = false;
		_isExpanded      = false;
		_isVisibleInTree = true;
		_isAlternate     = false;
	}
	#endregion CONSTRUCTOR

	#region METHODS
	/// <summary>
	///	Lets derived nodes react after their hidden state changes.
	/// </summary>
	protected virtual void OnHiddenChanged()
	{
	}

	/// <summary>
	///	Notifies bindings that the effective dimmed state changed.
	/// </summary>
	protected void RaiseDimmedChanged() => OnPropertyChanged(nameof(IsDimmed));
	#endregion METHODS
}