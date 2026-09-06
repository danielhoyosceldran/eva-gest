using EvaGest.Models;

namespace EvaGest.Helpers;

public static class SetmanaHelper {
    /// <summary>Monday of the week containing <paramref name="data"/>. Sunday belongs
    /// to the week that started the Monday before it, never the next one.</summary>
    public static DateOnly DilluIrsDeLaSetmana(DateOnly data)
    {
        int offset = ((int)data.DayOfWeek + 6) % 7; // Monday = 0, ..., Sunday = 6
        return data.AddDays(-offset);
    }

    /// <summary>Monday = Dl, ..., Sunday = Dg, matching the enum declared in models-domini.</summary>
    public static DiaSetmana ADiaSetmana(DateOnly data) => (DiaSetmana)(((int)data.DayOfWeek + 6) % 7);
}
