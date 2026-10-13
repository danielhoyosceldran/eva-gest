using System.Windows.Controls;
using System.Windows.Input;
using EvaGest.ViewModels.Dialogs;

namespace EvaGest.Views.Dialogs;

public partial class ConfirmDialogView : UserControl
{
    public ConfirmDialogView()
    {
        InitializeComponent();
        // Focus on the answer that goes ahead, as the MessageBox did: Enter or Space takes
        // it. Not when going ahead loses something: Enter, pressed out of habit, used to
        // delete or discard (B-34), so the focus starts on the way back.
        Loaded += (_, _) => Keyboard.Focus(FirstFocus);
    }

    /// <summary>The button the keyboard starts on.</summary>
    public Button FirstFocus
        => DataContext is ConfirmDialogViewModel { IsDestructive: true } ? CancelButton : ConfirmButton;
}
