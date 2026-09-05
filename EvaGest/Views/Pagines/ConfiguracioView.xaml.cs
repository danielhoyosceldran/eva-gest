using System.Windows.Controls;
using EvaGest.ViewModels.Pagines;

namespace EvaGest.Views.Pagines;

public partial class ConfiguracioView : UserControl
{
    public ConfiguracioView()
    {
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            if (DataContext is ConfiguracioViewModel vm) await vm.Carregar();
        };
    }
}
