namespace EvaGest.Models;

public class ShopSchedule
{
    public int Id { get; set; }
    public Weekday Weekday { get; set; }
    public TimeOnly OpeningTime { get; set; }
    public TimeOnly ClosingTime { get; set; }
}
