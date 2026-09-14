using System.Windows.Controls;
using EvaGest.ViewModels.Pages;

namespace EvaGest.Views.Pages;

public partial class AgendaView : UserControl
{
    public AgendaView()
    {
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            if (DataContext is AgendaViewModel vm) await vm.Load();
        };
    }
}
