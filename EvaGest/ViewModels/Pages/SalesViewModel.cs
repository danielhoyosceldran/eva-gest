using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EvaGest.Models;
using EvaGest.Services;
using EvaGest.ViewModels.Dialogs;
using EvaGest.Resources;

namespace EvaGest.ViewModels.Pages;

/// <summary>
/// Sales (pantalles 2.6): the sales history, and the only place a mistake gets fixed.
/// Cancelled sales stay listed but are excluded from the footer totals, which is the
/// whole point of cancelling rather than deleting (RF-10).
/// </summary>
public partial class SalesViewModel(
    ISaleService sales, IClientService clients, ICatalogService catalog,
    IWorkerService workers, ISoundService so, ISettingsService settings,
    IExportService export, IDialogService dialogs) : PageViewModelBase
{
    public override string Title => Texts.NavSales;

    /// <summary>Money is formatted here rather than in XAML: binding a *Cents field
    /// straight to a {0:0.00} format string showed 36,50 € as "3650,00".</summary>
    public record SaleRow(
        Sale Sale, string DateText, string ClientText, string WorkerText,
        string ConceptText, string BaseText, string VatText, string TotalText,
        string MethodText, string StatusText, bool CanVoid);

    public ObservableCollection<SaleRow> Sales { get; } = [];

    // ── Filters (RF-15) ──────────────────────────────────────────────────────
    [ObservableProperty] private DateOnly? _from;
    [ObservableProperty] private DateOnly? _to;
    [ObservableProperty] private Client? _client;
    [ObservableProperty] private Service? _service;
    [ObservableProperty] private Product? _product;
    [ObservableProperty] private PaymentMethod? _paymentMethod;
    [ObservableProperty] private Worker? _worker;
    [ObservableProperty] private SaleStatus? _status;

    public ObservableCollection<Client> FilterClients { get; } = [];
    public ObservableCollection<Service> FilterServices { get; } = [];
    public ObservableCollection<Product> FilterProducts { get; } = [];
    public ObservableCollection<PaymentMethod> FilterMethods { get; } = [];
    public ObservableCollection<Worker> FilterWorkers { get; } = [];

    public IReadOnlyList<SaleStatus> FilterStatuses { get; } = [SaleStatus.Active, SaleStatus.Voided];

    // ── Table footer ─────────────────────────────────────────────────────────
    [ObservableProperty] private string _totalBaseText = "—";
    [ObservableProperty] private string _totalVatText = "—";
    [ObservableProperty] private string _totalTotalText = "—";
    [ObservableProperty] private int _activeCount;

    public bool HasNoResults => Sales.Count == 0;

    private bool _optionsLoaded;

    public async Task Load()
    {
        Loading = true;
        try
        {
            if (!_optionsLoaded) await LoadFilterOptions();

            var found = await sales.Search(new SalesFilter(
                From, To, Client?.Id, Service?.Id, Product?.Id,
                PaymentMethod?.Id, Worker?.Id, Status));

            Sales.Clear();
            foreach (var v in found) Sales.Add(ToRow(v));

            // Only active sales count: a cancelled one is still on screen precisely so
            // the user can see it does not add up (RF-10).
            var active = found.Where(v => v.Status == SaleStatus.Active).ToList();
            ActiveCount = active.Count;
            // Summed as long: with no date filter this is the whole history, and
            // Enumerable.Sum over int is checked — it throws rather than widening.
            TotalBaseText = Money.Format(active.Sum(v => (long)v.BaseCents));
            TotalVatText = Money.Format(active.Sum(v => (long)v.VatCents));
            TotalTotalText = Money.Format(active.Sum(v => (long)v.TotalCents));

            OnPropertyChanged(nameof(HasNoResults));
        }
        finally { Loading = false; }
    }

    private async Task LoadFilterOptions()
    {
        foreach (var c in await clients.GetActive()) FilterClients.Add(c);
        foreach (var s in await catalog.GetServices()) FilterServices.Add(s);
        foreach (var p in await catalog.GetProducts()) FilterProducts.Add(p);
        foreach (var m in await catalog.GetMethods()) FilterMethods.Add(m);
        foreach (var t in await workers.GetAll()) FilterWorkers.Add(t);
        _optionsLoaded = true;
    }

    private static SaleRow ToRow(Sale v) => new(
        v,
        v.Date.ToString("dd/MM/yyyy"),
        v.DisplayName,
        v.Worker?.Name ?? Texts.Unassigned,
        Summarize(v.Lines),
        Money.Format(v.BaseCents),
        Money.Format(v.VatCents),
        Money.Format(v.TotalCents),
        v.PaymentMethod?.Name ?? "—",
        Labels.Text(v.Status),
        v.Status == SaleStatus.Active);

    /// <summary>"Tall + Cera" (pantalles 2.6). Past three items it stops naming them,
    /// because a full list would push every other column off the row.</summary>
    private static string Summarize(List<SaleLine> lines)
    {
        if (lines.Count == 0) return "—";
        if (lines.Count <= 3) return string.Join(" + ", lines.Select(l => l.Description));

        return string.Join(" + ", lines.Take(2).Select(l => l.Description))
               + string.Format(Texts.MoreLinesSuffix, lines.Count - 2);
    }

    /// <summary>
    /// Set while several filters are being changed at once, so the page reloads once at
    /// the end instead of once per box. Clearing eight filters used to start eight
    /// fire-and-forget queries plus the awaited one, and the last to come back won - which
    /// is not necessarily the last to be sent, so the table and its totals could settle on
    /// a filter combination the boxes no longer showed.
    /// </summary>
    private bool _changingSeveralFilters;

    private void FilterChanged()
    {
        if (!_changingSeveralFilters) _ = Load();
    }

    partial void OnFromChanged(DateOnly? value) => FilterChanged();
    partial void OnToChanged(DateOnly? value) => FilterChanged();
    partial void OnClientChanged(Client? value) => FilterChanged();
    partial void OnServiceChanged(Service? value) => FilterChanged();
    partial void OnProductChanged(Product? value) => FilterChanged();
    partial void OnPaymentMethodChanged(PaymentMethod? value) => FilterChanged();
    partial void OnWorkerChanged(Worker? value) => FilterChanged();
    partial void OnStatusChanged(SaleStatus? value) => FilterChanged();

    [RelayCommand]
    private async Task ClearFilters()
    {
        _changingSeveralFilters = true;
        try
        {
            From = null;
            To = null;
            Client = null;
            Service = null;
            Product = null;
            PaymentMethod = null;
            Worker = null;
            Status = null;
        }
        finally { _changingSeveralFilters = false; }

        await Load();
    }

    [RelayCommand]
    private async Task NewSale()
    {
        var vm = await SaleDialogViewModel.New(sales, clients, catalog, workers, so, settings, dialogs);
        if (await dialogs.ShowDialog(vm)) await Load();
    }

    [RelayCommand]
    private async Task EditSale(Sale sale)
    {
        var vm = await SaleDialogViewModel.Edit(sales, clients, catalog, workers, so, settings, dialogs, sale);
        if (await dialogs.ShowDialog(vm)) await Load();
    }

    [RelayCommand]
    private async Task VoidSale(Sale sale)
    {
        bool confirmed = await dialogs.Confirm(
            Texts.ConfirmVoidSaleTitle,
            string.Format(Texts.ConfirmVoidSaleMessage, Money.Format(sale.TotalCents)),
            Texts.Void);

        if (!confirmed) return;

        await sales.Void(sale.Id);
        await Load();
    }

    /// <summary>
    /// An active sale is the accounting record, so "eliminar" cancels it and says so
    /// (RF-10). Repeating it on an already cancelled sale wipes it for good: by then the
    /// user has seen it sitting outside the totals and confirmed twice.
    /// </summary>
    [RelayCommand]
    private async Task DeleteSale(Sale sale)
    {
        bool voided = sale.Status == SaleStatus.Voided;

        bool confirmed = await dialogs.Confirm(
            voided ? Texts.DeleteForeverTitle : Texts.DeleteSaleTitle,
            voided
                ? string.Format(Texts.DeleteVoidedSaleMessage, Money.Format(sale.TotalCents))
                : string.Format(Texts.DeleteActiveSaleMessage, Money.Format(sale.TotalCents)),
            voided ? Texts.DeleteButton : Texts.Void);

        if (!confirmed) return;

        var result = await sales.Delete(sale.Id);
        await Load();

        ShowNotice(result == DeleteResult.Deactivated
            ? Texts.SaleVoidedNotDeleted
            : Texts.SaleDeletedForever);
    }

    [RelayCommand]
    private async Task ExportPeriod()
    {
        var vm = new ExportDialogViewModel(export, dialogs);
        await dialogs.ShowDialog(vm);
    }
}
