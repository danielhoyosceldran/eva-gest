namespace EvaGest.Helpers;

/// <summary>A half-open time interval in whole minutes from midnight.</summary>
public readonly record struct BlocTemporal(int MinutInici, int MinutFi);

/// <summary>Lane assignment for an overlapping block: which lane, out of how many.</summary>
public readonly record struct AssignacioCarril(int Index, int Total);

/// <summary>
/// All the geometry of the weekly grid, kept as pure statics so it can be tested
/// without a UI thread. Everything is computed in whole minutes from midnight and
/// never with <see cref="TimeOnly.AddMinutes"/>, which wraps past midnight silently.
/// </summary>
public static class GraellaHelper
{
    public const int MinutsPerDia = 24 * 60;
    public const int MinutsSlotPerDefecte = 30;

    /// <summary>Fallback visible band when the week has no opening hours and no appointments.</summary>
    public static readonly (int inici, int fi) RangPerDefecte = (9 * 60, 20 * 60);

    public static int MinutsDelDia(TimeOnly hora) => hora.Hour * 60 + hora.Minute;

    public static TimeOnly AHora(int minuts)
    {
        int m = Math.Clamp(minuts, 0, MinutsPerDia - 1);
        return new TimeOnly(m / 60, m % 60);
    }

    /// <summary>A hand-edited database must never produce a zero-height or 1440-row grid.</summary>
    public static int MinutsSlotValid(int minuts)
        => minuts is 15 or 30 or 60 ? minuts : MinutsSlotPerDefecte;

    public static double PixelsPerMinut(double alcadaSlotPx, int minutsSlot)
        => alcadaSlotPx / MinutsSlotValid(minutsSlot);

    public static double Top(int minutsAbsoluts, int minutIniciGraella, double pixelsPerMinut)
        => (minutsAbsoluts - minutIniciGraella) * pixelsPerMinut;

    public static double Top(TimeOnly hora, int minutIniciGraella, double pixelsPerMinut)
        => Top(MinutsDelDia(hora), minutIniciGraella, pixelsPerMinut);

    /// <summary>
    /// Height of an appointment block. Clamped so a 5-minute appointment is still
    /// clickable, and shortened by 1px so consecutive blocks show a seam.
    /// </summary>
    public static double Alcada(int duradaMin, double pixelsPerMinut, double alcadaMinimaPx)
        => Math.Max(alcadaMinimaPx, duradaMin * pixelsPerMinut) - SeparacioVerticalPx;

    public const double SeparacioVerticalPx = 1;

    /// <summary>Snaps a vertical pixel offset inside a day column to the slot that contains it.</summary>
    public static TimeOnly SlotDesDeY(double y, int minutIniciGraella, double pixelsPerMinut, int minutsSlot)
    {
        int slot = MinutsSlotValid(minutsSlot);
        if (pixelsPerMinut <= 0 || double.IsNaN(y)) return AHora(minutIniciGraella);

        int minuts = minutIniciGraella + (int)Math.Floor(y / pixelsPerMinut);
        int ajustat = Math.Clamp(minuts / slot * slot, 0, MinutsPerDia - slot);
        return AHora(ajustat);
    }

    /// <summary>
    /// Visible band for the whole week: the union of every day's opening hours, widened
    /// to contain any appointment falling outside them (an out-of-hours appointment must
    /// never be invisible), then floored/ceiled to whole hours so the hour lines align
    /// with the origin.
    /// </summary>
    public static (int minutInici, int minutFi) RangVisible(
        IEnumerable<(TimeOnly inici, TimeOnly fi)> franges,
        IEnumerable<(TimeOnly hora, int duradaMin)> cites)
    {
        int inici = int.MaxValue, fi = int.MinValue;

        foreach (var f in franges)
        {
            inici = Math.Min(inici, MinutsDelDia(f.inici));
            fi = Math.Max(fi, MinutFinal(f.fi));
        }
        foreach (var c in cites)
        {
            int h = MinutsDelDia(c.hora);
            inici = Math.Min(inici, h);
            fi = Math.Max(fi, h + Math.Max(0, c.duradaMin));
        }

        if (inici == int.MaxValue || fi <= inici) return RangPerDefecte;

        inici = inici / 60 * 60;                                  // floor to the hour
        fi = (int)Math.Ceiling(fi / 60.0) * 60;                   // ceil to the hour
        fi = Math.Min(fi, MinutsPerDia);
        if (fi - inici < 60) inici = Math.Max(0, fi - 60);        // never a zero-height grid

        return (inici, fi);
    }

    /// <summary>Whole hours to label on the ruler, as absolute minutes from midnight.</summary>
    public static IEnumerable<int> HoresDeLaRegla(int minutInici, int minutFi, double alcadaHoraPx)
    {
        // Below ~28px an hourly label would collide with the next one.
        int pas = alcadaHoraPx < 28 ? 120 : 60;
        for (int m = minutInici; m <= minutFi; m += pas) yield return m;
    }

    /// <summary>
    /// Complement of the opening ranges inside the visible band: what to shade as closed.
    /// A split shift (morning + afternoon) yields the lunch gap as its own band.
    /// </summary>
    public static List<(int inici, int fi)> BandesForaHorari(
        IReadOnlyList<(TimeOnly inici, TimeOnly fi)> franges, int minutInici, int minutFi)
    {
        if (franges.Count == 0) return [(minutInici, minutFi)];   // day fully closed

        var ordenades = franges
            .Select(f => (i: MinutsDelDia(f.inici), f: MinutFinal(f.fi)))
            .OrderBy(f => f.i)
            .ToList();

        var bandes = new List<(int, int)>();
        int cursor = minutInici;
        foreach (var (i, f) in ordenades)
        {
            if (i > cursor) bandes.Add((cursor, Math.Min(i, minutFi)));
            cursor = Math.Max(cursor, f);
            if (cursor >= minutFi) break;
        }
        if (cursor < minutFi) bandes.Add((cursor, minutFi));

        return bandes.Where(b => b.Item2 > b.Item1).ToList();
    }

    /// <summary>
    /// Greedy interval-graph colouring. Lanes are counted per connected cluster, so a
    /// three-way overlap at 10:00 does not narrow an unrelated appointment at 18:00.
    /// Blocks that merely touch (A ends exactly when B starts) do not overlap. O(n log n).
    /// </summary>
    public static AssignacioCarril[] RepartirCarrils(IReadOnlyList<BlocTemporal> blocs)
    {
        var resultat = new AssignacioCarril[blocs.Count];
        if (blocs.Count == 0) return resultat;

        // Longest-first on ties keeps the big block in lane 0, which reads better.
        var ordre = Enumerable.Range(0, blocs.Count)
            .OrderBy(i => blocs[i].MinutInici)
            .ThenByDescending(i => blocs[i].MinutFi)
            .ToArray();

        var finsCarril = new List<int>();   // last end minute per lane, current cluster only
        var grup = new List<int>();
        int finMaxGrup = int.MinValue;

        void TancarGrup()
        {
            foreach (int i in grup) resultat[i] = resultat[i] with { Total = finsCarril.Count };
            grup.Clear();
            finsCarril.Clear();
            finMaxGrup = int.MinValue;
        }

        foreach (int i in ordre)
        {
            var bloc = blocs[i];
            int fi = Math.Max(bloc.MinutFi, bloc.MinutInici + 1);   // zero-duration guard

            if (grup.Count > 0 && bloc.MinutInici >= finMaxGrup) TancarGrup();

            int carril = finsCarril.FindIndex(f => f <= bloc.MinutInici);
            if (carril < 0)
            {
                finsCarril.Add(fi);
                carril = finsCarril.Count - 1;
            }
            else finsCarril[carril] = fi;

            resultat[i] = new AssignacioCarril(carril, 0);
            grup.Add(i);
            finMaxGrup = Math.Max(finMaxGrup, fi);
        }

        TancarGrup();
        return resultat;
    }

    /// <summary>Midnight as a closing time means the end of the day, not minute zero.</summary>
    private static int MinutFinal(TimeOnly fi)
        => fi == TimeOnly.MinValue ? MinutsPerDia : MinutsDelDia(fi);
}
