using System.Windows.Controls;
using EvaGest.ViewModels.Pagines;

namespace EvaGest.Views.Pagines;

public partial class IniciView : UserControl
{
    public IniciView()
    {
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            if (DataContext is IniciViewModel vm) await vm.Carregar();
        };
    }
}
