using System.Windows;

namespace DataGenerator.Infrastructure;

/// <summary>
///	Passes a data context to elements outside the visual tree, such as DataGrid columns and context menus, through a
///	resource: <c>Source={StaticResource ...}</c> with a path that starts with Data.
/// </summary>
public sealed class BindingProxy : Freezable
{
	#region FIELDS
	#region PUBLIC
	public static readonly DependencyProperty DATA_PROPERTY;
	#endregion PUBLIC
	#endregion FIELDS

	#region PROPERTIES
	#region PUBLIC
	public object? Data
	{
		get => GetValue(DATA_PROPERTY);
		set => SetValue(DATA_PROPERTY, value);
	}
	#endregion PUBLIC
	#endregion PROPERTIES

	#region CONSTRUCTORS
	#region STATIC
	/// <summary>
	///	Sets the default values of the static fields and properties of <see cref="BindingProxy"/>.
	/// </summary>
	static BindingProxy()
	{
		DATA_PROPERTY =
			DependencyProperty.Register(
				"Data",
				typeof(object),
				typeof(BindingProxy),
				new UIPropertyMetadata(null)
			);
	}
	#endregion STATIC

	#region PUBLIC
	/// <summary>
	///	Creates a new <see cref="BindingProxy"/>.
	/// </summary>
	public BindingProxy()
	{
	}
	#endregion PUBLIC
	#endregion CONSTRUCTORS

	#region METHODS
	#region PROTECTED
	/// <summary>
	///	Creates a new proxy instance for WPF's <see cref="Freezable"/> cloning infrastructure.
	/// </summary>
	/// <returns>
	///	A new <see cref="BindingProxy"/> with no data assigned.
	/// </returns>
	protected override Freezable CreateInstanceCore() => new BindingProxy();
	#endregion PROTECTED
	#endregion METHODS
}