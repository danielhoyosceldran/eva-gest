using EvaGest.Models;

namespace EvaGest.Services;

public record WorkerDetail(
    int WorkerId, string Name,
    int SalesHandled, long IncomeCents,
    decimal? WorkPercentage,        // null when the period had no sales
    decimal? ProductsPercentage,      // null when this worker had no sales
    List<(string name, int times)> Services,
    List<(string name, int units)> Products,
    long OtherConceptsCents,
    List<(Weekday day, int sales, long incomeCents)> ActivityByDay);

public interface IReportsService
{
    Task<ClientIndicators> GetClientIndicators(int clientId);

    Task<List<(Client client, int visits, long totalCents)>> TopByVisits(int limit = 10);
    Task<List<(Client client, long totalCents)>> TopBySpend(int limit = 10);
    Task<List<(Client client, decimal averageEuros)>> TopByAverage(int limit = 10);
    Task<List<(Client client, DateOnly last, int daysSince)>> GetNotSeenRecently(int limit = 10);

    Task<WorkerDetail> GetWorkerDetail(int workerId, DateOnly from, DateOnly to);
    Task<List<WorkerDetail>> WorkerRanking(DateOnly from, DateOnly to);

    Task<List<(int year, int month, long totalCents)>> MonthlyEvolution(int months = 12);
    Task<(Client client, int visits, long totalCents)?> ClientOfTheMonth();
}
