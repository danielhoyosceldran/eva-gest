using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EvaGest.Models;
using EvaGest.Services;
using EvaGest.Resources;

namespace EvaGest.ViewModels.Pages;

public enum TillPeriod { Today, Yesterday, ThisWeek, ThisMonth, PreviousMonth, Custom }

public partial class TillViewModel(ITillService till, ICatalogService catalog, IDialogService dialogs)
    : PageViewModelBase
{
    public override string Title => Texts.NavTill;

    [ObservableProperty] private TillPeriod _period = TillPeriod.Today;
    [ObservableProperty] private DateOnly _from = DateOnly.FromDateTime(DateTime.Today);
    [ObservableProperty] private DateOnly _to = DateOnly.FromDateTime(DateTime.Today);
    [ObservableProperty] private TillSummary? _summary;

    public ObservableCollection<CashMovement> Movements { get; } = [];

    public string SalesText => Money.Format((int)(Summary?.SalesCents ?? 0));
    public string CashInText => Money.Format((int)(Summary?.CashInCents ?? 0));
    public string CashOutText => Money.Format((int)(Summary?.CashOutCents ?? 0));
    public string BalanceText => Money.Format((int)(Summary?.BalanceCents ?? 0));
    public string BaseText => Money.Format((int)(Summary?.BaseCents ?? 0));
    public string VatText => Money.Format((int)(Summary?.VatCents ?? 0));

    /// <summary>Only shown per rate when the period actually mixes VAT rates
    /// (pantalles 2.7): with a single rate the top-line breakdown already says it all.</summary>
    public bool ShowRateBreakdown => (Summary?.VatBreakdown.Count ?? 0) > 1;

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
            foreach (var m in await till.GetByPeriod(From, To)) Movements.Add(m);

            foreach (var text in new[]
                     { nameof(SalesText), nameof(CashInText), nameof(CashOutText),
                       nameof(BalanceText), nameof(BaseText), nameof(VatText), nameof(ShowRateBreakdown) })
                OnPropertyChanged(text);
        }
        finally { Loading = false; }
    }

    [RelayCommand]
    private async Task NewCashIn() => await OpenMovementDialog(MovementType.In);

    [RelayCommand]
    private async Task NewCashOut() => await OpenMovementDialog(MovementType.Out);

    private async Task OpenMovementDialog(MovementType type)
    {
        var vm = new ViewModels.Dialogs.MovementDialogViewModel(type);
        await vm.LoadMethods(catalog);
        if (await dialogs.ShowDialog(vm))
        {
            await till.Create(vm.AModel());
            await Load();
        }
    }

    [RelayCommand]
    private async Task Delete(CashMovement movement)
    {
        bool confirmed = await dialogs.Confirm(Texts.DeleteMovementTitle,
            string.Format(Texts.DeleteMovementMessage, Money.Format(movement.AmountCents)),
            Texts.Delete);
        if (!confirmed) return;

        await till.Delete(movement.Id);
        await Load();
    }
}
