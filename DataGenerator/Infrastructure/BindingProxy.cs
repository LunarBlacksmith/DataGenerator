using System.Windows;

namespace DataGenerator.Infrastructure;

/// <summary>
/// Passes a data context to elements outside the visual tree, such as DataGrid columns and context menus, through a
/// resource: <c>Source={StaticResource ...}</c> with a path that starts with Data.
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

	protected override Freezable CreateInstanceCore() => new BindingProxy();
}