using System.Globalization;

namespace EvaGest.Helpers;

/// <summary>
/// The rules behind <c>TimeBox</c>, the hour input with one box for the hours and one for
/// the minutes and a fixed ":" between them. Kept as pure statics so every keystroke rule
/// is testable without a UI thread; the control only feeds them what would be in a box
/// after a keystroke and applies the answer.
///
/// Outside the control a time is still the one "hh:mm" string the rest of the app reads
/// (<see cref="ScheduleHelper.Analyze"/>), so nothing past the view had to change.
/// </summary>
public static class TimeEntry
{
    /// <summary>What a keystroke in the hours box comes to.</summary>
    /// <param name="Accepted">False: the key is ignored and the box stays as it was.</param>
    /// <param name="Text">The hours box after the keystroke.</param>
    /// <param name="JumpToMinutes">The hours are complete: move on to the minutes.</param>
    public readonly record struct HoursInput(bool Accepted, string Text, bool JumpToMinutes);

    /// <summary>
    /// The hours box would read <paramref name="proposed"/> after a keystroke. Two digits
    /// up to 23 are complete and jump on; a single digit above 2 can only be that hour,
    /// so it becomes "09" and jumps on too. Anything else that is not an hour is refused.
    /// </summary>
    public static HoursInput TypeHours(string proposed)
    {
        if (proposed.Length == 0) return new(true, proposed, false);
        if (proposed.Length > 2 || !proposed.All(char.IsAsciiDigit)) return new(false, proposed, false);

        int value = int.Parse(proposed, CultureInfo.InvariantCulture);
        if (proposed.Length == 2) return value <= 23 ? new(true, proposed, true) : new(false, proposed, false);
        return value > 2 ? new(true, "0" + proposed, true) : new(true, proposed, false);
    }

    /// <summary>
    /// The minutes box would read <paramref name="proposed"/>: the text to keep, or null
    /// to refuse the key. A single digit above 5 can only be that minute, so it becomes
    /// "07" straight away.
    /// </summary>
    public static string? TypeMinutes(string proposed)
    {
        if (proposed.Length == 0) return proposed;
        if (proposed.Length > 2 || !proposed.All(char.IsAsciiDigit)) return null;

        int value = int.Parse(proposed, CultureInfo.InvariantCulture);
        if (proposed.Length == 2) return value <= 59 ? proposed : null;
        return value > 5 ? "0" + proposed : proposed;
    }

    /// <summary>The keys that, typed in the hours box, mean "on to the minutes" instead of
    /// being written: the separators people type out of habit.</summary>
    public static bool IsSeparator(string text) => text is ":" or "." or "," or " " or "h";

    /// <summary>
    /// The two boxes as the one "hh:mm" string the app stores. Both empty is no time ("").
    /// Missing parts are filled the way a person means them: "9" and nothing is 09:00.
    /// Minutes with no hours are not a time yet, so they also give "".
    /// </summary>
    public static string Join(string hours, string minutes)
    {
        if (hours.Length == 0) return string.Empty;
        return hours.PadLeft(2, '0') + ":" + (minutes.Length == 0 ? "00" : minutes.PadLeft(2, '0'));
    }

    /// <summary>A stored "hh:mm" back into the two boxes; anything else leaves them empty.</summary>
    public static (string Hours, string Minutes) Split(string? time)
    {
        if (string.IsNullOrWhiteSpace(time)) return (string.Empty, string.Empty);
        var parsed = Parse(time);
        return parsed ?? (string.Empty, string.Empty);
    }

    /// <summary>
    /// Text pasted into either box, read loosely: "9:30", "09.30", "0930", "9" — the
    /// shapes a person copies from somewhere else. Null when it is not a time.
    /// </summary>
    public static (string Hours, string Minutes)? Parse(string text)
    {
        string trimmed = text.Trim();
        string[] parts = trimmed.Split([':', '.', ',', 'h', ' '], StringSplitOptions.RemoveEmptyEntries);

        string h, m;
        if (parts.Length == 2) (h, m) = (parts[0], parts[1]);
        else if (parts.Length == 1 && parts[0].Length is 3 or 4) (h, m) = (parts[0][..^2], parts[0][^2..]);
        else if (parts.Length == 1 && parts[0].Length <= 2) (h, m) = (parts[0], "00");
        else return null;

        if (!h.All(char.IsAsciiDigit) || !m.All(char.IsAsciiDigit) || h.Length is 0 or > 2 || m.Length != 2)
            return null;
        int hv = int.Parse(h, CultureInfo.InvariantCulture), mv = int.Parse(m, CultureInfo.InvariantCulture);
        if (hv > 23 || mv > 59) return null;

        return (hv.ToString("00", CultureInfo.InvariantCulture), m);
    }

    /// <summary>Up/Down in the hours box: one hour, wrapping round midnight.</summary>
    public static string StepHours(string hours, int delta)
    {
        int value = hours.Length == 0 ? (delta > 0 ? -1 : 0) : int.Parse(hours, CultureInfo.InvariantCulture);
        return (((value + delta) % 24 + 24) % 24).ToString("00", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Up/Down in the minutes box: to the next or previous quarter hour, which is how
    /// appointments and shifts are set. From an odd minute such as 10 it lands on the
    /// quarter either side (15 or 00) rather than 25 or 55.
    /// </summary>
    public static string StepMinutes(string minutes, int delta)
    {
        int value = minutes.Length == 0 ? 0 : int.Parse(minutes, CultureInfo.InvariantCulture);
        int next = delta > 0
            ? (value / 15 + 1) * 15
            : (value % 15 == 0 ? value - 15 : value / 15 * 15);
        return (((next % 60) + 60) % 60).ToString("00", CultureInfo.InvariantCulture);
    }
}
