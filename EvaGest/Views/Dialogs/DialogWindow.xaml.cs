using System.Windows;
using EvaGest.ViewModels.Dialogs;

namespace EvaGest.Views.Dialogs;

/// <summary>Generic host: content comes from a DataTemplate registered for the
/// dialog ViewModel's type (App.xaml), so this window never needs its own per-dialog XAML.</summary>
public partial class DialogWindow : Window
{
    public DialogWindow(DialogViewModelBase viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.Close += confirmed =>
        {
            DialogResult = confirmed;
            Close();
        };
    }
}
