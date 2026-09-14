using EvaGest.Models;

namespace EvaGest.Services;

public interface IClientService
{
    Task<List<Client>> GetActive();
    Task<List<Client>> GetAsleep();
    Task<List<Client>> Search(string text);          // by name or phone
    Task<Client?> GetById(int id);

    /// <summary>Returns the existing client whose ClientKey matches, if any.
    /// Used to warn about probable duplicates before saving (RF-03).</summary>
    Task<Client?> FindPossibleDuplicate(string name, string mobile);

    Task<int> Create(Client client);

    /// <summary>Recalculates ClientKey when name or phone changed.</summary>
    Task Update(Client client);

    Task Sleep(int clientId);
    Task Wake(int clientId);

    /// <summary>Physical delete. Cascades to appointments and sales (RF-04).</summary>
    Task Delete(int clientId);

    /// <summary>Counts used by the delete confirmation message.</summary>
    Task<(int appointments, int sales)> CountHistory(int clientId);

    /// <summary>Registered clients whose birthday is today (RF-03).</summary>
    Task<List<Client>> BirthdaysToday();

    /// <summary>Chronological history (appointments and sales) for the fitxa's timeline.
    /// A stand-in for what AppointmentService/SaleService will expose once Phase 5/6 build them.</summary>
    Task<List<ClientHistoryRow>> GetClientHistory(int clientId);
}

/// <summary>One row of the client timeline. <paramref name="Type"/> is a code from
/// <see cref="HistoryType"/>, never shown as it is; a sale has no concept of its own,
/// so the record page names it after its type.</summary>
public record ClientHistoryRow(DateOnly Date, string Type, string? Concept, string Status, int? AmountCents);

/// <summary>What a <see cref="ClientHistoryRow"/> came from.</summary>
public static class HistoryType
{
    public const string Appointment = "Appointment";
    public const string Sale = "Sale";
}
