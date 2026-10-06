using System.Windows;
using System.Windows.Controls;

namespace DataGenerator.Infrastructure;

public static class PasswordBoxBinding
{
	#region FIELDS
	#region PUBLIC
	public static readonly DependencyProperty BOUND_PASSWORD_PROPERTY;

	public static readonly DependencyProperty BIND_PASSWORD_PROPERTY;
	#endregion PUBLIC

	#region PRIVATE
	private static readonly DependencyProperty UPDATING_PASSWORD_PROPERTY;
	#endregion PRIVATE
	#endregion FIELDS

	#region CONSTRUCTORS
	#region STATIC
	/// <summary>
	///	Sets the default values of the static fields and properties of <see cref="PasswordBoxBinding"/>.
	/// </summary>
	static PasswordBoxBinding()
	{
		BOUND_PASSWORD_PROPERTY =
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


		BIND_PASSWORD_PROPERTY =
			DependencyProperty.RegisterAttached(
				"BindPassword",
				typeof(bool),
				typeof(PasswordBoxBinding),
				new PropertyMetadata(false, OnBindPasswordChanged)
			);


		UPDATING_PASSWORD_PROPERTY =
			DependencyProperty.RegisterAttached(
				"UpdatingPassword",
				typeof(bool),
				typeof(PasswordBoxBinding),
				new PropertyMetadata(false)
			);
	}
	#endregion STATIC
	#endregion CONSTRUCTORS

	#region METHODS
	#region PUBLIC
	/// <summary>
	///	Gets the password value mirrored from a password box.
	/// </summary>
	/// <param name="element">
	///	The element that stores the attached password value.
	/// </param>
	/// <returns>
	///	The bound password string.
	/// </returns>
	public static string GetBoundPassword(DependencyObject element) => (string)element.GetValue(BOUND_PASSWORD_PROPERTY);

	/// <summary>
	///	Sets the password value mirrored to a password box.
	/// </summary>
	/// <param name="element">
	///	The element that stores the attached password value.
	/// </param>
	/// <param name="value">
	///	The password string to mirror into the password box.
	/// </param>
	public static void SetBoundPassword(DependencyObject element, string value) => element.SetValue(BOUND_PASSWORD_PROPERTY, value);

	/// <summary>
	///	Gets whether a password box mirrors its password through the bound password property.
	/// </summary>
	/// <param name="element">
	///	The element that stores the attached setting.
	/// </param>
	/// <returns>
	///	<see langword="true"/> when password binding is enabled; otherwise <see langword="false"/>.
	/// </returns>
	public static bool GetBindPassword(DependencyObject element) => (bool)element.GetValue(BIND_PASSWORD_PROPERTY);

	/// <summary>
	///	Sets whether a password box mirrors its password through the bound password property.
	/// </summary>
	/// <param name="element">
	///	The element that stores the attached setting.
	/// </param>
	/// <param name="value">
	///	Whether the password box should update the bound password property as the user types.
	/// </param>
	public static void SetBindPassword(DependencyObject element, bool value) => element.SetValue(BIND_PASSWORD_PROPERTY, value);
	#endregion PUBLIC

	#region PRIVATE
	/// <summary>
	///	Adds or removes the password changed handler when password binding is enabled or disabled.
	/// </summary>
	/// <param name="sender">
	///	The element whose attached setting changed.
	/// </param>
	/// <param name="e">
	///	The old and new binding enabled values.
	/// </param>
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

	/// <summary>
	///	Copies a new bound password value into the password box unless the change came from that box.
	/// </summary>
	/// <param name="sender">
	///	The element whose bound password changed.
	/// </param>
	/// <param name="e">
	///	The old and new password values.
	/// </param>
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

	/// <summary>
	///	Copies the password box's current password into the attached bound password property.
	/// </summary>
	/// <param name="sender">
	///	The password box whose password changed.
	/// </param>
	/// <param name="e">
	///	The routed event data supplied by WPF.
	/// </param>
	private static void OnPasswordChanged(object sender, RoutedEventArgs e)
	{
		PasswordBox passwordBox = (PasswordBox)sender;

		passwordBox.SetValue(UPDATING_PASSWORD_PROPERTY, true);
		SetBoundPassword(passwordBox, passwordBox.Password);
		passwordBox.SetValue(UPDATING_PASSWORD_PROPERTY, false);
	}
	#endregion PRIVATE
	#endregion METHODS
}