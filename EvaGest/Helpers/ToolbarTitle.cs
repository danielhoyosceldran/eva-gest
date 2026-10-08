using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace EvaGest.Helpers;

/// <summary>
/// For a one-row toolbar Grid whose title sits between its buttons — the week range
/// between ‹ and › in the Agenda and in the appointment dialog's picker. Set
/// <c>ToolbarTitle.Column</c> on the Grid to the title's column, give the Grid a second
/// Auto row, and bind the title's Text with <c>NotifyOnTargetUpdated=True</c>.
///
/// On a laptop screen the buttons used to leave the title no room and it was squeezed
/// to nothing. Whenever the toolbar resizes or the text changes, this checks whether
/// the other columns plus the text still fit; if not, the title moves to the second row,
/// across the whole toolbar, and it moves back once there is room again. The other
/// columns are sized by their own content only, so where the title sits never changes
/// the answer and it cannot flip back and forth.
/// </summary>
public static class ToolbarTitle
{
    public static readonly DependencyProperty ColumnProperty = DependencyProperty.RegisterAttached(
        "Column", typeof(int), typeof(ToolbarTitle), new PropertyMetadata(-1, OnColumnChanged));

    public static int GetColumn(Grid grid) => (int)grid.GetValue(ColumnProperty);
    public static void SetColumn(Grid grid, int value) => grid.SetValue(ColumnProperty, value);

    /// <summary>The title's own margin, remembered so it can be put back.</summary>
    private static readonly DependencyProperty InlineMarginProperty = DependencyProperty.RegisterAttached(
        "InlineMargin", typeof(Thickness?), typeof(ToolbarTitle), new PropertyMetadata(null));

    /// <summary>The gap above the title once it has moved under the buttons.</summary>
    private static readonly Thickness FoldedMargin = new(0, 8, 0, 0);

    private static void OnColumnChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not Grid grid) return;
        grid.SizeChanged -= OnSizeChanged;
        if ((int)e.NewValue >= 0) grid.SizeChanged += OnSizeChanged;
    }

    private static void OnSizeChanged(object sender, SizeChangedEventArgs e) => Place((Grid)sender);

    private static void OnTitleUpdated(object? sender, DataTransferEventArgs e)
    {
        if (sender is TextBlock { Parent: Grid grid }) Place(grid);
    }

    /// <summary>Puts the title inline or under the buttons, whichever fits.</summary>
    public static void Place(Grid grid)
    {
        int column = GetColumn(grid);
        if (column < 0 || grid.ActualWidth <= 0) return;

        var title = Title(grid, column);
        if (title is null) return;

        double others = 0;
        for (int i = 0; i < grid.ColumnDefinitions.Count; i++)
            if (i != column) others += grid.ColumnDefinitions[i].ActualWidth;

        var inline = (Thickness)title.GetValue(InlineMarginProperty)!;
        bool folded = others + TextWidth(title) + inline.Left + inline.Right > grid.ActualWidth;

        Grid.SetRow(title, folded ? 1 : 0);
        Grid.SetColumn(title, folded ? 0 : column);
        Grid.SetColumnSpan(title, folded ? Math.Max(1, grid.ColumnDefinitions.Count) : 1);
        title.Margin = folded ? FoldedMargin : inline;
    }

    /// <summary>
    /// Finds the title (the TextBlock in the title column, or the one already moved under
    /// the buttons) and, the first time, remembers its margin and starts listening for
    /// text changes. Found lazily: when the attached property is set while the XAML is
    /// read, the title has not been added to the Grid yet.
    /// </summary>
    private static TextBlock? Title(Grid grid, int column)
    {
        var title = grid.Children.OfType<TextBlock>().FirstOrDefault(t =>
            t.GetValue(InlineMarginProperty) is not null
            || (Grid.GetColumn(t) == column && Grid.GetRow(t) == 0));
        if (title is null) return null;

        if (title.GetValue(InlineMarginProperty) is null)
        {
            title.SetValue(InlineMarginProperty, title.Margin);
            title.AddHandler(Binding.TargetUpdatedEvent, new EventHandler<DataTransferEventArgs>(OnTitleUpdated));
            // The toolbar keeps its width when a button beside the title grows (its text
            // arriving through a binding, « » appearing in the three-day view); the title
            // is what gets squeezed then, so its own resize triggers a fresh check too.
            // The check does not depend on where the title sits, so this settles at once.
            title.SizeChanged += OnTitleResized;
        }
        return title;
    }

    private static void OnTitleResized(object sender, SizeChangedEventArgs e)
    {
        if (sender is TextBlock { Parent: Grid grid }) Place(grid);
    }

    /// <summary>The width one line of the text needs, in the TextBlock's own font.</summary>
    private static double TextWidth(TextBlock block)
    {
        var text = new FormattedText(block.Text ?? string.Empty, CultureInfo.CurrentUICulture,
            block.FlowDirection,
            new Typeface(block.FontFamily, block.FontStyle, block.FontWeight, block.FontStretch),
            block.FontSize, Brushes.Black, VisualTreeHelper.GetDpi(block).PixelsPerDip);
        return text.WidthIncludingTrailingWhitespace;
    }
}
