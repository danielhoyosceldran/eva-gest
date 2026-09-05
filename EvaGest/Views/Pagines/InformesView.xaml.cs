using System.Windows.Controls;
using EvaGest.ViewModels.Pagines;

namespace EvaGest.Views.Pagines;

public partial class InformesView : UserControl
{
    public InformesView()
    {
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            if (DataContext is InformesViewModel vm) await vm.Carregar();
        };
    }
}
