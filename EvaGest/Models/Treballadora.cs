namespace EvaGest.Models;

public class Treballadora
{
    public int Id { get; set; }
    public string Nom { get; set; } = string.Empty;

    /// <summary>Inactive workers keep their schedule but do not count towards
    /// availability when checking for appointment overlaps (RF-06).</summary>
    public bool Actiu { get; set; } = true;

    /// <summary>Hex colour used to tell workers apart in the UI.</summary>
    public string Color { get; set; } = "#0F766E";

    public List<HorariTreballadora> Horaris { get; set; } = [];
    public List<Cita> Cites { get; set; } = [];
    public List<Venda> Vendes { get; set; } = [];
}
