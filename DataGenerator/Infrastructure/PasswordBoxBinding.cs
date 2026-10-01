using System.Windows;
using System.Windows.Controls;

namespace DataGenerator.Infrastructure;

public static class PasswordBoxBinding
{
	public static readonly DependencyProperty BOUND_PASSWORD_PROPERTY =
		DependencyProperty.RegisterAttached(
			"BoundPassword",
			typeof(string),
			typeof(PasswordBoxBinding),
			new FrameworkPropertyMetadata(
				string.Empty,
				FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
				OnBoundPasswordChanged
			)
		);

	public static readonly DependencyProperty BIND_PASSWORD_PROPERTY =
		DependencyProperty.RegisterAttached(
			"BindPassword",
			typeof(bool),
			typeof(PasswordBoxBinding),
			new PropertyMetadata(false, OnBindPasswordChanged)
		);

	private static readonly DependencyProperty UPDATING_PASSWORD_PROPERTY =
		DependencyProperty.RegisterAttached(
			"UpdatingPassword",
			typeof(bool),
			typeof(PasswordBoxBinding),
			new PropertyMetadata(false)
		);

	public static string GetBoundPassword(DependencyObject element) => (string)element.GetValue(BOUND_PASSWORD_PROPERTY);
	public static void SetBoundPassword(DependencyObject element, string value) => element.SetValue(BOUND_PASSWORD_PROPERTY, value);
	public static bool GetBindPassword(DependencyObject element) => (bool)element.GetValue(BIND_PASSWORD_PROPERTY);
	public static void SetBindPassword(DependencyObject element, bool value) => element.SetValue(BIND_PASSWORD_PROPERTY, value);

	private static void OnBindPasswordChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
	{
		if (sender is not PasswordBox passwordBox)
		{
			return;
		}

		passwordBox.PasswordChanged -= OnPasswordChanged;

		if ((bool)e.NewValue)
		{
			passwordBox.PasswordChanged += OnPasswordChanged;
		}
	}

	private static void OnBoundPasswordChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
	{
		if (sender is not PasswordBox passwordBox)
		{
			return;
		}

		bool isUpdating = (bool)passwordBox.GetValue(UPDATING_PASSWORD_PROPERTY);

		if (isUpdating)
		{
			return;
		}

		passwordBox.Password = e.NewValue?.ToString() ?? string.Empty;
	}

	private static void OnPasswordChanged(object sender, RoutedEventArgs e)
	{
		PasswordBox passwordBox = (PasswordBox)sender;

		passwordBox.SetValue(UPDATING_PASSWORD_PROPERTY, true);
		SetBoundPassword(passwordBox, passwordBox.Password);
		passwordBox.SetValue(UPDATING_PASSWORD_PROPERTY, false);
	}
}