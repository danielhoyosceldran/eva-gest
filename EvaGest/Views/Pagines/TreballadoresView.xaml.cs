using System.Windows.Controls;
using EvaGest.ViewModels.Pagines;

namespace EvaGest.Views.Pagines;

public partial class TreballadoresView : UserControl
{
    public TreballadoresView()
    {
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            if (DataContext is TreballadoresViewModel vm) await vm.Carregar();
        };
    }
}
