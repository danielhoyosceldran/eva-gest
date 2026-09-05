using System.Windows.Controls;
using EvaGest.ViewModels.Pagines;

namespace EvaGest.Views.Pagines;

public partial class CatalegView : UserControl
{
    public CatalegView()
    {
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            if (DataContext is CatalegViewModel vm) await vm.Carregar();
        };
    }
}
