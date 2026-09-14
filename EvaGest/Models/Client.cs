namespace EvaGest.Models;

public class Client
{
    public int Id { get; set; }

    /// <summary>Duplicate-detection hash: normalised first name + last 9 phone digits.
    /// Must be recalculated by the service layer whenever Name or Mobile change.</summary>
    public string ClientKey { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
    public string Mobile { get; set; } = string.Empty;

    public string? Email { get; set; }
    public DateOnly? BirthDate { get; set; }
    public string? Notes { get; set; }

    /// <summary>Hidden from searches and pickers, but still counted in statistics.</summary>
    public bool Asleep { get; set; }

    public List<Appointment> Appointments { get; set; } = [];
    public List<Sale> Sales { get; set; } = [];

    /// <summary>Defence in depth: any picker that forgets DisplayMemberPath would
    /// otherwise render the type name instead of the person.</summary>
    public override string ToString() => Name;
}
