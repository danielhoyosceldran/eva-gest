using System.Windows;
using System.Windows.Controls;

namespace EvaGest.Views.Elements;

/// <summary>
/// A page toolbar of two parts: what you look at (the period, the week arrows) on the
/// left and what you do (+ Nova cita, + Entrada, + Sortida) on the right. When both fit
/// they share one row, the actions pushed to the right edge; when they do not, the
/// actions move to a second row, left-aligned under the first part, instead of running
/// off the edge of the page. At the main window's minimum width (and on a 1280x800
/// laptop) the Agenda lost "+ Nova cita" and Caixa "+ Sortida" that way.
///
/// Takes exactly two children: the first is the left part, the second the actions. The
/// first is given whatever width the actions leave, so a part that adapts to its width
/// (the Agenda's range title, see <c>ToolbarTitle</c>) still can.
/// </summary>
public class FoldingToolbar : Panel
{
    public static readonly DependencyProperty RowGapProperty = DependencyProperty.Register(
        nameof(RowGap), typeof(double), typeof(FoldingToolbar),
        new FrameworkPropertyMetadata(8.0, FrameworkPropertyMetadataOptions.AffectsMeasure));

    /// <summary>Space between the two rows once the actions have moved down.</summary>
    public double RowGap
    {
        get => (double)GetValue(RowGapProperty);
        set => SetValue(RowGapProperty, value);
    }

    /// <summary>Set by the last measure: the actions are on a row of their own.</summary>
    private bool _folded;

    protected override Size MeasureOverride(Size constraint)
    {
        if (InternalChildren.Count != 2) return MeasureAnyOther(constraint);
        UIElement left = InternalChildren[0], actions = InternalChildren[1];

        // Each part's natural width, with no limit: one row only if both fit as they are
        var unlimited = new Size(double.PositiveInfinity, constraint.Height);
        left.Measure(unlimited);
        actions.Measure(unlimited);
        _folded = left.DesiredSize.Width + actions.DesiredSize.Width > constraint.Width;

        if (_folded)
        {
            // Two rows: each part gets the whole width
            left.Measure(new Size(constraint.Width, constraint.Height));
            actions.Measure(new Size(constraint.Width, constraint.Height));
            return new Size(
                Math.Min(constraint.Width, Math.Max(left.DesiredSize.Width, actions.DesiredSize.Width)),
                left.DesiredSize.Height + RowGap + actions.DesiredSize.Height);
        }

        // One row: the left part takes what the actions leave
        left.Measure(new Size(Math.Max(0, constraint.Width - actions.DesiredSize.Width), constraint.Height));
        return new Size(left.DesiredSize.Width + actions.DesiredSize.Width,
                        Math.Max(left.DesiredSize.Height, actions.DesiredSize.Height));
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (InternalChildren.Count != 2) return ArrangeAnyOther(finalSize);
        UIElement left = InternalChildren[0], actions = InternalChildren[1];

        if (_folded)
        {
            left.Arrange(new Rect(0, 0, finalSize.Width, left.DesiredSize.Height));
            actions.Arrange(new Rect(0, left.DesiredSize.Height + RowGap,
                                     Math.Min(finalSize.Width, actions.DesiredSize.Width), actions.DesiredSize.Height));
        }
        else
        {
            double actionsWidth = actions.DesiredSize.Width;
            left.Arrange(new Rect(0, 0, Math.Max(0, finalSize.Width - actionsWidth), finalSize.Height));
            actions.Arrange(new Rect(finalSize.Width - actionsWidth, 0, actionsWidth, finalSize.Height));
        }
        return finalSize;
    }

    // Anything but two children is a mistake in the XAML; lay them out stacked rather
    // than throw in front of the user.
    private Size MeasureAnyOther(Size constraint)
    {
        double width = 0, height = 0;
        foreach (UIElement child in InternalChildren)
        {
            child.Measure(constraint);
            width = Math.Max(width, child.DesiredSize.Width);
            height += child.DesiredSize.Height;
        }
        return new Size(width, height);
    }

    private Size ArrangeAnyOther(Size finalSize)
    {
        double top = 0;
        foreach (UIElement child in InternalChildren)
        {
            child.Arrange(new Rect(0, top, finalSize.Width, child.DesiredSize.Height));
            top += child.DesiredSize.Height;
        }
        return finalSize;
    }
}
