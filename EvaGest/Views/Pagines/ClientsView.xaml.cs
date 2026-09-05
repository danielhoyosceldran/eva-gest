using System.Windows.Controls;
using EvaGest.ViewModels.Pagines;

namespace EvaGest.Views.Pagines;

public partial class ClientsView : UserControl
{
    public ClientsView()
    {
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            if (DataContext is ClientsViewModel vm) await vm.Carregar();
        };
    }
}
