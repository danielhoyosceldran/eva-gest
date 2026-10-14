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

    /// <summary>The rate a custom concept line starts on (ConfigKeys.DefaultVatBp).</summary>
    private int _defaultVatBp = 2100;

    /// <summary>
    /// The VAT mode the live footer computes in. Read once when the dialog opens and
    /// held, so the totals the user is shown match what SaleService will freeze onto
    /// the sale. Editing an existing sale keeps that sale's own mode (decision 6.4).
    /// </summary>
    private VatMode _modeVat = VatMode.Included;

    [ObservableProperty] private Mode _currentMode;

    /// <summary>The client box: an existing client searched or browsed, or a new one's name.</summary>
    public ClientPickerViewModel ClientPicker { get; }

    /// <summary>The registered client picked in <see cref="ClientPicker"/>, or null for a guest.</summary>
    public Client? SelectedClient
    {
        get => ClientPicker.SelectedClient;
        set => ClientPicker.Select(value);
    }

    /// <summary>The guest's free name; empty while a registered client is picked.
    /// Writing it makes the sale a guest's again.</summary>
    public string TextClient
    {
        get => ClientPicker.GuestName;
        set { if (value != TextClient) ClientPicker.SetGuestName(value); }
    }

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

    public ObservableCollection<Service> ActiveServices { get; } = [];
    public ObservableCollection<Product> ActiveProducts { get; } = [];
    public ObservableCollection<Worker> ActiveWorkers { get; } = [];
    public ObservableCollection<PaymentMethod> ActiveMethods { get; } = [];

    /// <summary>How many quick buttons sit above the search box: exactly one row of
    /// six across the 760 DIP dialog, so they never cost a second row of height.</summary>
    public const int QuickServiceCount = 6;

    /// <summary>One quick button: the service, and its price already formatted for the label.</summary>
    public sealed record QuickService(Service Service, string PriceText)
    {
        public string Name => Service.Name;
    }

    /// <summary>The shop's most sold services, one click each (the quick buttons).</summary>
    public ObservableCollection<QuickService> QuickServices { get; } = [];

    public SearchPickerViewModel<Worker> WorkerPicker { get; }

    /// <summary>Up to this many workers, the sale shows one button per worker instead of
    /// the search box and its list: with one or two there is nothing to search.</summary>
    public const int WorkerButtonLimit = 2;

    /// <summary>
    /// True while the shop has one or two workers to choose from: the view then shows
    /// them as buttons, only one chosen at a time. A third worker brings the search box
    /// back. Counted on the list the sale offers, so editing a sale whose worker has
    /// since left counts that worker too.
    /// </summary>
    public bool ShowsWorkerButtons => ActiveWorkers.Count is > 0 and <= WorkerButtonLimit;

    /// <summary>The one search box over services and products; a pick becomes a line.</summary>
    public CatalogAdderViewModel CatalogAdder { get; }

    partial void OnWorkerChanged(Worker? value) => WorkerPicker.Show(value);

    /// <summary>Drives the empty state shown in place of the lines.</summary>
    public bool HasLines => Lines.Count > 0;

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

    /// <summary>
    /// The main button's verb. Editing a sale already charged saves the correction and
    /// moves no money, so it no longer says "Cobrar" as if charging again (B-36).
    /// </summary>
    public string ChargeText => CurrentMode == Mode.Edit ? Texts.SaveSale : Texts.Charge;

    /// <summary>Always as tall as the screen: the lines list takes the free height and
    /// scrolls by itself, so the totals and Cobrar never move.</summary>
    public override bool FillsHeight => true;

    private SaleDialogViewModel(
        ISaleService sales, ISoundService so, IClientService clients, IDialogService dialogs)
    {
        _sales = sales;
        _so = so;
        _clients = clients;
        _dialogs = dialogs;

        // Search boxes over the lists that grow long, with the whole list a button away.
        // Their OnXChanged below mirror the property back into the box.
        WorkerPicker = new(ActiveWorkers, w => w.Name, w => Worker = w, dialogs, Texts.WorkerBrowserTitle);
        CatalogAdder = new(ActiveServices, ActiveProducts, AddPicked, dialogs);
        Lines.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasLines));
        ActiveWorkers.CollectionChanged += (_, _) => OnPropertyChanged(nameof(ShowsWorkerButtons));

        // Sales, unlike appointments, may be charged to nobody at all
        ClientPicker = new ClientPickerViewModel(dialogs, allowsNoClient: true);
        ClientPicker.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ClientPickerViewModel.SelectedClient))
            {
                OnPropertyChanged(nameof(SelectedClient));
                ReviewGuestNotice();
            }
            else if (e.PropertyName == nameof(ClientPickerViewModel.GuestName))
            {
                OnPropertyChanged(nameof(TextClient));
                ReviewGuestNotice();
            }
        };
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

    /// <summary>Registers the guest without losing the half-filled sale, and selects
    /// the new client, so the reminder does not simply reappear.</summary>
    [RelayCommand]
    private async Task RegisterClientNow()
    {
        var dialog = new ClientDialogViewModel(_clients, _dialogs, TextClient.Trim());
        if (GuestPhone is { Length: > 0 } phone) dialog.Mobile = phone;

        // Saved by the client dialog itself, so a failed write keeps it open with what
        // was typed instead of losing it behind the generic error (E-09).
        Client? created = null;
        dialog.Persist = async () =>
        {
            var model = dialog.AModel();
            model.Id = await _clients.Create(model);
            created = model;
        };
        if (!await _dialogs.ShowDialog(dialog) || created is null) return;

        ClientPicker.Add(created);
        SelectedClient = created;
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
        vm.MarkOpened();
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
        vm.ShowOwnGuest(sale);
        await vm.SelectOwnClient(clients, sale.ClientId);
        vm.GuestPhone = sale.GuestPhone;
        vm.Worker = vm.ActiveWorkers.FirstOrDefault(t => t.Id == sale.WorkerId);

        // The line is a starting point, not a commitment: it stays fully editable, and
        // an appointment with no service simply opens with an empty sale.
        if (appointment.ServiceId is int serviceId
            && vm.ActiveServices.FirstOrDefault(s => s.Id == serviceId) is { } service)
            vm.AddExistingLine(SaleLineViewModel.FromService(service));

        vm.ReviewGuestNotice();
        vm.MarkOpened();
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

        vm.ShowOwnGuest(sale);
        await vm.SelectOwnClient(clients, sale.ClientId);
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
        vm.MarkOpened();
        return vm;
    }

    private async Task LoadOptions(IClientService clients, ICatalogService catalog,
        IWorkerService workers, ISettingsService settings)
    {
        _guestNoticeEnabled = await settings.GetBool(ConfigKeys.ShowGuestNotice, true);
        _defaultVatBp = await settings.GetInt(ConfigKeys.DefaultVatBp, 2100);

        ClientPicker.SetClients(await clients.GetActive());
        foreach (var s in await catalog.GetServices(onlyActive: true)) ActiveServices.Add(s);
        foreach (var p in await catalog.GetProducts(onlyActive: true)) ActiveProducts.Add(p);
        foreach (var t in await workers.GetAll(onlyActive: true)) ActiveWorkers.Add(t);
        foreach (var m in await catalog.GetMethods(onlyActive: true)) ActiveMethods.Add(m);
        foreach (var s in await catalog.GetBestSellingServices(QuickServiceCount)) QuickServices.Add(new(s, Money.Format(s.PriceCents)));
    }

    /// <summary>
    /// Picks the client the sale (or its appointment) already belongs to. Looked up by
    /// id rather than among the active ones: an asleep client is left out of the search,
    /// but a sale already made for them must keep them rather than turn guest.
    /// </summary>
    private async Task SelectOwnClient(IClientService clients, int? clientId)
    {
        if (clientId is not int id || await clients.GetById(id) is not { } client) return;
        ClientPicker.Add(client);
        SelectedClient = client;
    }

    /// <summary>
    /// Puts a saved sale's guest back in the picker: the "no client" option for one
    /// charged to nobody, so the marker never shows up as a typed name, or the guest's
    /// name otherwise.
    /// </summary>
    private void ShowOwnGuest(Sale sale)
    {
        if (sale.IsNoClient) ClientPicker.IsNoClient = true;
        else TextClient = sale.GuestName ?? string.Empty;
    }

    /// <summary>A quick button: adds that service as a line.</summary>
    [RelayCommand]
    private Task AddQuickService(QuickService quick) => AddServiceLine(quick.Service);

    /// <summary>
    /// What the search box hands over when something in it is picked. async void because
    /// the search box reports a pick synchronously, like an event; an exception still
    /// reaches the global handler in App.xaml.cs, which logs it and tells the user.
    /// </summary>
    private async void AddPicked(object picked)
    {
        switch (picked)
        {
            case Service service: await AddServiceLine(service); break;
            case Product product: AddCatalogLine(SaleLineViewModel.FromProduct(product)); break;
            case CatalogAdderViewModel.FreeConcept free:
                AddExistingLine(SaleLineViewModel.Free(_defaultVatBp, free.Description));
                break;
        }
    }

    /// <summary>
    /// Adds a service, asking first when the sale already has it: a client does not
    /// usually have two haircuts, so a second one is more often a double click or a
    /// mis-tap than a real sale. Products are not asked about — two bottles of the same
    /// shampoo are normal. The line's own + button never asks: that is deliberate.
    /// </summary>
    private async Task AddServiceLine(Service service)
    {
        if (Lines.Any(l => l.ServiceId == service.Id)
            && !await _dialogs.Confirm(Texts.DuplicateServiceTitle,
                   string.Format(Texts.DuplicateServiceMessage, service.Name),
                   Texts.AddAnotherOne, Texts.DontAdd))
            return;

        AddCatalogLine(SaleLineViewModel.FromService(service));
    }

    /// <summary>
    /// Adds a catalogue line, or raises the quantity of the line already holding the
    /// same item at the same price: two cuts are one line of two, not two lines. A line
    /// whose price or VAT was changed by hand is left alone (see
    /// <see cref="SaleLineViewModel.IsSameItemAs"/>).
    /// </summary>
    private void AddCatalogLine(SaleLineViewModel line)
    {
        if (Lines.FirstOrDefault(l => l.IsSameItemAs(line)) is { } existing)
            existing.Increase();
        else
            AddExistingLine(line);
    }

    [RelayCommand] private void AddCustomConcept() => AddExistingLine(SaleLineViewModel.Free(_defaultVatBp));

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
        && (SelectedClient is not null || ClientPicker.IsNoClient || !string.IsNullOrWhiteSpace(TextClient));

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
            // Checked in the same order as CanCharge. The client used to be missing
            // here, so a sale with no client was refused as missing a payment method.
            // The sale's own wording names "Sense client" too: a counter sale is the
            // commonest reason to have no client, and the appointment's message left
            // that way out (B-38).
            ErrorValidation = Lines.Count == 0 ? Texts.AtLeastOneLineRequired
                : SelectedClient is null && !ClientPicker.IsNoClient && string.IsNullOrWhiteSpace(TextClient)
                    ? (ClientPicker.IsNewClient ? Texts.SaleGuestNameOrClientRequired : Texts.SaleClientNotPicked)
                : Texts.PaymentMethodRequired;
            return;
        }

        // The "no client" marker is reserved: typed as a guest's name, the sale would
        // read back as charged to nobody.
        if (SelectedClient is null && !ClientPicker.IsNoClient
            && string.Equals(TextClient.Trim(), Sale.NoClientName, StringComparison.OrdinalIgnoreCase))
        {
            ErrorValidation = Texts.GuestNameReserved;
            return;
        }

        // Every line is checked before anything is charged: a half-typed quantity or
        // VAT rate would otherwise be read as zero and freeze a wrong figure onto the
        // sale, which no later edit of the catalogue can put right.
        if (Lines.FirstOrDefault(l => !l.IsValid) is { } wrong)
        {
            // The faulty box may be behind the line's pencil button: open it, so the
            // message points at something the user can actually see.
            wrong.IsExpanded = true;
            ErrorValidation = string.IsNullOrWhiteSpace(wrong.Description) ? Texts.LineDescriptionRequired
                : !NumberValidator.TryParseAtLeast(wrong.QuantityText, 1, out _) ? Texts.QuantityInvalid
                : string.IsNullOrWhiteSpace(wrong.PriceText) ? Texts.PriceRequired
                : !Money.TryParse(wrong.PriceText, out int wrongPrice) ? Texts.PriceInvalid
                : wrongPrice < 0 ? Texts.PriceNegative
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
            // A guest's name, the "no client" marker, or nothing for a registered client:
            // ck_sales_client_xor wants exactly one of ClientId and GuestName.
            GuestName = SelectedClient is not null ? null
                : ClientPicker.IsNoClient ? Sale.NoClientName
                : TextClient.Trim(),
            GuestPhone = SelectedClient is null && !ClientPicker.IsNoClient ? GuestPhone : null,
            WorkerId = Worker?.Id,
            PaymentMethodId = PaymentMethod!.Id,
            Notes = string.IsNullOrWhiteSpace(Notes) ? null : Notes.Trim(),
            AppointmentId = _appointmentId
        };

        if (CurrentMode == Mode.Edit) await _sales.Update(sale, lines);
        else await _sales.Create(sale, lines);

        // Closed as soon as the sale is stored, before anything else can go wrong: a
        // dialog left open on a saved sale offers Cobrar again, and a second click
        // charges the same sale twice (E-07). The chime is only a confirmation.
        RequestClose(true);
        await _so.PlayConfirmation();
    }

    [RelayCommand]
    private async Task VoidSale()
    {
        if (_id is int id)
        {
            // Voided straight from the button with no question at all; now it asks with
            // the owner's PIN, like every other void and delete (F-05).
            bool confirmed = await _dialogs.ConfirmWithOwnerPin(
                Texts.ConfirmVoidSaleTitle,
                string.Format(Texts.ConfirmVoidSaleMessage, Money.Format(TotalCents)),
                Texts.Void);
            if (!confirmed) return;

            await _sales.Void(id);
            RequestClose(true);
        }
    }

    /// <summary>Esc and the X button reach the same question through the window.</summary>
    [RelayCommand]
    private async Task Cancel()
    {
        if (await CanDiscard()) RequestClose(false);
    }

    protected override IDialogService DiscardDialogs => _dialogs;

    /// <summary>Everything Charge would save: the lines as typed, who, how and the notes.</summary>
    protected override string EditableState()
        => string.Join("|",
            SelectedClient?.Id, ClientPicker.Mode, TextClient, GuestPhone, Worker?.Id, PaymentMethod?.Id, Notes,
            string.Join(";", Lines.Where(l => !IsUntouchedFreeLine(l)).Select(l =>
                $"{l.ServiceId}/{l.ProductId}/{l.Description}/{l.QuantityText}/{l.PriceText}/{l.VatText}")));

    /// <summary>A free-concept line the user added but has not written anything in yet. It
    /// is not something typed, so it must not make Esc ask whether to discard.</summary>
    private static bool IsUntouchedFreeLine(SaleLineViewModel line)
        => line.ServiceId is null && line.ProductId is null
           && string.IsNullOrWhiteSpace(line.Description)
           && (string.IsNullOrWhiteSpace(line.PriceText)
               || (Money.TryParse(line.PriceText, out int cents) && cents == 0));
}
