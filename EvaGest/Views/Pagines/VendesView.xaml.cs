using System.Windows.Controls;
using EvaGest.ViewModels.Pagines;

namespace EvaGest.Views.Pagines;

public partial class VendesView : UserControl
{
    public VendesView()
    {
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            if (DataContext is VendesViewModel vm) await vm.Carregar();
        };
    }
}
