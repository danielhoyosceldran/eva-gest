using System.Windows;
using System.Windows.Controls.Primitives;
using EvaGest.Helpers;

namespace EvaGest.Views.Elements;

/// <summary>
/// A UniformGrid that picks its own column count: as many equal cells of at least
/// <see cref="MinItemWidth"/> as fit across the space it is given, then wraps onto
/// more rows. Used for the summary cards, quick actions and filters, which used to sit
/// on one fixed row and clipped their figures on a laptop screen.
///
/// Collapsed children are not counted, matching UniformGrid itself, so a row of cards
/// that hides the money cards when owner mode is closed still closes up.
/// </summary>
public class WrappingUniformGrid : UniformGrid
{
    public static readonly DependencyProperty MinItemWidthProperty = DependencyProperty.Register(
        nameof(MinItemWidth), typeof(double), typeof(WrappingUniformGrid),
        new FrameworkPropertyMetadata(200.0, FrameworkPropertyMetadataOptions.AffectsMeasure));

    /// <summary>The narrowest a cell may get before a card moves to the next row.</summary>
    public double MinItemWidth
    {
        get => (double)GetValue(MinItemWidthProperty);
        set => SetValue(MinItemWidthProperty, value);
    }

    /// <summary>
    /// Sets <see cref="UniformGrid.Columns"/> from the width on offer, then lets
    /// UniformGrid lay the cells out as usual. Rows stays 0 so UniformGrid works out
    /// how many it needs. Setting Columns here is safe: the invalidation it raises is
    /// absorbed by the measure pass already running.
    /// </summary>
    protected override Size MeasureOverride(Size constraint)
    {
        int visible = 0;
        foreach (UIElement child in InternalChildren)
            if (child.Visibility != Visibility.Collapsed) visible++;

        int columns = LayoutFit.Columns(constraint.Width, MinItemWidth, visible);
        if (Columns != columns) Columns = columns;
        if (Rows != 0) Rows = 0;

        return base.MeasureOverride(constraint);
    }
}
