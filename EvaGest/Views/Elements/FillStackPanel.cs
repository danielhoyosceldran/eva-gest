using System.Windows;
using System.Windows.Controls;
using EvaGest.Helpers;

namespace EvaGest.Views.Elements;

/// <summary>
/// A vertical stack where one child (marked <c>el:FillStackPanel.Fill="True"</c>) takes
/// whatever height the others leave free in <see cref="ViewportHeight"/>, so the stack
/// exactly fills it. Used by the sale dialog: the lines list grows into the free space
/// and scrolls by itself, while the client, search box and payment rows stay in view.
///
/// The panel sits inside the DialogShell's scrolling body, which measures its content
/// with unlimited height; that is why the height to fill comes in through
/// <see cref="ViewportHeight"/> (bound to the body's ViewportHeight) rather than from the
/// measure constraint. When even <see cref="FillMinHeight"/> does not fit — a laptop
/// screen — the stack grows taller than the viewport and the whole form scrolls, as
/// every other dialog does. Before the viewport is known (0) the fill child just takes
/// its natural height, like a plain StackPanel.
/// </summary>
public class FillStackPanel : Panel
{
    public static readonly DependencyProperty FillProperty = DependencyProperty.RegisterAttached(
        "Fill", typeof(bool), typeof(FillStackPanel),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsParentMeasure));

    public static bool GetFill(UIElement element) => (bool)element.GetValue(FillProperty);
    public static void SetFill(UIElement element, bool value) => element.SetValue(FillProperty, value);

    public static readonly DependencyProperty ViewportHeightProperty = DependencyProperty.Register(
        nameof(ViewportHeight), typeof(double), typeof(FillStackPanel),
        new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsMeasure));

    /// <summary>The height the stack should fill: the visible height of the scrolling
    /// area it sits in.</summary>
    public double ViewportHeight
    {
        get => (double)GetValue(ViewportHeightProperty);
        set => SetValue(ViewportHeightProperty, value);
    }

    public static readonly DependencyProperty FillMinHeightProperty = DependencyProperty.Register(
        nameof(FillMinHeight), typeof(double), typeof(FillStackPanel),
        new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsMeasure));

    /// <summary>The least the fill child is given, however little room is left. Below
    /// this a list is technically scrollable but useless.</summary>
    public double FillMinHeight
    {
        get => (double)GetValue(FillMinHeightProperty);
        set => SetValue(FillMinHeightProperty, value);
    }

    /// <summary>The height the fill child got in the last measure, reused by arrange.</summary>
    private double _fillHeight;

    protected override Size MeasureOverride(Size constraint)
    {
        var unlimited = new Size(constraint.Width, double.PositiveInfinity);
        double width = 0, fixedHeight = 0;
        UIElement? fill = null;

        // The fixed children first, at their natural height
        foreach (UIElement child in InternalChildren)
        {
            if (fill is null && GetFill(child)) { fill = child; continue; }
            child.Measure(unlimited);
            width = Math.Max(width, child.DesiredSize.Width);
            fixedHeight += child.DesiredSize.Height;
        }

        if (fill is null) return new Size(width, fixedHeight);

        // Then the fill child: what is left of the viewport, never less than its minimum
        fill.Measure(unlimited);
        _fillHeight = LayoutFit.FillHeight(ViewportHeight, fixedHeight, FillMinHeight, fill.DesiredSize.Height);
        fill.Measure(new Size(constraint.Width, _fillHeight));
        width = Math.Max(width, fill.DesiredSize.Width);

        return new Size(width, fixedHeight + _fillHeight);
    }

    protected override Size ArrangeOverride(Size arrangeSize)
    {
        // One child under the other, the fill child at the height measure gave it
        double top = 0;
        bool fillDone = false;
        foreach (UIElement child in InternalChildren)
        {
            bool isFill = !fillDone && GetFill(child);
            fillDone |= isFill;
            double height = isFill ? _fillHeight : child.DesiredSize.Height;
            child.Arrange(new Rect(0, top, arrangeSize.Width, height));
            top += height;
        }
        return arrangeSize;
    }
}
