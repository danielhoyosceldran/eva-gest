namespace EvaGest.Models;

public class HorariBarberia
{
    public int Id { get; set; }
    public DiaSetmana DiaSetmana { get; set; }
    public TimeOnly HoraObertura { get; set; }
    public TimeOnly HoraTancament { get; set; }
}
