using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EvaGest.Helpers;
using EvaGest.Models;
using EvaGest.Services;
using EvaGest.ViewModels.Elements;
using EvaGest.Resources;

namespace EvaGest.ViewModels.Dialogs;

/// <summary>
/// Appointment (new / edit), pantalles 3.1. What is special: warnings recompute live as
/// date/time/worker change, and NONE of them ever blocks saving (casos-us CU-01).
/// The hour is picked on the same weekly grid the Agenda page uses, with a ghost block
/// showing where this appointment would land.
/// </summary>
public partial class AppointmentDialogViewModel : DialogViewModelBase
{
    private readonly IAppointmentService _appointments;
    private readonly IAvailabilityService _availability;
    private readonly IClientService _clients;
    private readonly IDialogService _dialogs;
    private readonly int? _id;

    /// <summary>Whether the guest reminder is switched on (RF-05bis). Read once when
    /// the dialog opens; off by default until the settings have loaded, so the warning
    /// never flashes up before it is known to be wanted.</summary>
    private bool _guestNoticeEnabled;
    private readonly int? _originalClientId;
    private readonly int? _originalServiceId;
    private readonly int? _originalWorkerId;
    private readonly AppointmentStatus _originalStatus = AppointmentStatus.Pending;

    [ObservableProperty] private DateOnly _date;
    [ObservableProperty] private TimeOnly _time;
    [ObservableProperty] private int _durationMin = 30;

    /// <summary>
    /// What the hour and duration boxes are actually bound to. They are text, not a
    /// TimeOnly and an int: binding those types directly let WPF's culture-aware
    /// converter accept "10.00" and "20-00" and quietly turn them into times nobody
    /// typed. The text is parsed here instead, strictly, and a value that does not
    /// parse leaves <see cref="Time"/> and <see cref="DurationMin"/> untouched until
    /// Save refuses it with a message that says what the field expects.
    /// </summary>
    [ObservableProperty] private string _timeText = string.Empty;
    [ObservableProperty] private string _durationText = string.Empty;

    /// <summary>Guards the two-way sync between the typed text and the parsed value,
    /// so pushing one into the other does not bounce straight back.</summary>
    private bool _syncingText;

    /// <summary>The client box: an existing client searched or browsed, or a new one's name.</summary>
    public ClientPickerViewModel ClientPicker { get; }

    /// <summary>The registered client picked in <see cref="ClientPicker"/>, or null for a guest.</summary>
    public Client? SelectedClient
    {
        get => ClientPicker.SelectedClient;
        set => ClientPicker.Select(value);
    }

    /// <summary>The guest's free name; empty while a registered client is picked.
    /// Writing it makes the appointment a guest's again.</summary>
    public string TextClient
    {
        get => ClientPicker.GuestName;
        set { if (value != TextClient) ClientPicker.SetGuestName(value); }
    }

    [ObservableProperty] private string? _guestPhone;

    [ObservableProperty] private Service? _service;
    [ObservableProperty] private Worker? _worker;
    [ObservableProperty] private string? _notes;

    // Non-blocking inline warnings (pantalles 3.1)
    [ObservableProperty] private bool _overlapNotice;
    [ObservableProperty] private bool _outsideScheduleNotice;
    [ObservableProperty] private bool _closedDayNotice;
    [ObservableProperty] private string? _closedDayReason;
    [ObservableProperty] private bool _unregisteredClientNotice;

    [ObservableProperty] private WeekGridViewModel? _grid;

    public ObservableCollection<Service> ActiveServices { get; } = [];
    public ObservableCollection<Worker> ActiveWorkers { get; } = [];

    /// <summary>The phone box belongs to a new client (a guest); a registered one already has one.</summary>
    public bool CanWriteGuest => ClientPicker.IsNewClient;

    public override string Title => _id is null ? Texts.NewAppointmentTitle : Texts.EditAppointmentTitle;

    /// <summary>Awaited by the tests; the views let it run in the background.</summary>
    public Task Initialization { get; }

    public AppointmentDialogViewModel(IAppointmentService appointments, IAvailabilityService availability,
        IClientService clients, ICatalogService catalog, IWorkerService workers,
        ISettingsService settings, IDialogService dialogs,
        DateOnly dateInitial, TimeOnly? initialTime = null)
        : this(appointments, availability, clients, catalog, workers, settings, dialogs,
               null, dateInitial, initialTime) { }

    public AppointmentDialogViewModel(IAppointmentService appointments, IAvailabilityService availability,
        IClientService clients, ICatalogService catalog, IWorkerService workers,
        ISettingsService settings, IDialogService dialogs, Appointment appointment)
        : this(appointments, availability, clients, catalog, workers, settings, dialogs,
               appointment, appointment.Date, appointment.Time) { }

    /// <summary>
    /// Both modes share one body on purpose. Chaining the edit constructor to the new one
    /// used to leave the appointment's fields unset while the background load was already
    /// running, so the client could not be matched by the time the list arrived.
    /// </summary>
    private AppointmentDialogViewModel(IAppointmentService appointments, IAvailabilityService availability,
        IClientService clients, ICatalogService catalog, IWorkerService workers,
        ISettingsService settings, IDialogService dialogs,
        Appointment? appointment, DateOnly dateInitial, TimeOnly? initialTime)
    {
        _appointments = appointments;
        _availability = availability;
        _clients = clients;
        _dialogs = dialogs;
        Date = dateInitial;
        Time = initialTime ?? new TimeOnly(10, 0);
        // DurationMin is a field initialiser, which does not raise OnDurationMinChanged,
        // so the box would open empty without this.
        Sync(() => DurationText = DurationMin.ToString());

        ClientPicker = new ClientPickerViewModel(dialogs);
        ClientPicker.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ClientPickerViewModel.SelectedClient)) OnSelectedClientChanged();
            else if (e.PropertyName == nameof(ClientPickerViewModel.IsNewClient)) OnPropertyChanged(nameof(CanWriteGuest));
            else if (e.PropertyName == nameof(ClientPickerViewModel.GuestName))
            {
                OnPropertyChanged(nameof(TextClient));
                ReviewGuestNotice();
            }
        };

        if (appointment is not null)
        {
            _id = appointment.Id;
            _originalClientId = appointment.ClientId;
            _originalStatus = appointment.Status;
            _originalServiceId = appointment.ServiceId;
            _originalWorkerId = appointment.WorkerId;
            TextClient = appointment.GuestName ?? string.Empty;
            if (appointment.Client is null) GuestPhone = appointment.GuestPhone;
            Notes = appointment.Notes;
            // Last, and after the service: picking a service overwrites the duration,
            // and an edited appointment must keep the duration it was saved with.
            DurationMin = appointment.DurationMin;
        }

        Initialization = Initialize(clients, catalog, workers, settings);
    }

    private async Task Initialize(IClientService clients, ICatalogService catalog,
        IWorkerService workers, ISettingsService settings)
    {
        _guestNoticeEnabled = await settings.GetBool(ConfigKeys.ShowGuestNotice, true);

        // A new appointment starts at the configured default; an edited one keeps the
        // duration it was saved with, and a service overrides both further down.
        if (_id is null)
            DurationMin = await settings.GetInt(ConfigKeys.DefaultAppointmentDurationMin, 30);

        ClientPicker.SetClients(await clients.GetActive());
        foreach (var s in await catalog.GetServices(onlyActive: true)) ActiveServices.Add(s);
        foreach (var t in await workers.GetAll(onlyActive: true)) ActiveWorkers.Add(t);

        // Deleting a used catalogue entry deactivates it, so an appointment booked before
        // that must still show what it was booked with: leaving it out of the lists would
        // blank the combo box and drop the reference on the next save.
        if (_originalServiceId is int originalServiceId
            && ActiveServices.All(s => s.Id != originalServiceId)
            && await catalog.GetService(originalServiceId) is { } serviceInactive)
            ActiveServices.Add(serviceInactive);

        if (_originalWorkerId is int originalWorkerId
            && ActiveWorkers.All(t => t.Id != originalWorkerId)
            && (await workers.GetAll())
                .FirstOrDefault(t => t.Id == originalWorkerId) is { } workerInactive)
            ActiveWorkers.Add(workerInactive);

        // Resolve every selection by id, never by reference: the appointment's related
        // entities come from a different AsNoTracking query than these lists, so the
        // instances are not equal and the combo boxes would render empty.
        // An asleep client is left out of the search like any other picker, but an
        // appointment already booked for them must keep them rather than turn guest.
        if (_originalClientId is int clientId && await clients.GetById(clientId) is { } client)
        {
            ClientPicker.Add(client);
            SelectedClient = client;
        }

        int savedDuration = DurationMin;
        if (_originalServiceId is int serviceId)
            Service = ActiveServices.FirstOrDefault(s => s.Id == serviceId);
        if (_originalWorkerId is int id)
            Worker = ActiveWorkers.FirstOrDefault(t => t.Id == id);
        DurationMin = savedDuration;   // picking the service above resets it

        Grid = new WeekGridViewModel(_appointments, _availability, settings, ModeGrid.Selector,
            onSlotClick: (date, time) => { Date = date; Time = time; },
            onAppointmentClick: null)
        {
            SelectedAppointmentId = _id
        };
        await Grid.LoadRange(Date);
        Grid.ShowGhost(Date, Time, DurationMin);

        ReviewGuestNotice();
        await ReviewNotices();
    }

    /// <summary>
    /// Shown only while a free-text name is actually typed: an empty guest field is not
    /// a guest yet. Never blocks saving — it offers to register, nothing more (CU-01).
    /// </summary>
    private void ReviewGuestNotice()
        => UnregisteredClientNotice = _guestNoticeEnabled
            && SelectedClient is null
            && !string.IsNullOrWhiteSpace(TextClient);

    /// <summary>
    /// Registers the guest without losing the half-filled appointment, and selects the
    /// new client straight away, so the reminder does not simply reappear.
    /// </summary>
    [RelayCommand]
    private async Task RegisterClientNow()
    {
        var dialog = new ClientDialogViewModel(_clients) { Name = TextClient.Trim() };
        if (GuestPhone is { Length: > 0 } phone) dialog.Mobile = phone;

        if (!await _dialogs.ShowDialog(dialog)) return;

        var created = dialog.AModel();
        created.Id = await _clients.Create(created);

        ClientPicker.Add(created);
        SelectedClient = created;
    }

    partial void OnServiceChanged(Service? value)
    {
        if (value?.DurationMin is int d) DurationMin = d;
    }

    private void OnSelectedClientChanged()
    {
        var value = SelectedClient;
        if (value is not null)
        {
            GuestPhone = value.Mobile;
        }
        else if (PreviouslySelectedClient is not null)
        {
            // Going back to a guest must not leave the previous client's phone behind
            GuestPhone = null;
        }

        PreviouslySelectedClient = value;
        OnPropertyChanged(nameof(CanWriteGuest));
        OnPropertyChanged(nameof(SelectedClient));
        ReviewGuestNotice();
    }

    private Client? PreviouslySelectedClient { get; set; }

    partial void OnDateChanged(DateOnly value)
    {
        _ = SyncGrid(value);
        _ = ReviewNotices();
    }

    partial void OnTimeChanged(TimeOnly value)
    {
        Sync(() => TimeText = ScheduleHelper.Format(value));
        Grid?.ShowGhost(Date, value, DurationMin);
        _ = ReviewNotices();
    }

    partial void OnDurationMinChanged(int value)
    {
        Sync(() => DurationText = value.ToString());
        Grid?.ShowGhost(Date, Time, Math.Max(1, value));
        _ = ReviewNotices();
    }

    // Only a value that parses moves the appointment. Anything else just sits in the
    // box until Save explains what is wrong with it.
    partial void OnTimeTextChanged(string value)
    {
        if (_syncingText) return;
        if (TimeValidator.IsValidTime(value.Trim(), out var parsed)) Time = parsed;
    }

    partial void OnDurationTextChanged(string value)
    {
        if (_syncingText) return;
        if (NumberValidator.TryParseAtLeast(value, 1, out int parsed)) DurationMin = parsed;
    }

    private void Sync(Action write)
    {
        _syncingText = true;
        try { write(); }
        finally { _syncingText = false; }
    }

    partial void OnWorkerChanged(Worker? value) => _ = ReviewNotices();

    private async Task SyncGrid(DateOnly date)
    {
        if (Grid is not { } grid) return;

        // Only reload when the date has left the days on screen: a click on another
        // visible column must not shift the three-day view under the user's cursor.
        if (!grid.Days.Any(d => d.Date == date)) await grid.LoadRange(date);
        grid.ShowGhost(date, Time, DurationMin);
    }

    private async Task ReviewNotices()
    {
        var r = await _availability.Check(Date, Time, DurationMin, Worker?.Id, _id);
        OverlapNotice = r.HasOverlap;
        OutsideScheduleNotice = r.OutsideSchedule;
        ClosedDayNotice = r.ClosedDay;
        ClosedDayReason = r.ClosedDayReason;
    }

    [RelayCommand]
    private async Task Save()
    {
        var client = SelectedClient;
        if (client is null && string.IsNullOrWhiteSpace(TextClient))
        {
            // Say what is missing in the mode the user is in: a name to type, or a
            // client still to be picked from the search.
            ErrorValidation = ClientPicker.IsNewClient ? Texts.GuestNameOrClientRequired : Texts.ClientNotPicked;
            return;
        }
        // The text is what the user is looking at, so it is what gets judged: checking
        // the parsed value instead would pass a box reading "10.00" as a valid 10:00.
        if (!TimeValidator.IsValidTime(TimeText.Trim(), out var time))
        {
            ErrorValidation = Texts.TimeInvalid;
            return;
        }
        if (!NumberValidator.TryParseAtLeast(DurationText, 1, out int durationMin))
        {
            ErrorValidation = Texts.DurationInvalid;
            return;
        }
        if (client is null && !string.IsNullOrWhiteSpace(GuestPhone) && !ContactValidator.IsValidPhone(GuestPhone))
        {
            ErrorValidation = Texts.PhoneInvalid;
            return;
        }

        ErrorValidation = null;

        var appointment = new Appointment
        {
            Id = _id ?? 0,
            Date = Date,
            Time = time,
            DurationMin = durationMin,
            ClientId = client?.Id,
            GuestName = client is null ? TextClient.Trim() : null,
            GuestPhone = client is null ? GuestPhone : null,
            ServiceId = Service?.Id,
            WorkerId = Worker?.Id,
            Notes = string.IsNullOrWhiteSpace(Notes) ? null : Notes.Trim(),
            Status = _originalStatus
        };

        if (_id is null) await _appointments.Create(appointment);
        else await _appointments.Update(appointment);

        RequestClose(true);
    }

    /// <summary>Only an appointment that exists can be deleted; a half-filled new one is
    /// discarded with Cancel·lar.</summary>
    public bool CanDelete => _id is not null;

    [RelayCommand]
    private async Task Delete()
    {
        if (_id is not int id) return;

        bool confirmed = await _dialogs.Confirm(
            Texts.DeleteAppointmentTitle,
            Texts.DeleteAppointmentMessage,
            Texts.Delete);

        if (!confirmed) return;

        if (await _appointments.Delete(id) == DeleteResult.Blocked)
        {
            ErrorValidation = Texts.AppointmentHasSale;
            return;
        }

        RequestClose(true);
    }

    [RelayCommand]
    private void Cancel() => RequestClose(false);
}
