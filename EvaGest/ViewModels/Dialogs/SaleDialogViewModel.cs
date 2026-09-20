using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EvaGest.Models;
using EvaGest.Services;
using EvaGest.ViewModels.Elements;
using EvaGest.Resources;

namespace EvaGest.ViewModels.Dialogs;

/// <summary>
/// Sale (new / from an appointment / edit), pantalles 3.2. The most complex dialog, and
/// the one that reuses the same class for all three modes (RF-09).
/// </summary>
public partial class SaleDialogViewModel : DialogViewModelBase
{
    public enum Mode { New, FromAppointment, Edit }

    private readonly ISaleService _sales;
    private readonly ISoundService _so;
    private readonly IClientService _clients;
    private readonly IDialogService _dialogs;
    private int? _id;

    /// <summary>Whether the guest reminder is switched on (RF-05bis).</summary>
    private bool _guestNoticeEnabled;

    /// <summary>
    /// The VAT mode the live footer computes in. Read once when the dialog opens and
    /// held, so the totals the user is shown match what SaleService will freeze onto
    /// the sale. Editing an existing sale keeps that sale's own mode (decision 6.4).
    /// </summary>
    private VatMode _modeVat = VatMode.Included;

    [ObservableProperty] private Mode _currentMode;

    [ObservableProperty] private Client? _selectedClient;
    [ObservableProperty] private string _textClient = string.Empty;
    [ObservableProperty] private string? _guestPhone;

    [ObservableProperty] private Worker? _worker;
    [ObservableProperty] private PaymentMethod? _paymentMethod;
    [ObservableProperty] private string? _notes;

    [ObservableProperty] private bool _unregisteredClientNotice;

    /// <summary>Informational only: set when opened from an appointment (pantalles 3.2).</summary>
    [ObservableProperty] private string? _linkedAppointmentText;
    private int? _appointmentId;

    /// <summary>
    /// When this sale happened. Only a brand new sale is stamped with "now": editing an
    /// existing one keeps its own date, and one raised from an appointment takes the
    /// appointment's. Charge used to stamp DateTime.Now in all three modes, so correcting
    /// a typo on an old ticket moved that sale into today — out of its VAT quarter, out of
    /// its month in the reports, and onto today's till balance (decision 6.4: a recorded
    /// sale never moves).
    /// </summary>
    private DateOnly _date = DateOnly.FromDateTime(DateTime.Now);
    private TimeOnly _time = TimeOnly.FromDateTime(DateTime.Now);

    public ObservableCollection<SaleLineViewModel> Lines { get; } = [];

    public ObservableCollection<Client> ActiveClients { get; } = [];
    public ObservableCollection<Service> ActiveServices { get; } = [];
    public ObservableCollection<Product> ActiveProducts { get; } = [];
    public ObservableCollection<Worker> ActiveWorkers { get; } = [];
    public ObservableCollection<PaymentMethod> ActiveMethods { get; } = [];

    [ObservableProperty] private int _baseCents;
    [ObservableProperty] private int _vatCents;
    [ObservableProperty] private int _totalCents;
    [ObservableProperty] private List<RateBreakdown> _breakdown = [];

    public string BaseText => Money.Format(BaseCents);
    public string VatText => Money.Format(VatCents);
    public string TotalText => Money.Format(TotalCents);

    public override string Title => CurrentMode switch
    {
        Mode.Edit => Texts.EditSaleTitle,
        Mode.FromAppointment => Texts.NewSaleFromAppointmentTitle,
        _ => Texts.NewSaleTitle
    };

    private SaleDialogViewModel(
        ISaleService sales, ISoundService so, IClientService clients, IDialogService dialogs)
    {
        _sales = sales;
        _so = so;
        _clients = clients;
        _dialogs = dialogs;
    }

    /// <summary>
    /// Shown only while a free-text name is actually typed. A sale for a guest is
    /// perfectly valid; the reminder just says the history will not be kept, and the
    /// user can turn it off entirely in Configuració (RF-05bis).
    /// </summary>
    private void ReviewGuestNotice()
        => UnregisteredClientNotice = _guestNoticeEnabled
            && SelectedClient is null
            && !string.IsNullOrWhiteSpace(TextClient);

    partial void OnTextClientChanged(string value) => ReviewGuestNotice();
    partial void OnSelectedClientChanged(Client? value) => ReviewGuestNotice();

    /// <summary>Registers the guest without losing the half-filled sale, and selects
    /// the new client, so the reminder does not simply reappear.</summary>
    [RelayCommand]
    private async Task RegisterClientNow()
    {
        var dialog = new ClientDialogViewModel(_clients) { Name = TextClient.Trim() };
        if (GuestPhone is { Length: > 0 } phone) dialog.Mobile = phone;

        if (!await _dialogs.ShowDialog(dialog)) return;

        var created = dialog.AModel();
        created.Id = await _clients.Create(created);

        ActiveClients.Add(created);
        SelectedClient = created;
        TextClient = string.Empty;
        ReviewGuestNotice();
    }

    /// <summary>Empty sale, opened from "+ New sale".</summary>
    public static async Task<SaleDialogViewModel> New(
        ISaleService sales, IClientService clients, ICatalogService catalog,
        IWorkerService workers, ISoundService so, ISettingsService settings,
        IDialogService dialogs)
    {
        var vm = new SaleDialogViewModel(sales, so, clients, dialogs) { CurrentMode = Mode.New };
        vm._modeVat = await settings.CurrentVatMode();
        await vm.LoadOptions(clients, catalog, workers, settings);
        return vm;
    }

    /// <summary>
    /// Prefilled from a completed appointment (RF-09). Takes the appointment itself and
    /// not just its id because the service it was booked for becomes the first sale
    /// line, and a Sale has no service of its own to read that from.
    /// </summary>
    public static async Task<SaleDialogViewModel> FromAppointment(
        ISaleService sales, IClientService clients, ICatalogService catalog,
        IWorkerService workers, ISoundService so, ISettingsService settings,
        IDialogService dialogs, Appointment appointment)
    {
        var vm = new SaleDialogViewModel(sales, so, clients, dialogs) { CurrentMode = Mode.FromAppointment };
        vm._modeVat = await settings.CurrentVatMode();
        await vm.LoadOptions(clients, catalog, workers, settings);

        var sale = await sales.PrepareFromAppointment(appointment.Id);
        vm._appointmentId = appointment.Id;
        // The sale belongs to the appointment's slot, not to the moment it was charged:
        // an appointment closed the next morning still belongs to the day it happened.
        vm._date = sale.Date;
        vm._time = sale.Time;
        vm.LinkedAppointmentText = string.Format(Texts.FromAppointmentOn,
            sale.Date.ToString("dd/MM/yyyy"), sale.Time.ToString("HH\\:mm"));
        vm.SelectedClient = vm.ActiveClients.FirstOrDefault(c => c.Id == sale.ClientId);
        vm.TextClient = sale.GuestName ?? string.Empty;
        vm.GuestPhone = sale.GuestPhone;
        vm.Worker = vm.ActiveWorkers.FirstOrDefault(t => t.Id == sale.WorkerId);

        // The line is a starting point, not a commitment: it stays fully editable, and
        // an appointment with no service simply opens with an empty sale.
        if (appointment.ServiceId is int serviceId
            && vm.ActiveServices.FirstOrDefault(s => s.Id == serviceId) is { } service)
            vm.AddService(service);

        vm.ReviewGuestNotice();
        return vm;
    }

    /// <summary>Existing sale, opened for editing.</summary>
    public static async Task<SaleDialogViewModel> Edit(
        ISaleService sales, IClientService clients, ICatalogService catalog,
        IWorkerService workers, ISoundService so, ISettingsService settings,
        IDialogService dialogs, Sale sale)
    {
        var vm = new SaleDialogViewModel(sales, so, clients, dialogs) { CurrentMode = Mode.Edit };
        vm._modeVat = sale.VatMode;
        await vm.LoadOptions(clients, catalog, workers, settings);

        // Deleting a used worker or payment method deactivates it, and the pickers only
        // carry active ones. Without putting this sale's own back, reopening it would
        // show an empty method and refuse to save, or quietly lose the worker.
        if (sale.WorkerId is int tidSale
            && vm.ActiveWorkers.All(t => t.Id != tidSale)
            && (await workers.GetAll()).FirstOrDefault(t => t.Id == tidSale) is { } inactive)
            vm.ActiveWorkers.Add(inactive);

        if (vm.ActiveMethods.All(m => m.Id != sale.PaymentMethodId)
            && (await catalog.GetMethods())
                .FirstOrDefault(m => m.Id == sale.PaymentMethodId) is { } methodInactive)
            vm.ActiveMethods.Add(methodInactive);

        vm.SelectedClient = sale.ClientId is int cid ? vm.ActiveClients.FirstOrDefault(c => c.Id == cid) : null;
        vm.TextClient = sale.GuestName ?? string.Empty;
        vm.GuestPhone = sale.GuestPhone;
        vm.Worker = sale.WorkerId is int tid ? vm.ActiveWorkers.FirstOrDefault(t => t.Id == tid) : null;
        vm.PaymentMethod = vm.ActiveMethods.FirstOrDefault(m => m.Id == sale.PaymentMethodId);
        vm.Notes = sale.Notes;
        vm._appointmentId = sale.AppointmentId;

        foreach (var line in sale.Lines)
        {
            var lineVm = new SaleLineViewModel
            {
                ServiceId = line.ServiceId, ProductId = line.ProductId,
                Description = line.Description, Quantity = line.Quantity,
                PriceText = Money.FormatExport(line.UnitPriceCents), VatBp = line.VatBp
            };
            vm.AddExistingLine(lineVm);
        }

        vm._id = sale.Id;
        vm._date = sale.Date;
        vm._time = sale.Time;
        vm.ReviewGuestNotice();
        return vm;
    }

    private async Task LoadOptions(IClientService clients, ICatalogService catalog,
        IWorkerService workers, ISettingsService settings)
    {
        _guestNoticeEnabled = await settings.GetBool(ConfigKeys.ShowGuestNotice, true);

        foreach (var c in await clients.GetActive()) ActiveClients.Add(c);
        foreach (var s in await catalog.GetServices(onlyActive: true)) ActiveServices.Add(s);
        foreach (var p in await catalog.GetProducts(onlyActive: true)) ActiveProducts.Add(p);
        foreach (var t in await workers.GetAll(onlyActive: true)) ActiveWorkers.Add(t);
        foreach (var m in await catalog.GetMethods(onlyActive: true)) ActiveMethods.Add(m);
    }

    [RelayCommand] private void AddService(Service service) => AddExistingLine(SaleLineViewModel.FromService(service));
    [RelayCommand] private void AddProduct(Product product) => AddExistingLine(SaleLineViewModel.FromProduct(product));
    [RelayCommand] private void AddCustomConcept() => AddExistingLine(SaleLineViewModel.Free());

    [RelayCommand]
    private void RemoveLine(SaleLineViewModel line)
    {
        line.Changed -= RecomputeTotals;
        Lines.Remove(line);
        RecomputeTotals();
    }

    private void AddExistingLine(SaleLineViewModel line)
    {
        line.Changed += RecomputeTotals;
        Lines.Add(line);
        RecomputeTotals();
    }

    private bool CanCharge() =>
        Lines.Count > 0
        && PaymentMethod is not null
        && (SelectedClient is not null || !string.IsNullOrWhiteSpace(TextClient));

    /// <summary>Set while the lines add up to more than one sale can hold. Charge
    /// refuses, and the footer stops recomputing rather than throwing mid-keystroke.</summary>
    [ObservableProperty] private bool _totalTooLarge;

    [RelayCommand]
    private void RecomputeTotals()
    {
        var lines = Lines.Select(l => l.AModel()).ToList();

        // Asked before computing rather than caught afterwards: ComputeByRate sums each
        // rate group with a checked Enumerable.Sum, so an over-large group throws rather
        // than returning a wrong number — and this runs on every keystroke.
        TotalTooLarge = !VatCalculator.FitsInOneSale(lines);
        if (TotalTooLarge) return;

        var d = VatCalculator.Compute(lines, _modeVat);
        BaseCents = d.BaseCents;
        VatCents = d.VatCents;
        TotalCents = d.TotalCents;
        Breakdown = VatCalculator.ComputeByRate(lines, _modeVat);
        OnPropertyChanged(nameof(BaseText));
        OnPropertyChanged(nameof(VatText));
        OnPropertyChanged(nameof(TotalText));
    }

    [RelayCommand]
    private async Task Charge()
    {
        if (!CanCharge())
        {
            ErrorValidation = Lines.Count == 0
                ? Texts.AtLeastOneLineRequired
                : Texts.PaymentMethodRequired;
            return;
        }

        // Every line is checked before anything is charged: a half-typed quantity or
        // VAT rate would otherwise be read as zero and freeze a wrong figure onto the
        // sale, which no later edit of the catalogue can put right.
        if (Lines.FirstOrDefault(l => !l.IsValid) is { } wrong)
        {
            ErrorValidation = string.IsNullOrWhiteSpace(wrong.Description) ? Texts.LineDescriptionRequired
                : !NumberValidator.TryParseAtLeast(wrong.QuantityText, 1, out _) ? Texts.QuantityInvalid
                : !Money.TryParse(wrong.PriceText, out _) ? Texts.PriceInvalid
                : !Percentages.TryParse(wrong.VatText, out _) ? Texts.VatOutOfRange
                : Texts.LineAmountTooLarge;
            return;
        }

        var lines = Lines.Select(l => l.AModel()).ToList();

        // Each line is bounded on its own above; this is the only check of what they
        // come to together. A VAT-rate group is summed with a checked Enumerable.Sum,
        // so without this the sale would throw on the way to the database instead of
        // being refused with something the user can act on.
        if (!VatCalculator.FitsInOneSale(lines))
        {
            ErrorValidation = Texts.SaleTotalTooLarge;
            return;
        }

        ErrorValidation = null;

        var sale = new Sale
        {
            Id = _id ?? 0,
            Date = _date,
            Time = _time,
            ClientId = SelectedClient?.Id,
            GuestName = SelectedClient is null ? TextClient.Trim() : null,
            GuestPhone = SelectedClient is null ? GuestPhone : null,
            WorkerId = Worker?.Id,
            PaymentMethodId = PaymentMethod!.Id,
            Notes = string.IsNullOrWhiteSpace(Notes) ? null : Notes.Trim(),
            AppointmentId = _appointmentId
        };

        if (CurrentMode == Mode.Edit) await _sales.Update(sale, lines);
        else await _sales.Create(sale, lines);

        await _so.PlayConfirmation();
        RequestClose(true);
    }

    [RelayCommand]
    private async Task VoidSale()
    {
        if (_id is int id)
        {
            await _sales.Void(id);
            RequestClose(true);
        }
    }

    [RelayCommand]
    private void Cancel() => RequestClose(false);
}
