using System.Windows.Controls;
using EvaGest.ViewModels.Pagines;

namespace EvaGest.Views.Pagines;

public partial class CaixaView : UserControl
{
    public CaixaView()
    {
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            if (DataContext is CaixaViewModel vm) await vm.Carregar();
        };
    }
}
