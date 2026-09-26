using AwesomeAssertions;
using EvaGest.Models;
using EvaGest.Services;
using EvaGest.ViewModels.Pages;
using Xunit;

namespace EvaGest.Tests.Services;

/// <summary>
/// A reports service whose calls complete out of order, which is all an async database
/// call guarantees. The first call in is the slowest out — the ordering that used to
/// leave two loads interleaved.
/// </summary>
internal sealed class OutOfOrderReports : IReportsService
{
    public int Calls;

    public async Task<List<(Client client, long totalCents)>> TopBySpend(int limit = 10)
    {
        int n = ++Calls;
        await Task.Delay(n == 1 ? 120 : 10);
        return [(new Client { Id = n, Name = $"call{n}" }, 100)];
    }

    public Task<List<(Client client, int visits, long totalCents)>> TopByVisits(int limit = 10)
        => Task.FromResult(new List<(Client, int, long)>());
    public Task<List<(Client client, decimal averageEuros)>> TopByAverage(int limit = 10)
        => Task.FromResult(new List<(Client, decimal)>());
    public Task<List<(Client client, DateOnly last, int daysSince)>> GetNotSeenRecently(int limit = 10)
        => Task.FromResult(new List<(Client, DateOnly, int)>());
    public Task<List<WorkerDetail>> WorkerRanking(DateOnly from, DateOnly to)
        => Task.FromResult(new List<WorkerDetail>());
    public Task<List<(int year, int month, long totalCents)>> MonthlyEvolution(int months = 12)
        => Task.FromResult(Enumerable.Range(0, months).Select(i => (2026, 1 + i % 12, 0L)).ToList());
    public Task<(Client client, int visits, long totalCents)?> ClientOfTheMonth()
        => Task.FromResult<(Client, int, long)?>(null);
    public Task<ClientIndicators> GetClientIndicators(int clientId) => throw new NotSupportedException();
    public Task<WorkerDetail> GetWorkerDetail(int workerId, DateOnly from, DateOnly to) => throw new NotSupportedException();
}

/// <summary>
/// Two reloads of one page can be in flight at once, because every filter change starts
/// one fire-and-forget. These pin that the second does not end up appended to the first.
/// </summary>
public class PageReloadRaceTests
{
    [Fact]
    public async Task Two_overlapping_report_loads_do_not_duplicate_the_ranking_rows()
    {
        var reports = new OutOfOrderReports();
        var vm = new ReportsViewModel(reports);

        var first = vm.Load();
        var second = vm.Load();
        await Task.WhenAll(first, second);

        reports.Calls.Should().Be(2, "both loads really did run");
        vm.TopSpend.Should().HaveCount(1, "the card shows one period, not two appended");
    }

    [Fact]
    public async Task A_load_overtaken_by_a_newer_one_does_not_write_its_stale_rows()
    {
        var reports = new OutOfOrderReports();
        var vm = new ReportsViewModel(reports);

        var stale = vm.Load();     // slow: comes back last
        var current = vm.Load();   // fast: comes back first
        await Task.WhenAll(stale, current);

        // The newer load is the one on screen even though the older finished after it.
        vm.TopSpend.Should().ContainSingle()
          .Which.Client.Name.Should().Be("call2");
    }

    [Fact]
    public async Task The_this_month_shortcut_leaves_one_set_of_rows()
    {
        var reports = new OutOfOrderReports();
        var vm = new ReportsViewModel(reports)
        {
            // A range outside the current month, so the shortcut has to move both ends.
            From = new DateOnly(2020, 1, 1),
            To = new DateOnly(2020, 1, 31)
        };

        vm.SelectThisMonthCommand.Execute(null);
        // The shortcut sets From then To, each starting its own fire-and-forget load.
        await Task.Delay(300);

        vm.TopSpend.Should().HaveCount(1);
    }
}
