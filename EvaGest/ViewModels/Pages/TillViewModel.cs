using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EvaGest.Models;
using EvaGest.Services;
using EvaGest.Resources;

namespace EvaGest.ViewModels.Pages;

public enum TillPeriod { Today, Yesterday, ThisWeek, ThisMonth, PreviousMonth, Custom }

/// <param name="now">
/// How the page reads the clock. Defaulted rather than injected, like BackupService's,
/// so the container still resolves the page; the tests pass a clock they can move past
/// midnight.
/// </param>
public partial class TillViewModel(
    ITillService till, ICatalogService catalog, IWorkerService workers,
    ISettingsService settings, IDialogService dialogs, Func<DateTime>? now = null)
    : PageViewModelBase
{
    public override string Title => Texts.NavTill;

    private readonly Func<DateTime> _now = now ?? (() => DateTime.Now);

    /// <summary>Today by the page's clock.</summary>
    private DateOnly Today => DateOnly.FromDateTime(_now());

    [ObservableProperty] private TillPeriod _period = TillPeriod.Today;
    [ObservableProperty] private DateOnly _from = DateOnly.FromDateTime(DateTime.Today);
    [ObservableProperty] private DateOnly _to = DateOnly.FromDateTime(DateTime.Today);
    [ObservableProperty] private TillSummary? _summary;

    /// <summary>
    /// One movement row, formatted here rather than in XAML. The table used to bind the
    /// model directly, which printed SignedAmountCents through a {0:0.00} format string —
    /// so a 15 € cash-in read "1500,00" — and printed the raw enum ("In"/"Out") and the
    /// raw DateOnly, neither of which is in the user's language.
    /// </summary>
    public record MovementRow(
        CashMovement Movement, string DateText, string TypeText, string ConceptText,
        string CategoryText, string WorkerText, string MethodText, string AmountText);

    /// <summary>One VAT-rate row. The rate is basis points in storage and a percentage
    /// on screen; the three amounts are cents in storage and euros on screen.</summary>
    public record RateRow(string RateText, string BaseText, string VatText, string TotalText);

    public ObservableCollection<MovementRow> Movements { get; } = [];
    public ObservableCollection<RateRow> VatRates { get; } = [];

    public string SalesText => Money.Format(Summary?.SalesCents ?? 0);
    public string CashInText => Money.Format(Summary?.CashInCents ?? 0);
    public string CashOutText => Money.Format(Summary?.CashOutCents ?? 0);
    public string BalanceText => Money.Format(Summary?.BalanceCents ?? 0);
    public string BaseText => Money.Format(Summary?.BaseCents ?? 0);
    public string VatText => Money.Format(Summary?.VatCents ?? 0);

    /// <summary>Base + quota. This is the invoiced total the VAT block is about, and it is
    /// NOT the till balance: the balance also carries cash movements, which have no VAT
    /// breakdown of their own. The two were bound to the same property, so the VAT block's
    /// "Total" silently showed the balance.</summary>
    public string VatTotalText => Money.Format((Summary?.BaseCents ?? 0) + (Summary?.VatCents ?? 0));

    /// <summary>True once any sale in the period carries VAT, which is the only case where
    /// a per-rate table has nothing to say. It is no longer hidden when the period uses a
    /// single rate: the user asked for the rate to be legible at all times, and a one-row
    /// table still states which rate the money was taxed at.</summary>
    public bool ShowRateBreakdown => VatRates.Count > 0;

    partial void OnPeriodChanged(TillPeriod value)
    {
        ApplyPeriod();
        RunInBackground(Load);
    }

    /// <summary>
    /// Sets From and To to what the selected period means today. Every period but Custom
    /// is relative to today, so this is also run on every load: the page is kept for the
    /// whole session, and with the range worked out only when the period was picked, an
    /// app left open overnight showed yesterday's till under "Avui" — and dated the next
    /// morning's cash movements yesterday (B-1).
    /// </summary>
    private void ApplyPeriod()
    {
        var today = Today;
        (From, To) = Period switch
        {
            TillPeriod.Today => (today, today),
            TillPeriod.Yesterday => (today.AddDays(-1), today.AddDays(-1)),
            TillPeriod.ThisWeek => (Helpers.WeekHelper.MondayOfWeek(today), today),
            TillPeriod.ThisMonth => (new DateOnly(today.Year, today.Month, 1), today),
            TillPeriod.PreviousMonth => FirstAndLastOfPreviousMonth(today),
            _ => (From, To)
        };
    }

    partial void OnFromChanged(DateOnly value)
    {
        if (value > To) { To = value; return; }
        if (Period == TillPeriod.Custom) RunInBackground(Load);
    }

    partial void OnToChanged(DateOnly value)
    {
        if (value < From) { From = value; return; }
        if (Period == TillPeriod.Custom) RunInBackground(Load);
    }

    private static (DateOnly, DateOnly) FirstAndLastOfPreviousMonth(DateOnly today)
    {
        var currentFirst = new DateOnly(today.Year, today.Month, 1);
        var previousLast = currentFirst.AddDays(-1);
        return (new DateOnly(previousLast.Year, previousLast.Month, 1), previousLast);
    }

    public async Task Load()
    {
        Loading = true;
        try
        {
            // A custom range is the user's and stays put; any other period follows the
            // date. Setting From and To here does not reload: outside Custom their change
            // handlers leave the loading to this method.
            if (Period != TillPeriod.Custom) ApplyPeriod();

            // Both queries first, then one synchronous rewrite: a custom range moves From
            // and To separately, so two loads can be in flight and clearing before the
            // await would let them interleave.
            var summary = await till.Summary(From, To);
            var movements = await till.GetByPeriod(From, To);

            Summary = summary;

            Movements.Clear();
            foreach (var m in movements) Movements.Add(ToRow(m));

            VatRates.Clear();
            foreach (var rate in Summary.VatBreakdown)
                VatRates.Add(new RateRow(
                    Percentages.Format(rate.VatBp),
                    Money.Format(rate.BaseCents),
                    Money.Format(rate.VatCents),
                    Money.Format(rate.TotalCents)));

            foreach (var text in new[]
                     { nameof(SalesText), nameof(CashInText), nameof(CashOutText),
                       nameof(BalanceText), nameof(BaseText), nameof(VatText),
                       nameof(VatTotalText), nameof(ShowRateBreakdown) })
                OnPropertyChanged(text);
        }
        finally { Loading = false; }
    }

    /// <summary>A cash-out is shown as a negative amount, which is what SignedAmountCents
    /// already encodes; the sign is the only thing distinguishing the two in the column.</summary>
    private static MovementRow ToRow(CashMovement m) => new(
        m,
        m.Date.ToString("dd/MM/yyyy"),
        Labels.Text(m.Type),
        m.Concept,
        m.Category?.Name ?? "—",
        m.Worker?.Name ?? "—",
        m.PaymentMethod?.Name ?? Texts.DeletedPaymentMethod,
        Money.Format(m.SignedAmountCents));

    [RelayCommand]
    private async Task NewCashIn() => await OpenMovementDialog(MovementType.In);

    [RelayCommand]
    private async Task NewCashOut() => await OpenMovementDialog(MovementType.Out);

    /// <summary>
    /// Today when today is in the period on screen; the period's last day otherwise. A
    /// movement entered while looking at a past month belongs to that month, and dating
    /// it today would drop it outside the table the user is reading. The dialog shows the
    /// date either way, so this is a starting point and not a decision made for them.
    /// </summary>
    private DateOnly DateForNewMovement()
    {
        var today = Today;
        return today >= From && today <= To ? today : To;
    }

    private async Task OpenMovementDialog(MovementType type)
    {
        var vm = await ViewModels.Dialogs.MovementDialogViewModel.New(
            type, DateForNewMovement(), catalog, workers, settings);
        vm.Persist = () => till.Create(vm.AModel());
        if (await dialogs.ShowDialog(vm)) await Load();
    }

    [RelayCommand]
    private async Task Delete(MovementRow row)
    {
        bool confirmed = await dialogs.ConfirmWithOwnerPin(Texts.DeleteMovementTitle,
            string.Format(Texts.DeleteMovementMessage, Money.Format(row.Movement.AmountCents)),
            Texts.Delete);
        if (!confirmed) return;

        await till.Delete(row.Movement.Id);
        await Load();
    }
}
