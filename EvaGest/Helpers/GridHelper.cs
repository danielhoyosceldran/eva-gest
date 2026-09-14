namespace EvaGest.Helpers;

/// <summary>A half-open time interval in whole minutes from midnight.</summary>
public readonly record struct TimeBlock(int StartMinute, int EndMinute);

/// <summary>Lane assignment for an overlapping block: which lane, out of how many.</summary>
public readonly record struct LaneAssignment(int Index, int Total);

/// <summary>
/// All the geometry of the weekly grid, kept as pure statics so it can be tested
/// without a UI thread. Everything is computed in whole minutes from midnight and
/// never with <see cref="TimeOnly.AddMinutes"/>, which wraps past midnight silently.
/// </summary>
public static class GridHelper
{
    public const int MinutesPerDay = 24 * 60;
    public const int DefaultSlotMinutes = 30;

    /// <summary>Fallback visible band when the week has no opening hours and no appointments.</summary>
    public static readonly (int start, int fi) DefaultRange = (9 * 60, 20 * 60);

    public static int DayMinutes(TimeOnly time) => time.Hour * 60 + time.Minute;

    public static TimeOnly ATime(int minutes)
    {
        int m = Math.Clamp(minutes, 0, MinutesPerDay - 1);
        return new TimeOnly(m / 60, m % 60);
    }

    /// <summary>A hand-edited database must never produce a zero-height or 1440-row grid.</summary>
    public static int IsValidSlotMinutes(int minutes)
        => minutes is 15 or 30 or 60 ? minutes : DefaultSlotMinutes;

    public static double PixelsPerMinute(double slotHeightPx, int slotMinutes)
        => slotHeightPx / IsValidSlotMinutes(slotMinutes);

    public static double Top(int absoluteMinutes, int gridStartMinute, double pixelsPerMinute)
        => (absoluteMinutes - gridStartMinute) * pixelsPerMinute;

    public static double Top(TimeOnly time, int gridStartMinute, double pixelsPerMinute)
        => Top(DayMinutes(time), gridStartMinute, pixelsPerMinute);

    /// <summary>
    /// Height of an appointment block. Clamped so a 5-minute appointment is still
    /// clickable, and shortened by 1px so consecutive blocks show a seam.
    /// </summary>
    public static double Height(int durationMin, double pixelsPerMinute, double minHeightPx)
        => Math.Max(minHeightPx, durationMin * pixelsPerMinute) - VerticalGapPx;

    public const double VerticalGapPx = 1;

    /// <summary>Snaps a vertical pixel offset inside a day column to the slot that contains it.</summary>
    public static TimeOnly SlotFromY(double y, int gridStartMinute, double pixelsPerMinute, int slotMinutes)
    {
        int slot = IsValidSlotMinutes(slotMinutes);
        if (pixelsPerMinute <= 0 || double.IsNaN(y)) return ATime(gridStartMinute);

        int minutes = gridStartMinute + (int)Math.Floor(y / pixelsPerMinute);
        int adjusted = Math.Clamp(minutes / slot * slot, 0, MinutesPerDay - slot);
        return ATime(adjusted);
    }

    /// <summary>
    /// Visible band for the whole week: the union of every day's opening hours, widened
    /// to contain any appointment falling outside them (an out-of-hours appointment must
    /// never be invisible), then floored/ceiled to whole hours so the hour lines align
    /// with the origin.
    /// </summary>
    public static (int startMinute, int endMinute) VisibleRange(
        IEnumerable<(TimeOnly start, TimeOnly fi)> intervals,
        IEnumerable<(TimeOnly time, int durationMin)> appointments)
    {
        int start = int.MaxValue, fi = int.MinValue;

        foreach (var f in intervals)
        {
            start = Math.Min(start, DayMinutes(f.start));
            fi = Math.Max(fi, ToEndMinute(f.fi));
        }
        foreach (var c in appointments)
        {
            int h = DayMinutes(c.time);
            start = Math.Min(start, h);
            fi = Math.Max(fi, h + Math.Max(0, c.durationMin));
        }

        if (start == int.MaxValue || fi <= start) return DefaultRange;

        start = start / 60 * 60;                                  // floor to the hour
        fi = (int)Math.Ceiling(fi / 60.0) * 60;                   // ceil to the hour
        fi = Math.Min(fi, MinutesPerDay);
        if (fi - start < 60) start = Math.Max(0, fi - 60);        // never a zero-height grid

        return (start, fi);
    }

    /// <summary>Whole hours to label on the ruler, as absolute minutes from midnight.</summary>
    public static IEnumerable<int> RulerHours(int startMinute, int endMinute, double hourHeightPx)
    {
        // Below ~28px an hourly label would collide with the next one.
        int step = hourHeightPx < 28 ? 120 : 60;
        for (int m = startMinute; m <= endMinute; m += step) yield return m;
    }

    /// <summary>
    /// Complement of the opening ranges inside the visible band: what to shade as closed.
    /// A split shift (morning + afternoon) yields the lunch gap as its own band.
    /// </summary>
    public static List<(int start, int fi)> OutsideScheduleBands(
        IReadOnlyList<(TimeOnly start, TimeOnly fi)> intervals, int startMinute, int endMinute)
    {
        if (intervals.Count == 0) return [(startMinute, endMinute)];   // day fully closed

        var ordered = intervals
            .Select(f => (i: DayMinutes(f.start), f: ToEndMinute(f.fi)))
            .OrderBy(f => f.i)
            .ToList();

        var bands = new List<(int, int)>();
        int cursor = startMinute;
        foreach (var (i, f) in ordered)
        {
            if (i > cursor) bands.Add((cursor, Math.Min(i, endMinute)));
            cursor = Math.Max(cursor, f);
            if (cursor >= endMinute) break;
        }
        if (cursor < endMinute) bands.Add((cursor, endMinute));

        return bands.Where(b => b.Item2 > b.Item1).ToList();
    }

    /// <summary>
    /// Greedy interval-graph colouring. Lanes are counted per connected cluster, so a
    /// three-way overlap at 10:00 does not narrow an unrelated appointment at 18:00.
    /// Blocks that merely touch (A ends exactly when B starts) do not overlap. O(n log n).
    /// </summary>
    public static LaneAssignment[] DistributeLanes(IReadOnlyList<TimeBlock> blocks)
    {
        var result = new LaneAssignment[blocks.Count];
        if (blocks.Count == 0) return result;

        // Longest-first on ties keeps the big block in lane 0, which reads better.
        var order = Enumerable.Range(0, blocks.Count)
            .OrderBy(i => blocks[i].StartMinute)
            .ThenByDescending(i => blocks[i].EndMinute)
            .ToArray();

        var toLane = new List<int>();   // last end minute per lane, current cluster only
        var group = new List<int>();
        int groupMaxEnd = int.MinValue;

        void CloseGroup()
        {
            foreach (int i in group) result[i] = result[i] with { Total = toLane.Count };
            group.Clear();
            toLane.Clear();
            groupMaxEnd = int.MinValue;
        }

        foreach (int i in order)
        {
            var block = blocks[i];
            int fi = Math.Max(block.EndMinute, block.StartMinute + 1);   // zero-duration guard

            if (group.Count > 0 && block.StartMinute >= groupMaxEnd) CloseGroup();

            int lane = toLane.FindIndex(f => f <= block.StartMinute);
            if (lane < 0)
            {
                toLane.Add(fi);
                lane = toLane.Count - 1;
            }
            else toLane[lane] = fi;

            result[i] = new LaneAssignment(lane, 0);
            group.Add(i);
            groupMaxEnd = Math.Max(groupMaxEnd, fi);
        }

        CloseGroup();
        return result;
    }

    /// <summary>Midnight as a closing time means the end of the day, not minute zero.</summary>
    private static int ToEndMinute(TimeOnly fi)
        => fi == TimeOnly.MinValue ? MinutesPerDay : DayMinutes(fi);
}
