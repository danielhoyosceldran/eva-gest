using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EvaGest.Helpers;
using EvaGest.Models;
using EvaGest.Services;
using EvaGest.ViewModels.Elements;
using EvaGest.Resources;
using Serilog;

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

    /// <summary>The slot an edited appointment was saved with, so Save only asks about an
    /// overlap the edit itself created, not one the user already accepted before.</summary>
    private readonly (DateOnly Date, TimeOnly Time, int DurationMin)? _originalSlot;

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

    /// <summary>Awaited by the tests and by <see cref="Save"/>; the views let it run in
    /// the background. Never faults: a failure is logged and shown in the dialog (E-03).</summary>
    public Task Initialization { get; }

    /// <summary>Set once <see cref="Initialize"/> has filled the dialog in completely.
    /// Until then the form is not what the appointment holds, so it cannot be saved.</summary>
    private bool _initialized;

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
            _originalSlot = (appointment.Date, appointment.Time, appointment.DurationMin);
            TextClient = appointment.GuestName ?? string.Empty;
            if (appointment.Client is null) GuestPhone = appointment.GuestPhone;
            Notes = appointment.Notes;
            // Last, and after the service: picking a service overwrites the duration,
            // and an edited appointment must keep the duration it was saved with.
            DurationMin = appointment.DurationMin;
        }

        Initialization = InitializeObserved(clients, catalog, workers, settings);
    }

    /// <summary>
    /// Runs <see cref="Initialize"/> and owns its failure (E-03). Nothing in the app awaits
    /// the loading task, so an exception in it used to go unobserved: the dialog opened
    /// half empty with no message and no log line, and saving an edited appointment then
    /// wrote its service and worker back as empty. Now the failure is logged, the dialog
    /// says to reopen it, and <see cref="Save"/> refuses until a load has completed.
    /// </summary>
    private async Task InitializeObserved(IClientService clients, ICatalogService catalog,
        IWorkerService workers, ISettingsService settings)
    {
        try
        {
            await Initialize(clients, catalog, workers, settings);
            _initialized = true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Appointment dialog could not load (appointment {AppointmentId})", _id);
            ErrorValidation = Texts.AppointmentNotLoaded;
        }
    }

    /// <summary>
    /// Starts a refresh that follows an edit (the week picker, the availability notices)
    /// without awaiting it, but keeps its failure: these used to be bare <c>_ = ...</c>
    /// calls whose exceptions vanished. They only inform, and Save checks availability
    /// again itself, so a failure is logged and the form carries on.
    /// </summary>
    private void RefreshInBackground(Func<Task> refresh, string what) => _ = RefreshQuietly(refresh, what);

    private async Task RefreshQuietly(Func<Task> refresh, string what)
    {
        try
        {
            await refresh();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Appointment dialog could not refresh {What} (appointment {AppointmentId})", what, _id);
        }
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

        // Everything above (defaults, the client, service and worker picked by id) is
        // the dialog opening, not the user changing anything.
        MarkOpened();
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
        RefreshInBackground(() => SyncGrid(value), "the week picker");
        RefreshInBackground(ReviewNotices, "the availability notices");
    }

    partial void OnTimeChanged(TimeOnly value)
    {
        Sync(() => TimeText = ScheduleHelper.Format(value));
        Grid?.ShowGhost(Date, value, DurationMin);
        RefreshInBackground(ReviewNotices, "the availability notices");
    }

    partial void OnDurationMinChanged(int value)
    {
        Sync(() => DurationText = value.ToString());
        Grid?.ShowGhost(Date, Time, Math.Max(1, value));
        RefreshInBackground(ReviewNotices, "the availability notices");
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

    partial void OnWorkerChanged(Worker? value) => RefreshInBackground(ReviewNotices, "the availability notices");

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
        // A click that lands while the dialog is still loading waits for it; one after a
        // failed load is refused, since the form does not hold what the appointment does.
        await Initialization;
        if (!_initialized)
        {
            ErrorValidation = Texts.AppointmentNotLoaded;
            return;
        }

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

        if (!await ConfirmOverlap(time, durationMin)) return;

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

    /// <summary>
    /// CU-01b: an overlap never blocks saving ("es pot guardar igualment"), and it still
    /// does not. But it used to be only the red line above the buttons, which a busy
    /// counter clicks straight past, and that line was refreshed fire-and-forget as the
    /// boxes changed, so it could lag the values actually being saved. Now Save checks
    /// again with exactly those values and, if the slot has no room, asks once.
    /// Not asked when nothing that decides an overlap changed in an edit (the user
    /// already accepted it), nor for a cancelled or no-show appointment, which takes
    /// no room.
    /// </summary>
    /// <returns>True to go on saving.</returns>
    private async Task<bool> ConfirmOverlap(TimeOnly time, int durationMin)
    {
        if (_originalStatus is AppointmentStatus.Cancelled or AppointmentStatus.NoShow) return true;

        bool slotUnchanged = _originalSlot == (Date, time, durationMin) && _originalWorkerId == Worker?.Id;
        if (_id is not null && slotUnchanged) return true;

        var check = await _availability.Check(Date, time, durationMin, Worker?.Id, _id);
        OverlapNotice = check.HasOverlap;
        if (!check.HasOverlap) return true;

        // With a worker chosen the clash is that worker's own diary; without one it is
        // the whole shop's capacity at that hour.
        return await _dialogs.Confirm(
            Texts.ConfirmOverlapTitle,
            Worker is { } worker
                ? string.Format(Texts.ConfirmOverlapWorkerMessage, worker.Name)
                : Texts.ConfirmOverlapMessage,
            Texts.SaveAnyway,
            Texts.ChangeTheTime);
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

    /// <summary>Esc and the X button reach the same question through the window.</summary>
    [RelayCommand]
    private async Task Cancel()
    {
        if (await CanDiscard()) RequestClose(false);
    }

    protected override IDialogService DiscardDialogs => _dialogs;

    /// <summary>Everything Save would write, as the boxes hold it.</summary>
    protected override string EditableState()
        => string.Join("|",
            Date, Time, TimeText, DurationText, SelectedClient?.Id, TextClient, GuestPhone,
            Service?.Id, Worker?.Id, Notes);
}
