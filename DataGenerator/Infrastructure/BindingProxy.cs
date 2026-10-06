using System.Windows;

namespace DataGenerator.Infrastructure;

/// <summary>
///	Passes a data context to elements outside the visual tree, such as DataGrid columns and context menus, through a
///	resource: <c>Source={StaticResource ...}</c> with a path that starts with Data.
/// </summary>
public sealed class BindingProxy : Freezable
{
	public static readonly DependencyProperty DATA_PROPERTY =
		DependencyProperty.Register(
			"Data",
			typeof(object),
			typeof(BindingProxy),
			new UIPropertyMetadata(null)
		);

	public object? Data
	{
		get => GetValue(DATA_PROPERTY);
		set => SetValue(DATA_PROPERTY, value);
	}

	/// <summary>
	///	Creates a new proxy instance for WPF's <see cref="Freezable"/> cloning infrastructure.
	/// </summary>
	/// <returns>
	///	A new <see cref="BindingProxy"/> with no data assigned.
	/// </returns>
	protected override Freezable CreateInstanceCore() => new BindingProxy();
}