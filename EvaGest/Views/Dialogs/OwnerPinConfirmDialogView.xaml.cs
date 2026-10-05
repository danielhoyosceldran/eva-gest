using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using EvaGest.ViewModels.Dialogs;

namespace EvaGest.Views.Dialogs;

public partial class OwnerPinConfirmDialogView : UserControl
{
    public OwnerPinConfirmDialogView()
    {
        InitializeComponent();
        // Straight into the PIN box, so the owner can type the PIN and press Enter.
        Loaded += (_, _) => Keyboard.Focus(PinBox);
    }

    /// <summary>PasswordBox.Password is deliberately not a dependency property, so it
    /// cannot be bound; the value is handed to the ViewModel here instead.</summary>
    private void OnPinChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is OwnerPinConfirmDialogViewModel vm) vm.Pin = PinBox.Password;
    }
}
