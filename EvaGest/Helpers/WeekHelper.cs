using EvaGest.Models;

namespace EvaGest.Helpers;

public static class WeekHelper {
    /// <summary>Monday of the week containing <paramref name="date"/>. Sunday belongs
    /// to the week that started the Monday before it, never the next one.</summary>
    public static DateOnly MondayOfWeek(DateOnly date)
    {
        int offset = ((int)date.DayOfWeek + 6) % 7; // Monday = 0, ..., Sunday = 6
        return date.AddDays(-offset);
    }

    /// <summary>Monday = Mon, ..., Sunday = Sun, matching the enum declared in models-domini.</summary>
    public static Weekday ToWeekday(DateOnly date) => (Weekday)(((int)date.DayOfWeek + 6) % 7);
}
