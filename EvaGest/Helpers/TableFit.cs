using System.Windows;
using System.Windows.Controls;

namespace EvaGest.Helpers;

/// <summary>
/// Lets a DataGrid drop its least important columns on a narrow screen instead of
/// squeezing the flexible ones to nothing or scrolling sideways.
///
/// The WPF DataGrid, when its fixed columns already fill the screen, made room by
/// crushing the star columns — on a laptop the sales list showed no client and no
/// concept at all. With <c>TableFit.IsEnabled="True"</c> on the grid and a
/// <c>TableFit.HideOrder</c> on each column it can do without (1 goes first, then 2…),
/// every resize shows all columns again and hides them in that order until the rest
/// fit. A flexible column counts at its MinWidth, so give the ones that must stay
/// readable a MinWidth.
/// </summary>
public static class TableFit
{
    public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterAttached(
        "IsEnabled", typeof(bool), typeof(TableFit), new PropertyMetadata(false, OnIsEnabledChanged));

    public static bool GetIsEnabled(DataGrid grid) => (bool)grid.GetValue(IsEnabledProperty);
    public static void SetIsEnabled(DataGrid grid, bool value) => grid.SetValue(IsEnabledProperty, value);

    /// <summary>When this column goes on a narrow screen: 1 first, then 2, and so on.
    /// 0, the default, means never.</summary>
    public static readonly DependencyProperty HideOrderProperty = DependencyProperty.RegisterAttached(
        "HideOrder", typeof(int), typeof(TableFit), new PropertyMetadata(0));

    public static int GetHideOrder(DataGridColumn column) => (int)column.GetValue(HideOrderProperty);
    public static void SetHideOrder(DataGridColumn column, int value) => column.SetValue(HideOrderProperty, value);

    private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not DataGrid grid) return;
        if ((bool)e.NewValue) grid.SizeChanged += OnSizeChanged;
        else grid.SizeChanged -= OnSizeChanged;
    }

    private static void OnSizeChanged(object sender, SizeChangedEventArgs e) => Fit((DataGrid)sender);

    /// <summary>
    /// Shows every column, then hides the optional ones in order until the visible ones
    /// fit. The vertical scroll bar's width is kept free, since a long list shows one.
    /// Hiding a column never changes the grid's own size, so this cannot set off another
    /// SizeChanged.
    /// </summary>
    public static void Fit(DataGrid grid)
    {
        double available = grid.ActualWidth - grid.BorderThickness.Left - grid.BorderThickness.Right
                           - SystemParameters.VerticalScrollBarWidth;
        if (available <= 0) return;

        var optional = grid.Columns.Where(c => GetHideOrder(c) > 0).OrderBy(GetHideOrder).ToList();
        foreach (var column in optional) column.Visibility = Visibility.Visible;
        foreach (var column in optional)
        {
            if (WidthNeeded(grid) <= available) break;
            column.Visibility = Visibility.Collapsed;
        }
    }

    /// <summary>What the visible columns need: a fixed column its width, a flexible one
    /// its minimum, an automatic one what it currently takes.</summary>
    private static double WidthNeeded(DataGrid grid)
        => grid.Columns
            .Where(c => c.Visibility == Visibility.Visible)
            .Sum(c => c.Width.IsAbsolute ? c.Width.Value
                    : c.Width.IsStar ? c.MinWidth
                    : c.ActualWidth);
}
