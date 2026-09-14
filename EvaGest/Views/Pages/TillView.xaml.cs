using System.Windows.Controls;
using EvaGest.ViewModels.Pages;

namespace EvaGest.Views.Pages;

public partial class TillView : UserControl
{
    public TillView()
    {
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            if (DataContext is TillViewModel vm) await vm.Load();
        };
    }
}
