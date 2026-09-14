using System.Windows.Controls;
using EvaGest.ViewModels.Pages;

namespace EvaGest.Views.Pages;

public partial class ReportsView : UserControl
{
    public ReportsView()
    {
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            if (DataContext is ReportsViewModel vm) await vm.Load();
        };
    }
}
