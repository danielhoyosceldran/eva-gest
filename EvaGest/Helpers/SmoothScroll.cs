using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace EvaGest.Helpers;

/// <summary>
/// A precision trackpad sends a stream of wheel messages, and WPF answers each one
/// with three full lines of scroll, so the page flies past. Registering a class
/// handler once at startup slows every ScrollViewer in the app down instead of
/// having to opt each view in.
/// </summary>
public static class SmoothScroll
{
    /// <summary>Pixels scrolled per wheel unit. WPF's own step is roughly 0.4.</summary>
    private const double Factor = 0.15;

    public static void Activate()
    {
        EventManager.RegisterClassHandler(
            typeof(ScrollViewer),
            UIElement.PreviewMouseWheelEvent,
            new MouseWheelEventHandler(OnMouseWheel));
    }

    private static void OnMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (e.Handled) return;

        var sv = (ScrollViewer)sender;

        // PreviewMouseWheel tunnels, so the outermost ScrollViewer is called first.
        // Only the one closest to the pointer should move.
        if (NextMonth(e.OriginalSource as DependencyObject) != sv) return;

        // Nothing to scroll here: leave the event alone so an outer ScrollViewer
        // (or a control that uses the wheel for something else) still gets it.
        if (sv.ScrollableHeight <= 0) return;

        e.Handled = true;
        sv.ScrollToVerticalOffset(sv.VerticalOffset - e.Delta * Factor);
    }

    private static ScrollViewer? NextMonth(DependencyObject? origin)
    {
        while (origin is not null)
        {
            if (origin is ScrollViewer sv) return sv;

            origin = origin is Visual or System.Windows.Media.Media3D.Visual3D
                ? VisualTreeHelper.GetParent(origin)
                : LogicalTreeHelper.GetParent(origin);
        }

        return null;
    }
}
