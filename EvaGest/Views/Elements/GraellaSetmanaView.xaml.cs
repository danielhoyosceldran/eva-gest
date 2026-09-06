using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using EvaGest.ViewModels.Elements;

namespace EvaGest.Views.Elements;

/// <summary>
/// The only code-behind the grid needs: the initial scroll offset, the once-a-minute
/// tick for the now-line, and the tile heights of the background rules. All three are
/// UI-thread concerns that must not live in the ViewModel, which the tests build
/// headless.
/// </summary>
public partial class GraellaSetmanaView : UserControl
{
    private readonly DispatcherTimer _rellotge = new() { Interval = TimeSpan.FromMinutes(1) };
    private bool _desplacamentAplicat;

    public GraellaSetmanaView()
    {
        InitializeComponent();
        _rellotge.Tick += (_, _) => Vm?.RefrescarAra();

        Loaded += AlCarregar;
        Unloaded += AlDescarregar;
        DataContextChanged += AlCanviarDataContext;
    }

    private GraellaSetmanaViewModel? Vm => DataContext as GraellaSetmanaViewModel;

    private void AlCarregar(object remitent, RoutedEventArgs e)
    {
        _rellotge.Start();
        AplicarMetriques();
        AplicarDesplacamentInicial();
    }

    private void AlDescarregar(object remitent, RoutedEventArgs e) => _rellotge.Stop();

    private void AlCanviarDataContext(object remitent, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is GraellaSetmanaViewModel antic)
            antic.PropertyChanged -= AlCanviarPropietat;
        if (e.NewValue is GraellaSetmanaViewModel nou)
            nou.PropertyChanged += AlCanviarPropietat;

        _desplacamentAplicat = false;
        AplicarMetriques();
    }

    private void AlCanviarPropietat(object? remitent, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(GraellaSetmanaViewModel.PixelsPerMinut)
                           or nameof(GraellaSetmanaViewModel.AlcadaHoraPx))
            AplicarMetriques();

        if (e.PropertyName is nameof(GraellaSetmanaViewModel.DesplacamentInicialPx))
            AplicarDesplacamentInicial();
    }

    /// <summary>
    /// Retunes the two tiled brushes to the configured granularity. Setting Viewport on
    /// a shared resource is what lets all seven columns share two brushes instead of
    /// carrying ~280 slot elements between them.
    /// </summary>
    private void AplicarMetriques()
    {
        if (Vm is not { } vm) return;

        if (Resources["LiniesFranja"] is DrawingBrush franja && vm.AlcadaSlotPx > 0)
            franja.Viewport = new Rect(0, 0, 8, vm.AlcadaSlotPx);

        if (Resources["LiniesHora"] is DrawingBrush hora && vm.AlcadaHoraPx > 0)
            hora.Viewport = new Rect(0, 0, 8, vm.AlcadaHoraPx);
    }

    /// <summary>Opens on the first working hour rather than at midnight. Applied once per
    /// ViewModel, so navigating between weeks keeps the position the user scrolled to.</summary>
    private void AplicarDesplacamentInicial()
    {
        if (_desplacamentAplicat || Vm is not { } vm || !IsLoaded) return;

        _desplacamentAplicat = true;
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded,
            () => Desplacador.ScrollToVerticalOffset(vm.DesplacamentInicialPx));
    }

    private void AlPremerTecla(object remitent, KeyEventArgs e)
    {
        if (Vm is not { } vm) return;

        switch (e.Key)
        {
            case Key.Left:  vm.MoureFocus(-1, 0); break;
            case Key.Right: vm.MoureFocus(1, 0); break;
            case Key.Up:    vm.MoureFocus(0, -1); break;
            case Key.Down:  vm.MoureFocus(0, 1); break;
            case Key.PageUp:   vm.MoureFocus(0, -60 / vm.MinutsSlot); break;
            case Key.PageDown: vm.MoureFocus(0, 60 / vm.MinutsSlot); break;
            case Key.Home:  vm.AnarAlPrimerSlot(); break;
            case Key.Enter:
            case Key.Space: vm.ActivarFocus(); break;
            default: return;
        }
        e.Handled = true;
    }
}
