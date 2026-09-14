namespace EvaGest.Models;

public class ClosedDay
{
    public int Id { get; set; }
    public DateOnly Date { get; set; }
    public string? Reason { get; set; }
}
