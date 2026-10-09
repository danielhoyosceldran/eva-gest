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
        // Only the one closest to the pointer that has something to scroll should
        // move. Skipping the ones that can't matters: a DataGrid laid out at full
        // height inside a page still has its own inner ScrollViewer, and if the
        // wheel were left to it, WPF's ScrollViewer.OnMouseWheel would mark the
        // event handled without moving anything, and the page would never scroll.
        if (NearestScrollable(e.OriginalSource as DependencyObject) != sv) return;

        e.Handled = true;
        sv.ScrollToVerticalOffset(sv.VerticalOffset - e.Delta * Factor);
    }

    /// <summary>
    /// Walks up from where the wheel happened to the first ScrollViewer that can
    /// actually scroll vertically, or null when none can.
    /// </summary>
    private static ScrollViewer? NearestScrollable(DependencyObject? origin)
    {
        while (origin is not null)
        {
            if (origin is ScrollViewer { ScrollableHeight: > 0 } sv) return sv;

            origin = origin is Visual or System.Windows.Media.Media3D.Visual3D
                ? VisualTreeHelper.GetParent(origin)
                : LogicalTreeHelper.GetParent(origin);
        }

        return null;
    }
}
