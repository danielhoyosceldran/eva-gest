namespace EvaGest.Models;

public class Worker
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;

    /// <summary>Inactive workers keep their schedule but do not count towards
    /// availability when checking for appointment overlaps (RF-06).</summary>
    public bool Active { get; set; } = true;

    /// <summary>Hex colour used to tell workers apart in the UI.</summary>
    public string Color { get; set; } = "#0F766E";

    public List<WorkerSchedule> Schedules { get; set; } = [];
    public List<Appointment> Appointments { get; set; } = [];
    public List<Sale> Sales { get; set; } = [];

    public override string ToString() => Name;
}
