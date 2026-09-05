using System.Windows.Controls;
using EvaGest.ViewModels.Pagines;

namespace EvaGest.Views.Pagines;

public partial class AgendaView : UserControl
{
    public AgendaView()
    {
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            if (DataContext is AgendaViewModel vm) await vm.Carregar();
        };
    }
}
