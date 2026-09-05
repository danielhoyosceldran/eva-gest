using EvaGest.Models;

namespace EvaGest.Services;

public interface IClientService
{
    Task<List<Client>> ObtenirActius();
    Task<List<Client>> ObtenirAdormits();
    Task<List<Client>> Cercar(string text);          // by name or phone
    Task<Client?> ObtenirPerId(int id);

    /// <summary>Returns the existing client whose ClientKey matches, if any.
    /// Used to warn about probable duplicates before saving (RF-03).</summary>
    Task<Client?> BuscarPossibleDuplicat(string nom, string mobil);

    Task<int> Crear(Client client);

    /// <summary>Recalculates ClientKey when name or phone changed.</summary>
    Task Actualitzar(Client client);

    Task Adormir(int clientId);
    Task Despertar(int clientId);

    /// <summary>Physical delete. Cascades to appointments and sales (RF-04).</summary>
    Task Eliminar(int clientId);

    /// <summary>Counts used by the delete confirmation message.</summary>
    Task<(int cites, int vendes)> ComptarHistorial(int clientId);

    /// <summary>Registered clients whose birthday is today (RF-03).</summary>
    Task<List<Client>> AniversarisAvui();

    /// <summary>Chronological history (appointments and sales) for the fitxa's timeline.
    /// A stand-in for what CitaService/VendaService will expose once Fase 5/6 build them.</summary>
    Task<List<FilaHistorialClient>> HistorialDeClient(int clientId);
}

public record FilaHistorialClient(DateOnly Data, string Tipus, string Concepte, string Estat, int? ImportCents);
