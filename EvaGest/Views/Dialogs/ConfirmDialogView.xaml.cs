using System.Windows.Controls;
using System.Windows.Input;

namespace EvaGest.Views.Dialogs;

public partial class ConfirmDialogView : UserControl
{
    public ConfirmDialogView()
    {
        InitializeComponent();
        // Focus on the answer that goes ahead, as the MessageBox did: Enter or Space takes it
        Loaded += (_, _) => Keyboard.Focus(ConfirmButton);
    }
}
