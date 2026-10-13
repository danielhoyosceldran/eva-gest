using System.Globalization;

namespace EvaGest.Helpers;

/// <summary>
/// The rules behind <c>DateBox</c>, the date input with one box each for day, month and
/// year and a fixed "/" between them. The date twin of <see cref="TimeEntry"/>: pure
/// statics, so every keystroke rule is testable without a UI thread, and the control
/// only feeds them what a box would read after a keystroke and applies the answer.
///
/// The order is day / month / year, which is how both Catalan and Spanish write a date.
/// </summary>
public static class DateEntry
{
    /// <summary>What a keystroke in the day or month box comes to.</summary>
    /// <param name="Accepted">False: the key is ignored and the box stays as it was.</param>
    /// <param name="Text">The box after the keystroke.</param>
    /// <param name="JumpToNext">The part is complete: move on to the next box.</param>
    public readonly record struct PartInput(bool Accepted, string Text, bool JumpToNext);

    /// <summary>The day box would read <paramref name="proposed"/>. Two digits from 01 to
    /// 31 are complete; a single digit above 3 can only be that day ("4" is 04).</summary>
    public static PartInput TypeDay(string proposed) => TypeTwoDigits(proposed, max: 31, firstDigitMax: 3);

    /// <summary>The month box would read <paramref name="proposed"/>. Two digits from 01
    /// to 12 are complete; a single digit above 1 can only be that month ("2" is 02).</summary>
    public static PartInput TypeMonth(string proposed) => TypeTwoDigits(proposed, max: 12, firstDigitMax: 1);

    private static PartInput TypeTwoDigits(string proposed, int max, int firstDigitMax)
    {
        if (proposed.Length == 0) return new(true, proposed, false);
        if (proposed.Length > 2 || !proposed.All(char.IsAsciiDigit)) return new(false, proposed, false);

        int value = int.Parse(proposed, CultureInfo.InvariantCulture);
        if (proposed.Length == 2)
            return value is >= 1 && value <= max ? new(true, proposed, true) : new(false, proposed, false);
        return value > firstDigitMax ? new(true, "0" + proposed, true) : new(true, proposed, false);
    }

    /// <summary>The year box would read <paramref name="proposed"/>: the text to keep, or
    /// null to refuse the key. Up to four digits; it is the last box, so nothing jumps.</summary>
    public static string? TypeYear(string proposed)
        => proposed.Length <= 4 && proposed.All(char.IsAsciiDigit) ? proposed : null;

    /// <summary>The keys that, typed in the day or month box, mean "on to the next box"
    /// instead of being written: the separators people type out of habit.</summary>
    public static bool IsSeparator(string text) => text is "/" or "-" or "." or " ";

    /// <summary>
    /// The date the three boxes stand for while typing: only once all three are complete
    /// (a four-digit year) and form a real day. Anything short of that is not a date yet,
    /// so nothing is written until it is: a half-typed year would otherwise send 2020 on
    /// its way to 2026, and a filter would reload for it.
    /// </summary>
    public static DateOnly? Complete(string day, string month, string year)
        => day.Length > 0 && month.Length > 0 && year.Length == 4 ? ToDate(day, month, year) : null;

    /// <summary>All three boxes are filled in, with a four-digit year, but the day does
    /// not exist (31/02, 29/02 in a common year). The control draws its outline red.</summary>
    public static bool IsImpossible(string day, string month, string year)
        => day.Length > 0 && month.Length > 0 && year.Length == 4 && ToDate(day, month, year) is null;

    /// <summary>
    /// What leaving the control makes of the boxes, filling in what a person leaves out:
    /// a single digit is padded ("4" is 04), no year is the current one, and a two-digit
    /// year is this century ("26" is 2026). The day and month must be there.
    /// </summary>
    /// <returns>The boxes as they should now read, and the date they make (null when
    /// they make none).</returns>
    public static (string Day, string Month, string Year, DateOnly? Date) Tidy(
        string day, string month, string year, DateOnly today)
    {
        if (day.Length == 0 && month.Length == 0 && year.Length == 0)
            return (day, month, year, null);

        string d = day.Length == 1 ? "0" + day : day;
        string m = month.Length == 1 ? "0" + month : month;
        string y = year.Length switch
        {
            0 when d.Length > 0 && m.Length > 0 => today.Year.ToString(CultureInfo.InvariantCulture),
            2 => (2000 + int.Parse(year, CultureInfo.InvariantCulture)).ToString(CultureInfo.InvariantCulture),
            _ => year
        };

        return (d, m, y, Complete(d, m, y));
    }

    /// <summary>A date as the three boxes show it; null leaves them empty.</summary>
    public static (string Day, string Month, string Year) Split(DateOnly? date)
        => date is { } d
            ? (d.Day.ToString("00", CultureInfo.InvariantCulture),
               d.Month.ToString("00", CultureInfo.InvariantCulture),
               d.Year.ToString("0000", CultureInfo.InvariantCulture))
            : (string.Empty, string.Empty, string.Empty);

    /// <summary>
    /// Text pasted into any of the boxes, read loosely: "13/10/2026", "13-10-26",
    /// "13.10.2026" and the ISO "2026-10-13" a spreadsheet hands out. Null when it is not
    /// a real date.
    /// </summary>
    public static DateOnly? Parse(string text)
    {
        string[] parts = text.Trim().Split(['/', '-', '.', ' '], StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 3 || parts.Any(p => !p.All(char.IsAsciiDigit))) return null;

        // Year first only in the ISO shape, which is the only one with four digits up front
        var (d, m, y) = parts[0].Length == 4 ? (parts[2], parts[1], parts[0]) : (parts[0], parts[1], parts[2]);
        if (d.Length > 2 || m.Length > 2 || y.Length is not (2 or 4)) return null;
        if (y.Length == 2) y = (2000 + int.Parse(y, CultureInfo.InvariantCulture)).ToString(CultureInfo.InvariantCulture);

        return ToDate(d, m, y);
    }

    /// <summary>Up/Down in the day box: one day, from 31 round to 01 (the month is left
    /// alone, so the arrows never change more than the box they are in). An empty box
    /// starts from today's day.</summary>
    public static string StepDay(string day, int delta, DateOnly today)
        => Step(day, delta, 1, 31, today.Day, "00");

    /// <summary>Up/Down in the month box: one month, from 12 round to 01.</summary>
    public static string StepMonth(string month, int delta, DateOnly today)
        => Step(month, delta, 1, 12, today.Month, "00");

    /// <summary>Up/Down in the year box: one year, no wrapping.</summary>
    public static string StepYear(string year, int delta, DateOnly today)
    {
        int value = year.Length == 4 ? int.Parse(year, CultureInfo.InvariantCulture) : today.Year - delta;
        return Math.Clamp(value + delta, 1, 9999).ToString("0000", CultureInfo.InvariantCulture);
    }

    private static string Step(string text, int delta, int min, int max, int fromToday, string format)
    {
        // An empty box starts on today's value rather than one step away from it
        if (text.Length == 0) return fromToday.ToString(format, CultureInfo.InvariantCulture);

        int value = int.Parse(text, CultureInfo.InvariantCulture);
        int span = max - min + 1;
        int next = ((value - min + delta) % span + span) % span + min;
        return next.ToString(format, CultureInfo.InvariantCulture);
    }

    private static DateOnly? ToDate(string day, string month, string year)
    {
        if (!int.TryParse(day, NumberStyles.None, CultureInfo.InvariantCulture, out int d)
            || !int.TryParse(month, NumberStyles.None, CultureInfo.InvariantCulture, out int m)
            || !int.TryParse(year, NumberStyles.None, CultureInfo.InvariantCulture, out int y))
            return null;
        if (y is < 1 or > 9999 || m is < 1 or > 12 || d < 1 || d > DateTime.DaysInMonth(y, m)) return null;
        return new DateOnly(y, m, d);
    }
}
