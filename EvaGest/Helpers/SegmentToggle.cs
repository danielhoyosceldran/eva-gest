using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace EvaGest.Helpers;

/// <summary>
/// Set <c>SegmentToggle.Deselectable="True"</c> on a single-choice <c>SegmentList</c>
/// whose choice is optional: clicking the segment already chosen clears the choice, so
/// the row works like a set of toggle buttons where at most one is pressed. A plain
/// ListBox only clears it on Ctrl+click, which nobody at the counter would guess.
/// </summary>
public static class SegmentToggle
{
    public static readonly DependencyProperty DeselectableProperty = DependencyProperty.RegisterAttached(
        "Deselectable", typeof(bool), typeof(SegmentToggle), new PropertyMetadata(false, OnDeselectableChanged));

    public static bool GetDeselectable(ListBox list) => (bool)list.GetValue(DeselectableProperty);
    public static void SetDeselectable(ListBox list, bool value) => list.SetValue(DeselectableProperty, value);

    private static void OnDeselectableChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not ListBox list) return;
        list.PreviewMouseLeftButtonDown -= OnPreviewMouseLeftButtonDown;
        if ((bool)e.NewValue) list.PreviewMouseLeftButtonDown += OnPreviewMouseLeftButtonDown;
    }

    /// <summary>
    /// Preview, so the click is caught before the item selects itself again. A click on
    /// a segment not yet chosen is left alone and selects it as usual.
    /// </summary>
    private static void OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not ListBox list || ItemUnder(e.OriginalSource as DependencyObject) is not { IsSelected: true } item)
            return;

        list.SelectedItem = null;
        item.Focus();
        e.Handled = true;
    }

    /// <summary>The segment the click landed in, walking up from whatever was hit
    /// (the text, the border); null for a click between segments.</summary>
    private static ListBoxItem? ItemUnder(DependencyObject? hit)
    {
        while (hit is not null and not ListBoxItem)
            hit = hit is Visual ? VisualTreeHelper.GetParent(hit) : LogicalTreeHelper.GetParent(hit);
        return hit as ListBoxItem;
    }
}
