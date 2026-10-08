using System.Windows.Controls;
using EvaGest.Helpers;
using EvaGest.ViewModels.Pages;

namespace EvaGest.Views.Pages;

public partial class AgendaView : UserControl
{
    /// <summary>Share of the page the day detail panel takes, between
    /// DayPanelMinWidth and DayPanelMaxWidth.</summary>
    private const double DayPanelShare = 0.35;

    public AgendaView()
    {
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            if (DataContext is AgendaViewModel vm) await vm.Load();
        };

        Body.SizeChanged += (_, _) => SizeDayPanel();
    }

    /// <summary>
    /// The day panel was a fixed 400 DIP: on a laptop screen that left the week grid
    /// next to it about 50 DIP per day. It now takes a share of the page, so the grid
    /// keeps columns wide enough to read and a wide screen still gets the full panel.
    /// </summary>
    private void SizeDayPanel()
    {
        DayPanel.Width = LayoutFit.Flexible(Body.ActualWidth * DayPanelShare,
            (double)FindResource("DayPanelMinWidth"),
            (double)FindResource("DayPanelMaxWidth"));
    }
}
