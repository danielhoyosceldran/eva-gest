using System.Windows;
using EvaGest.ViewModels.Dialegs;

namespace EvaGest.Views.Dialegs;

/// <summary>Generic host: content comes from a DataTemplate registered for the
/// dialog ViewModel's type (App.xaml), so this window never needs its own per-dialog XAML.</summary>
public partial class DialogWindow : Window
{
    public DialogWindow(DialegViewModelBase viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.Tancar += confirmat =>
        {
            DialogResult = confirmat;
            Close();
        };
    }
}
