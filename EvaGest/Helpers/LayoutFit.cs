namespace EvaGest.Helpers;

/// <summary>
/// Screen arithmetic for dialogs and other layout that has to fit the screen, in DIPs.
/// Pure and stateless like <see cref="GridHelper"/>: the windows and views feed it the
/// numbers they read from WPF, so the rules themselves can be tested without a screen.
/// </summary>
public static class LayoutFit
{
    /// <summary>
    /// The largest a dialog window may grow: the work area (the screen minus the
    /// taskbar) with <paramref name="margin"/> kept free on every side. Never negative,
    /// even on an absurdly small screen.
    /// </summary>
    public static (double width, double height) MaxWindowSize(double workWidth, double workHeight, double margin)
        => (Math.Max(0, workWidth - 2 * margin), Math.Max(0, workHeight - 2 * margin));

    /// <summary>
    /// Moves a window edge so a window of <paramref name="length"/> starting at
    /// <paramref name="start"/> stays inside [<paramref name="areaStart"/>,
    /// <paramref name="areaEnd"/>]. Used once per axis. A window longer than the area
    /// is pinned to its start, so the title bar is never pushed off the top.
    /// </summary>
    public static double KeepInside(double start, double length, double areaStart, double areaEnd)
    {
        if (start + length > areaEnd) start = areaEnd - length;
        if (start < areaStart) start = areaStart;
        return start;
    }

    /// <summary>
    /// The width a flexible column gets out of <paramref name="available"/>: as much as
    /// it wants up to <paramref name="max"/>, never less than <paramref name="min"/>.
    /// Infinity (a SizeToContent window measuring itself) gives the full width.
    /// </summary>
    public static double Flexible(double available, double min, double max)
        => double.IsInfinity(available) || double.IsNaN(available)
            ? max
            : Math.Clamp(available, min, Math.Max(min, max));

    /// <summary>
    /// How many equal cells of at least <paramref name="minItemWidth"/> fit across
    /// <paramref name="width"/>, never more than there are <paramref name="items"/> and
    /// never fewer than one. Drives the card rows that wrap on a narrow screen.
    /// </summary>
    public static int Columns(double width, double minItemWidth, int items)
    {
        if (items <= 0) return 1;
        if (double.IsInfinity(width) || double.IsNaN(width) || minItemWidth <= 0) return items;
        int fit = (int)Math.Floor(width / minItemWidth);
        return Math.Clamp(fit, 1, items);
    }
}
