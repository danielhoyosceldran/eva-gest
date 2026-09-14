using System.Windows.Controls;
using EvaGest.ViewModels.Pages;

namespace EvaGest.Views.Pages;

public partial class WorkersView : UserControl
{
    public WorkersView()
    {
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            if (DataContext is WorkersViewModel vm) await vm.Load();
        };
    }
}
