using System.Windows.Controls;
using EvaGest.ViewModels.Pages;

namespace EvaGest.Views.Pages;

public partial class ClientsView : UserControl
{
    public ClientsView()
    {
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            if (DataContext is ClientsViewModel vm) await vm.Load();
        };
    }
}
