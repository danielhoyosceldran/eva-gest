using EvaGest.Models;

namespace EvaGest.Services;

public record DetallTreballadora(
    int TreballadoraId, string Nom,
    int VendesAteses, long IngressosCents,
    decimal? PercentatgeTreball,        // null when the period had no sales
    decimal? PercentatgeProductes,      // null when this worker had no sales
    List<(string nom, int vegades)> Serveis,
    List<(string nom, int unitats)> Productes,
    long AltresConceptesCents,
    List<(DiaSetmana dia, int vendes, long ingressosCents)> ActivitatPerDia);

public interface IInformesService
{
    Task<IndicadorsClient> IndicadorsDeClient(int clientId);

    Task<List<(Client client, int visites, long totalCents)>> TopPerVisites(int limit = 10);
    Task<List<(Client client, long totalCents)>> TopPerDespesa(int limit = 10);
    Task<List<(Client client, decimal mitjanaEuros)>> TopPerMitjana(int limit = 10);
    Task<List<(Client client, DateOnly ultima, int diesSense)>> FaTempsQueNoVenen(int limit = 10);

    Task<DetallTreballadora> DetallDeTreballadora(int treballadoraId, DateOnly des, DateOnly fins);
    Task<List<DetallTreballadora>> RanquingTreballadores(DateOnly des, DateOnly fins);

    Task<List<(int any, int mes, long totalCents)>> EvolucioMensual(int mesos = 12);
    Task<(Client client, int visites, long totalCents)?> ClientDelMes();
}
