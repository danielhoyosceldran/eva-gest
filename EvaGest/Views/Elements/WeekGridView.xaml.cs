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
public partial class WeekGridView : UserControl
{
    private readonly DispatcherTimer _clock = new() { Interval = TimeSpan.FromMinutes(1) };
    private bool _offsetApplied;

    public WeekGridView()
    {
        InitializeComponent();
        _clock.Tick += (_, _) => Vm?.RefreshNow();

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        DataContextChanged += OnDataContextChanged;
    }

    private WeekGridViewModel? Vm => DataContext as WeekGridViewModel;

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _clock.Start();
        ApplyMetrics();
        ApplyInitialOffset();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e) => _clock.Stop();

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is WeekGridViewModel old)
            old.PropertyChanged -= OnViewModelPropertyChanged;
        if (e.NewValue is WeekGridViewModel newValue)
            newValue.PropertyChanged += OnViewModelPropertyChanged;

        _offsetApplied = false;
        ApplyMetrics();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(WeekGridViewModel.PixelsPerMinute)
                           or nameof(WeekGridViewModel.HourHeightPx))
            ApplyMetrics();

        if (e.PropertyName is nameof(WeekGridViewModel.InitialOffsetPx))
            ApplyInitialOffset();
    }

    /// <summary>
    /// Retunes the two tiled brushes to the configured granularity. Setting Viewport on
    /// a shared resource is what lets all seven columns share two brushes instead of
    /// carrying ~280 slot elements between them.
    /// </summary>
    private void ApplyMetrics()
    {
        if (Vm is not { } vm) return;

        if (Resources["LiniesFranja"] is DrawingBrush interval && vm.SlotHeightPx > 0)
            interval.Viewport = new Rect(0, 0, 8, vm.SlotHeightPx);

        if (Resources["LiniesHora"] is DrawingBrush time && vm.HourHeightPx > 0)
            time.Viewport = new Rect(0, 0, 8, vm.HourHeightPx);
    }

    /// <summary>Opens on the first working hour rather than at midnight. Applied once per
    /// ViewModel, so navigating between weeks keeps the position the user scrolled to.</summary>
    private void ApplyInitialOffset()
    {
        if (_offsetApplied || Vm is not { } vm || !IsLoaded) return;

        _offsetApplied = true;
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded,
            () => Scroller.ScrollToVerticalOffset(vm.InitialOffsetPx));
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (Vm is not { } vm) return;

        switch (e.Key)
        {
            case Key.Left:  vm.MoveFocus(-1, 0); break;
            case Key.Right: vm.MoveFocus(1, 0); break;
            case Key.Up:    vm.MoveFocus(0, -1); break;
            case Key.Down:  vm.MoveFocus(0, 1); break;
            case Key.PageUp:   vm.MoveFocus(0, -60 / vm.SlotMinutes); break;
            case Key.PageDown: vm.MoveFocus(0, 60 / vm.SlotMinutes); break;
            case Key.Home:  vm.GoToFirstSlot(); break;
            case Key.Enter:
            case Key.Space: vm.ActivateFocus(); break;
            default: return;
        }
        e.Handled = true;
    }
}
