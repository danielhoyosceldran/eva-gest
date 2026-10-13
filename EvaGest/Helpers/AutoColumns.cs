using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace EvaGest.Helpers;

/// <summary>
/// Keeps a DataGrid's automatic columns (the row actions: Editar, Eliminar…) as wide as
/// their buttons when the rows change.
///
/// The WPF DataGrid sizes an "Auto" column from the cells it has measured so far and,
/// next to star columns, never widens it again: the star columns have already taken the
/// rest of the row. A table first shown empty (a new install) kept its actions column at
/// the width of its empty header, about 20 DIP, so the buttons were cut off. With
/// <c>AutoColumns.RefitOnNewRows="True"</c> (set for every grid by the DataGrid style)
/// each change of rows resets those columns once the new rows are laid out, so they are
/// measured again from the cells actually there.
/// </summary>
public static class AutoColumns
{
    public static readonly DependencyProperty RefitOnNewRowsProperty = DependencyProperty.RegisterAttached(
        "RefitOnNewRows", typeof(bool), typeof(AutoColumns), new PropertyMetadata(false, OnRefitOnNewRowsChanged));

    public static bool GetRefitOnNewRows(DataGrid grid) => (bool)grid.GetValue(RefitOnNewRowsProperty);
    public static void SetRefitOnNewRows(DataGrid grid, bool value) => grid.SetValue(RefitOnNewRowsProperty, value);

    private static void OnRefitOnNewRowsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not DataGrid grid) return;
        // Items is the grid's own view of its rows: it reports a change whether the bound
        // collection is refilled or replaced (a new ItemsSource arrives as a Reset).
        INotifyCollectionChanged items = grid.Items;
        if ((bool)e.NewValue) items.CollectionChanged += (_, _) => Schedule(grid);
        // Nothing to undo: the style sets it once for the grid's whole life
    }

    /// <summary>Deferred until the new rows have been laid out (Loaded priority runs after
    /// layout and after the bindings that show a table hidden behind its empty state).</summary>
    private static void Schedule(DataGrid grid)
        => grid.Dispatcher.InvokeAsync(() => Refit(grid), DispatcherPriority.Loaded);

    /// <summary>
    /// Widens every automatic column to what its widest cell needs, measured from the
    /// rows on screen. Only ever widens: a column already wide enough is left alone, so
    /// this cannot make the grid jump on every refresh.
    /// </summary>
    public static void Refit(DataGrid grid)
    {
        foreach (var column in grid.Columns.Where(c => c.Width.IsAuto || GetIsAutomatic(c)))
        {
            // Remembered on the column, because the fix below turns it into a fixed width
            SetIsAutomatic(column, true);

            double needed = 0;
            foreach (var item in grid.Items)
            {
                if (column.GetCellContent(item) is not FrameworkElement content
                    || content.Parent is not DataGridCell cell) continue;   // row not on screen
                cell.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                needed = Math.Max(needed, cell.DesiredSize.Width);
            }

            if (needed > column.ActualWidth)
                column.Width = new DataGridLength(Math.Ceiling(needed));
        }
    }

    /// <summary>Marks a column declared "Auto", which keeps being refitted after Refit has
    /// given it a fixed width.</summary>
    private static readonly DependencyProperty IsAutomaticProperty = DependencyProperty.RegisterAttached(
        "IsAutomatic", typeof(bool), typeof(AutoColumns), new PropertyMetadata(false));

    private static bool GetIsAutomatic(DataGridColumn column) => (bool)column.GetValue(IsAutomaticProperty);
    private static void SetIsAutomatic(DataGridColumn column, bool value) => column.SetValue(IsAutomaticProperty, value);
}
