using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EvaGest.Models;
using EvaGest.Services;
using EvaGest.Resources;

namespace EvaGest.ViewModels.Pages;

/// <summary>
/// Reports (pantalles 2.8): three blocks with a period selector for the worker
/// ranking. Reports only make sense once there is real data (Phase 8 is
/// deliberately one of the last modules built).
/// </summary>
public partial class ReportsViewModel(IReportsService reports)
    : PageViewModelBase
{
    public override string Title => Texts.NavReports;

    [ObservableProperty] private DateOnly _from = new(DateOnly.FromDateTime(DateTime.Today).Year,
        DateOnly.FromDateTime(DateTime.Today).Month, 1);
    [ObservableProperty] private DateOnly _to = DateOnly.FromDateTime(DateTime.Today);

    [ObservableProperty] private string? _clientOfTheMonthText;

    /// <summary>Bar height in DIP for the evolution chart, scaled to the largest month.</summary>
    public record EvolutionBar(string Label, double HeightDip, string AmountText);

    // Named-property rows for the ranking lists: WPF Binding resolves CLR properties
    // reliably, unlike ValueTuple's Item1/Item2 fields, which are not guaranteed to
    // bind (silently blank instead of throwing — see Phase 7's XAML resource lesson).
    // Each row carries its figure already formatted. The cents/euros values stay on the
    // record for sorting and tests; only the *Text properties are ever bound, because a
    // raw *Cents value in a TextBlock prints cents as if they were euros.
    public record ClientVisitsRow(Client Client, int Visits, long TotalCents)
    {
        public string TotalText => Money.Format(TotalCents);
    }

    public record ClientSpendRow(Client Client, long TotalCents)
    {
        public string TotalText => Money.Format(TotalCents);
    }

    public record ClientAverageRow(Client Client, decimal AverageEuros)
    {
        /// <summary>Already euros, not cents — this one comes out of Indicators as a
        /// decimal, so it is formatted directly rather than through Money.</summary>
        public string AverageText => AverageEuros.ToString("C2", AppLanguage.Culture);
    }

    public record InactiveClientRow(Client Client, DateOnly Last, int DaysSince)
    {
        public string LastText => Last.ToString("dd/MM/yyyy");
    }

    /// <summary>Money is formatted here rather than in XAML: binding IncomeCents
    /// straight to a {0:0.00} format string showed euros as if they were cents.</summary>
    public record WorkerRankingRow(int WorkerId, string Name, int SalesHandled, string IncomeText, decimal? WorkPercentage)
    {
        /// <summary>
        /// Formatted here and not in XAML. A StringFormat of {0:0.0}% is resolved with the
        /// binding's culture, which defaults to en-US whatever AppLanguage.Culture says -
        /// nothing sets FrameworkElement.Language - so the column printed "42.5%" beside an
        /// income column reading "1.250,00 EUR". The percentage is already 0-100, so it is a
        /// plain number with the same " %" suffix Percentages.Format uses for a rate.
        /// </summary>
        public string WorkPercentText => WorkPercentage is decimal percent
            ? percent.ToString("0.0", AppLanguage.Culture) + " %"
            : "-";
    }

    public ObservableCollection<EvolutionBar> Evolution { get; } = [];
    public ObservableCollection<ClientVisitsRow> TopVisits { get; } = [];
    public ObservableCollection<ClientSpendRow> TopSpend { get; } = [];
    public ObservableCollection<ClientAverageRow> TopAverage { get; } = [];
    public ObservableCollection<InactiveClientRow> NotSeenRecently { get; } = [];
    public ObservableCollection<WorkerRankingRow> WorkerRanking { get; } = [];

    /// <summary>
    /// Which load is the current one. Every reload takes the next number and only writes
    /// to the collections if it still holds it when its queries come back, so a load
    /// overtaken by a newer one discards itself instead of fighting it.
    /// </summary>
    private int _generation;

    public async Task Load()
    {
        int generation = ++_generation;
        Loading = true;
        try
        {
            // Everything is queried first and the collections are rewritten afterwards in
            // one synchronous block. Clearing a collection and THEN awaiting let a second
            // load clear in between and both append, so every ranking listed each client
            // twice - reachable from the "this month" shortcut, which moves From and To in
            // two steps and so triggers two loads.
            var clientOfTheMonth = await reports.ClientOfTheMonth();
            var monthly = await reports.MonthlyEvolution();
            var topVisits = await reports.TopByVisits();
            var topSpend = await reports.TopBySpend();
            var topAverage = await reports.TopByAverage();
            var notSeen = await reports.GetNotSeenRecently();
            var ranking = await reports.WorkerRanking(From, To);

            if (generation != _generation) return;

            ClientOfTheMonthText = clientOfTheMonth is { } cdm
                ? string.Format(Texts.ClientOfTheMonthLine, cdm.client.Name, cdm.visits,
                                Money.Format(cdm.totalCents))
                : Texts.NoSalesThisMonth;

            long max = Math.Max(1, monthly.Max(m => m.totalCents));
            Evolution.Clear();
            foreach (var (year, month, total) in monthly)
            {
                var label = new DateOnly(year, month, 1).ToString("MMM", AppLanguage.Culture);
                Evolution.Add(new EvolutionBar(label, 4 + (total * 116.0 / max), Money.FormatExport(total)));
            }

            TopVisits.Clear();
            foreach (var t in topVisits) TopVisits.Add(new(t.client, t.visits, t.totalCents));

            TopSpend.Clear();
            foreach (var t in topSpend) TopSpend.Add(new(t.client, t.totalCents));

            TopAverage.Clear();
            foreach (var t in topAverage) TopAverage.Add(new(t.client, t.averageEuros));

            NotSeenRecently.Clear();
            foreach (var t in notSeen) NotSeenRecently.Add(new(t.client, t.last, t.daysSince));

            WorkerRanking.Clear();
            foreach (var d in ranking)
                WorkerRanking.Add(new(d.WorkerId, d.Name, d.SalesHandled, Money.Format(d.IncomeCents), d.WorkPercentage));
        }
        finally { if (generation == _generation) Loading = false; }
    }

    partial void OnFromChanged(DateOnly value)
    {
        if (value > To) To = value;
        else _ = Load();
    }

    partial void OnToChanged(DateOnly value)
    {
        if (value < From) From = value;
        else _ = Load();
    }

    /// <summary>Shortcut next to the range pickers: jumps straight to the 1st of the
    /// current month through today, the range most people want most of the time.</summary>
    [RelayCommand]
    private void SelectThisMonth()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        (From, To) = (new DateOnly(today.Year, today.Month, 1), today);
    }
}
