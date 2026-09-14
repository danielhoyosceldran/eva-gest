using System.Windows.Controls;
using EvaGest.ViewModels.Pages;

namespace EvaGest.Views.Pages;

public partial class CatalogView : UserControl
{
    public CatalogView()
    {
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            if (DataContext is CatalogViewModel vm) await vm.Load();
        };
    }
}
