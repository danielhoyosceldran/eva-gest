using System.Globalization;
using EvaGest.Resources;

namespace EvaGest.Helpers;

/// <summary>
/// Parsing and validation of one day's opening hours, kept pure so every rule is
/// testable without a UI. Morning and afternoon are two independent shifts: a day can
/// be morning only, afternoon only, both, or closed.
/// </summary>
public static class ScheduleHelper
{
    public record ScheduleResult(List<(TimeOnly start, TimeOnly fi)> Intervals, string? Error)
    {
        public bool IsValid => Error is null;
    }

    /// <summary>Defaults offered when a shift is switched on with no hours yet.</summary>
    public static readonly (TimeOnly start, TimeOnly fi) DefaultMorning =
        (new TimeOnly(9, 0), new TimeOnly(14, 0));

    public static readonly (TimeOnly start, TimeOnly fi) DefaultAfternoon =
        (new TimeOnly(16, 0), new TimeOnly(20, 0));

    /// <summary>An hour at or after this belongs to the afternoon shift when a stored
    /// day has a single range, so "16:00–20:00" comes back on the afternoon row.</summary>
    public static readonly TimeOnly MorningAfternoonSplit = new(14, 1);

    /// <summary>
    /// Every quarter hour of a plausible working day, as the picker offers them. Typing is
    /// still allowed, so an odd hour like 09:10 is not lost by not being on the list.
    /// </summary>
    public static IReadOnlyList<string> Slots { get; } =
        [.. Enumerable.Range(0, (23 - 6) * 4 + 1)
             .Select(i => Format(new TimeOnly(6, 0).AddMinutes(i * 15)))];

    // "%H" and not "H": a one-character format string is read as a STANDARD specifier,
    // and TryParseExact throws FormatException instead of returning false.
    private static readonly string[] Formats =
        ["HH:mm", "H:mm", "HHmm", "Hmm", "HH.mm", "H.mm", "HH", "%H"];

    /// <summary>Accepts 9, 9:00, 09:00, 9.00 and 0900 — people type hours in all of these.</summary>
    public static TimeOnly? Analyze(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var net = text.Trim();

        foreach (var format in Formats)
            if (TimeOnly.TryParseExact(net, format, CultureInfo.InvariantCulture, DateTimeStyles.None, out var time))
                return time;

        return null;
    }

    public static string Format(TimeOnly time) => time.ToString("HH\\:mm", CultureInfo.InvariantCulture);

    /// <summary>
    /// Turns one day's two shifts into the ranges to store. Each shift is switched on
    /// independently, so a day can be morning only, afternoon only, both or closed; the
    /// hours of a shift that is switched off are ignored rather than validated, so
    /// unticking a shift never has to mean clearing its boxes first.
    /// </summary>
    public static ScheduleResult Check(bool morning, string? morningStart, string? morningEnd,
        bool afternoon, string? afternoonStart, string? afternoonEnd)
    {
        if (!morning && !afternoon) return new ScheduleResult([], null);

        TimeOnly? mi = null, mf = null;

        if (morning)
        {
            mi = Analyze(morningStart);
            mf = Analyze(morningEnd);
            if (mi is null || mf is null) return Invalid(Texts.MorningStartAndEndRequired);
            if (mf <= mi) return Invalid(Texts.MorningEndsBeforeStart);
        }

        if (!afternoon) return new ScheduleResult([(mi!.Value, mf!.Value)], null);

        var ti = Analyze(afternoonStart);
        var tf = Analyze(afternoonEnd);
        if (ti is null || tf is null) return Invalid(Texts.AfternoonStartAndEndRequired);
        if (tf <= ti) return Invalid(Texts.AfternoonEndsBeforeStart);

        if (!morning) return new ScheduleResult([(ti.Value, tf.Value)], null);
        if (ti < mf) return Invalid(Texts.AfternoonBeforeMorningEnds);

        return new ScheduleResult([(mi!.Value, mf!.Value), (ti.Value, tf.Value)], null);
    }

    private static ScheduleResult Invalid(string error) => new([], error);
}
