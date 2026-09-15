using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using EvaGest.Models;
using EvaGest.Services;
using EvaGest.Resources;

namespace EvaGest.ViewModels.Pages;

/// <summary>
/// Reports (pantalles 2.8): four blocks with a common period selector for the
/// worker-related ones. Reports only make sense once there is real data (Phase 8
/// is deliberately one of the last modules built).
/// </summary>
public partial class ReportsViewModel(IReportsService reports, IWorkerService workers)
    : PageViewModelBase
{
    public override string Title => Texts.NavReports;

    [ObservableProperty] private DateOnly _from = new(DateOnly.FromDateTime(DateTime.Today).Year,
        DateOnly.FromDateTime(DateTime.Today).Month, 1);
    [ObservableProperty] private DateOnly _to = DateOnly.FromDateTime(DateTime.Today);

    [ObservableProperty] private string? _clientOfTheMonthText;
    [ObservableProperty] private WorkerDetail? _selectedDetail;
    [ObservableProperty] private Worker? _selectedWorker;

    /// <summary>Bar height in DIP for the evolution chart, scaled to the largest month.</summary>
    public record EvolutionBar(string Label, double HeightDip, string AmountText);

    // Named-property rows for the ranking lists: WPF Binding resolves CLR properties
    // reliably, unlike ValueTuple's Item1/Item2 fields, which are not guaranteed to
    // bind (silently blank instead of throwing — see Phase 7's XAML resource lesson).
    public record ClientVisitsRow(Client Client, int Visits, long TotalCents);
    public record ClientSpendRow(Client Client, long TotalCents);
    public record ClientAverageRow(Client Client, decimal AverageEuros);
    public record InactiveClientRow(Client Client, DateOnly Last, int DaysSince);

    /// <summary>Money is formatted here rather than in XAML: binding IncomeCents
    /// straight to a {0:0.00} format string showed euros as if they were cents.</summary>
    public record WorkerRankingRow(int WorkerId, string Name, int SalesHandled, string IncomeText, decimal? WorkPercentage);

    public ObservableCollection<EvolutionBar> Evolution { get; } = [];
    public ObservableCollection<ClientVisitsRow> TopVisits { get; } = [];
    public ObservableCollection<ClientSpendRow> TopSpend { get; } = [];
    public ObservableCollection<ClientAverageRow> TopAverage { get; } = [];
    public ObservableCollection<InactiveClientRow> NotSeenRecently { get; } = [];
    public ObservableCollection<WorkerRankingRow> WorkerRanking { get; } = [];
    public ObservableCollection<Worker> Workers { get; } = [];

    [ObservableProperty] private string _selectedDetailIncomeText = "—";

    public async Task Load()
    {
        Loading = true;
        try
        {
            var clientOfTheMonth = await reports.ClientOfTheMonth();
            ClientOfTheMonthText = clientOfTheMonth is { } cdm
                ? string.Format(Texts.ClientOfTheMonthLine, cdm.client.Name, cdm.visits,
                                Money.Format((int)cdm.totalCents))
                : Texts.NoSalesThisMonth;

            var monthly = await reports.MonthlyEvolution();
            long max = Math.Max(1, monthly.Max(m => m.totalCents));
            Evolution.Clear();
            foreach (var (year, month, total) in monthly)
            {
                var label = new DateOnly(year, month, 1).ToString("MMM", AppLanguage.Culture);
                Evolution.Add(new EvolutionBar(label, 4 + (total * 116.0 / max), Money.FormatExport((int)total)));
            }

            TopVisits.Clear();
            foreach (var t in await reports.TopByVisits()) TopVisits.Add(new(t.client, t.visits, t.totalCents));

            TopSpend.Clear();
            foreach (var t in await reports.TopBySpend()) TopSpend.Add(new(t.client, t.totalCents));

            TopAverage.Clear();
            foreach (var t in await reports.TopByAverage()) TopAverage.Add(new(t.client, t.averageEuros));

            NotSeenRecently.Clear();
            foreach (var t in await reports.GetNotSeenRecently()) NotSeenRecently.Add(new(t.client, t.last, t.daysSince));

            WorkerRanking.Clear();
            foreach (var d in await reports.WorkerRanking(From, To))
                WorkerRanking.Add(new(d.WorkerId, d.Name, d.SalesHandled, Money.Format((int)d.IncomeCents), d.WorkPercentage));

            if (Workers.Count == 0)
                foreach (var t in await workers.GetAll()) Workers.Add(t);

            if (SelectedWorker is null && Workers.Count > 0)
                SelectedWorker = Workers[0];

            if (SelectedWorker is not null)
            {
                SelectedDetail = await reports.GetWorkerDetail(SelectedWorker.Id, From, To);
                SelectedDetailIncomeText = Money.Format((int)SelectedDetail.IncomeCents);
            }
        }
        finally { Loading = false; }
    }

    partial void OnFromChanged(DateOnly value) => _ = Load();
    partial void OnToChanged(DateOnly value) => _ = Load();
    partial void OnSelectedWorkerChanged(Worker? value) => _ = Load();
}
