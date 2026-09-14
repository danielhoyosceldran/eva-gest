using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using EvaGest.ViewModels.Elements;

namespace EvaGest.Helpers;

/// <summary>
/// Turns clicks and hovers on the empty space of a day column into (date, slot).
/// An attached property rather than code-behind, so the same three handlers serve all
/// seven columns and unhook themselves when a column is recycled.
///
/// Note the Canvas must have Background="Transparent": with a null background WPF does
/// not hit-test it at all and clicks on empty space are simply lost.
/// </summary>
public static class GridInteraction
{
    public static readonly DependencyProperty DayProperty = DependencyProperty.RegisterAttached(
        "Day", typeof(GridDayViewModel), typeof(GridInteraction),
        new PropertyMetadata(null, OnDayChanged));

    public static GridDayViewModel? GetDay(DependencyObject d) => (GridDayViewModel?)d.GetValue(DayProperty);
    public static void SetDay(DependencyObject d, GridDayViewModel? value) => d.SetValue(DayProperty, value);

    private static void OnDayChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not Canvas canvas) return;

        canvas.MouseLeftButtonUp -= OnClick;
        canvas.MouseMove -= OnMouseMove;
        canvas.MouseLeave -= OnExit;

        if (e.NewValue is null) return;

        canvas.MouseLeftButtonUp += OnClick;
        canvas.MouseMove += OnMouseMove;
        canvas.MouseLeave += OnExit;
    }

    private static void OnClick(object sender, MouseButtonEventArgs e)
    {
        // An appointment block is a Button and marks the event handled, so clicking one
        // never also books a new appointment underneath it.
        if (e.Handled) return;
        var canvas = (Canvas)sender;
        GetDay(canvas)?.ClickAtPosition(e.GetPosition(canvas).Y);
    }

    private static void OnMouseMove(object sender, MouseEventArgs e)
    {
        var canvas = (Canvas)sender;
        var day = GetDay(canvas);
        if (day is null) return;

        if (e.OriginalSource is DependencyObject origin && OverAnAppointment(origin, canvas)) day.StopHover();
        else day.HoverAtPosition(e.GetPosition(canvas).Y);
    }

    private static void OnExit(object sender, MouseEventArgs e)
        => GetDay((Canvas)sender)?.StopHover();

    /// <summary>MouseMove does not bubble as "handled", so the hover preview has to ask
    /// whether the pointer is actually over a block rather than over empty space.</summary>
    private static bool OverAnAppointment(DependencyObject origin, Canvas canvas)
    {
        for (var current = origin; current is not null && current != canvas;)
        {
            if (current is Button) return true;
            current = current is System.Windows.Media.Visual or System.Windows.Media.Media3D.Visual3D
                ? System.Windows.Media.VisualTreeHelper.GetParent(current)
                : LogicalTreeHelper.GetParent(current);
        }
        return false;
    }
}
