using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using EvaGest.ViewModels.Dialogs;

namespace EvaGest.Views.Dialogs;

public partial class OwnerPinDialogView : UserControl
{
    public OwnerPinDialogView()
    {
        InitializeComponent();
        // Focus the first box the dialog actually shows.
        Loaded += (_, _) => Keyboard.Focus(
            DataContext is OwnerPinDialogViewModel { AsksCurrentPin: true } ? CurrentPinBox : NewPinBox);
    }

    /// <summary>PasswordBox.Password cannot be bound, so each box hands its value to the
    /// ViewModel here.</summary>
    private void OnPinChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is not OwnerPinDialogViewModel vm) return;

        vm.CurrentPin = CurrentPinBox.Password;
        vm.NewPin = NewPinBox.Password;
        vm.RepeatPin = RepeatPinBox.Password;
    }
}
