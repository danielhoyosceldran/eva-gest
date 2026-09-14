namespace EvaGest.Models;

/// <summary>One row per time slot, which allows split schedules (morning and afternoon).</summary>
public class WorkerSchedule
{
    public int Id { get; set; }

    public int WorkerId { get; set; }
    public Worker Worker { get; set; } = null!;

    public Weekday Weekday { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
}
