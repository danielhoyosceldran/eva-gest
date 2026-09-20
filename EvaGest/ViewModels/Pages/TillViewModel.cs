using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EvaGest.Models;
using EvaGest.Services;
using EvaGest.Resources;

namespace EvaGest.ViewModels.Pages;

public enum TillPeriod { Today, Yesterday, ThisWeek, ThisMonth, PreviousMonth, Custom }

public partial class TillViewModel(
    ITillService till, ICatalogService catalog, IWorkerService workers,
    ISettingsService settings, IDialogService dialogs)
    : PageViewModelBase
{
    public override string Title => Texts.NavTill;

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
        var today = DateOnly.FromDateTime(DateTime.Today);
        (From, To) = value switch
        {
            TillPeriod.Today => (today, today),
            TillPeriod.Yesterday => (today.AddDays(-1), today.AddDays(-1)),
            TillPeriod.ThisWeek => (Helpers.WeekHelper.MondayOfWeek(today), today),
            TillPeriod.ThisMonth => (new DateOnly(today.Year, today.Month, 1), today),
            TillPeriod.PreviousMonth => FirstAndLastOfPreviousMonth(today),
            _ => (From, To)
        };
        _ = Load();
    }

    partial void OnFromChanged(DateOnly value)
    {
        if (value > To) { To = value; return; }
        if (Period == TillPeriod.Custom) _ = Load();
    }

    partial void OnToChanged(DateOnly value)
    {
        if (value < From) { From = value; return; }
        if (Period == TillPeriod.Custom) _ = Load();
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
            Summary = await till.Summary(From, To);

            Movements.Clear();
            foreach (var m in await till.GetByPeriod(From, To)) Movements.Add(ToRow(m));

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
        m.PaymentMethod?.Name ?? "—",
        Money.Format(m.SignedAmountCents));

    [RelayCommand]
    private async Task NewCashIn() => await OpenMovementDialog(MovementType.In);

    [RelayCommand]
    private async Task NewCashOut() => await OpenMovementDialog(MovementType.Out);

    private async Task OpenMovementDialog(MovementType type)
    {
        var vm = new ViewModels.Dialogs.MovementDialogViewModel(type);
        await vm.LoadMethods(catalog);
        await vm.LoadCategories(catalog);
        await vm.LoadWorkers(workers);
        await vm.LoadDefaults(settings);
        if (await dialogs.ShowDialog(vm))
        {
            await till.Create(vm.AModel());
            await Load();
        }
    }

    [RelayCommand]
    private async Task Delete(MovementRow row)
    {
        bool confirmed = await dialogs.Confirm(Texts.DeleteMovementTitle,
            string.Format(Texts.DeleteMovementMessage, Money.Format(row.Movement.AmountCents)),
            Texts.Delete);
        if (!confirmed) return;

        await till.Delete(row.Movement.Id);
        await Load();
    }
}
