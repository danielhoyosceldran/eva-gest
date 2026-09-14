using System.Windows.Controls;
using EvaGest.ViewModels.Pages;

namespace EvaGest.Views.Pages;

public partial class HomeView : UserControl
{
    public HomeView()
    {
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            if (DataContext is HomeViewModel vm) await vm.Load();
        };
    }
}
