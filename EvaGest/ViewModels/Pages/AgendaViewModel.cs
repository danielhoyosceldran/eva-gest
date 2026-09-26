using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EvaGest.Helpers;
using EvaGest.Models;
using EvaGest.Services;
using EvaGest.ViewModels.Dialogs;
using EvaGest.ViewModels.Elements;
using EvaGest.Resources;

namespace EvaGest.ViewModels.Pages;

/// <summary>
/// Weekly agenda (pantalles 2.2). The week itself is drawn by the shared time grid,
/// which the appointment dialog also embeds; this page owns the toolbar, turns the grid's
/// callbacks into "new appointment here" and "edit that appointment", and hosts the
/// day detail that the compact grid cards deliberately leave out.
/// </summary>
public partial class AgendaViewModel : PageViewModelBase
{

    private readonly IAppointmentService _appointments;
    private readonly ISaleService _sales;
    private readonly IAvailabilityService _availability;
    private readonly IClientService _clients;
    private readonly ICatalogService _catalog;
    private readonly IWorkerService _workers;
    private readonly ISettingsService _settings;
    private readonly ISoundService _so;
    private readonly IDialogService _dialogs;

    public AgendaViewModel(
        IAppointmentService appointments, ISaleService sales, IAvailabilityService availability,
        IClientService clients, ICatalogService catalog, IWorkerService workers,
        ISettingsService settings, ISoundService so, IDialogService dialogs)
    {
        _appointments = appointments;
        _sales = sales;
        _availability = availability;
        _clients = clients;
        _catalog = catalog;
        _workers = workers;
        _settings = settings;
        _so = so;
        _dialogs = dialogs;

        Grid = new WeekGridViewModel(appointments, availability, settings, ModeGrid.Agenda,
            onSlotClick: (date, time) => _ = NewAppointmentAt(date, time),
            onAppointmentClick: appointment => _ = EditAppointment(appointment),
            onDaySelected: date => _ = SelectDay(date));
    }

    public override string Title => Texts.NavAgenda;

    public WeekGridViewModel Grid { get; }

    /// <summary>
    /// One row of the day detail. The grid cards hide their status buttons until hover
    /// so a busy week stays readable; here they are always visible, which is the reason
    /// the panel exists at all.
    /// </summary>
    public record DayRow(
        Appointment Appointment, string TimeText, string DurationText, string ClientText, bool IsGuest,
        string ServiceText, string WorkerText, string ColorHex, string StatusText, bool IsPending);

    [ObservableProperty] private DateOnly? _selectedDay;
    [ObservableProperty] private string _selectedDayText = string.Empty;

    public ObservableCollection<DayRow> DayAppointments { get; } = [];

    public bool HasSelectedDay => SelectedDay is not null;
    public bool DayWithoutAppointments => SelectedDay is not null && DayAppointments.Count == 0;

    /// <summary>
    /// One combo entry for the toolbar filter: "All" and "Not defined" alongside every
    /// real worker, since neither of those two has a <see cref="Models.Worker"/> to bind
    /// to. <see cref="Worker"/> is null for both — <see cref="IsUnassigned"/> tells them
    /// apart — so <see cref="Matches"/> is the one place that has to know the difference.
    /// </summary>
    public sealed record WorkerFilterItem(string Name, Worker? Worker, bool IsUnassigned)
    {
        public bool Matches(Appointment appointment)
            => IsUnassigned ? appointment.Worker is null
             : Worker is null || appointment.Worker?.Id == Worker.Id;
    }

    [ObservableProperty] private WorkerFilterItem? _selectedWorkerFilter;
    public ObservableCollection<WorkerFilterItem> WorkerFilterOptions { get; } = [];

    /// <summary>Which week is shown and its label both live on the grid, so this toolbar
    /// and the picker embedded in the appointment dialog drive exactly the same navigation.</summary>
    private DateOnly WeekStart => Grid.WeekStart;

    public async Task Load()
    {
        if (WorkerFilterOptions.Count == 0)
        {
            WorkerFilterOptions.Add(new WorkerFilterItem(Texts.AllWorkers, null, IsUnassigned: false));
            WorkerFilterOptions.Add(new WorkerFilterItem(Texts.Unassigned, null, IsUnassigned: true));
            foreach (var worker in await _workers.GetAll())
                WorkerFilterOptions.Add(new WorkerFilterItem(worker.Name, worker, IsUnassigned: false));

            SelectedWorkerFilter = WorkerFilterOptions[0];
        }

        await LoadWeek(WeekHelper.MondayOfWeek(DateOnly.FromDateTime(DateTime.Today)));
    }

    partial void OnSelectedWorkerFilterChanged(WorkerFilterItem? value) => _ = LoadWeek(WeekStart);

    /// <summary>Toolbar button: books on the day being examined if there is one, else on
    /// today when today is in view, else on the Monday shown — and always at that day's
    /// first opening slot rather than a fixed hour.</summary>
    [RelayCommand]
    private async Task NewAppointment()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var date = SelectedDay
                   ?? (Grid.Days.Any(d => d.Date == today) ? today : WeekStart);
        await NewAppointmentAt(date, GridHelper.ATime(Grid.FirstSlotOf(date)));
    }

    private async Task NewAppointmentAt(DateOnly date, TimeOnly time)
    {
        var vm = new AppointmentDialogViewModel(_appointments, _availability, _clients, _catalog,
            _workers, _settings, _dialogs, date, time);
        if (await _dialogs.ShowDialog(vm)) await LoadWeek(WeekStart);
    }

    private async Task EditAppointment(Appointment appointment)
    {
        var vm = new AppointmentDialogViewModel(_appointments, _availability, _clients, _catalog,
            _workers, _settings, _dialogs, appointment);
        if (await _dialogs.ShowDialog(vm)) await LoadWeek(WeekStart);
    }

    // ── Day detail ───────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task SelectDay(DateOnly date)
    {
        SelectedDay = date;
        SelectedDayText = Capitalize(date.ToString(Texts.DayMonthFormat, AppLanguage.Culture));
        await LoadDayDetail();
    }

    [RelayCommand]
    private void CloseDetail()
    {
        SelectedDay = null;
        DayAppointments.Clear();
        OnPropertyChanged(nameof(HasSelectedDay));
        OnPropertyChanged(nameof(DayWithoutAppointments));
    }

    /// <summary>
    /// Same as the Start page: marking an appointment as done opens the sale dialog
    /// prefilled from it (CU-02), and it is saving that sale which moves the
    /// appointment to Completed. Closing the dialog without charging leaves it
    /// Pending, so the charge can be started again.
    /// </summary>
    [RelayCommand]
    private async Task MarkCompleted(Appointment appointment)
    {
        var vm = await SaleDialogViewModel.FromAppointment(
            _sales, _clients, _catalog, _workers, _so, _settings, _dialogs, appointment);
        await _dialogs.ShowDialog(vm);

        await LoadWeek(WeekStart);
    }

    [RelayCommand] private Task MarkCancelled(Appointment appointment) => ChangeStatus(appointment, AppointmentStatus.Cancelled);
    [RelayCommand] private Task MarkNoShow(Appointment appointment) => ChangeStatus(appointment, AppointmentStatus.NoShow);

    private async Task ChangeStatus(Appointment appointment, AppointmentStatus newValue)
    {
        await _appointments.ChangeStatus(appointment.Id, newValue);
        await LoadWeek(WeekStart);
    }

    /// <summary>
    /// Straight delete from the day detail, for the wrong entries that should never have
    /// been booked. An appointment that already carries a sale is refused, because the
    /// sale names it (F-05); cancelling is the way to keep it on the record instead.
    /// </summary>
    [RelayCommand]
    private async Task DeleteAppointment(Appointment appointment)
    {
        bool confirmed = await _dialogs.Confirm(
            Texts.DeleteAppointmentTitle,
            string.Format(Texts.DeleteAppointmentOfMessage, appointment.DisplayName,
                          appointment.Date.ToString("dd/MM/yyyy"),
                          appointment.Time.ToString("HH\\:mm")),
            Texts.Delete);

        if (!confirmed) return;

        var result = await _appointments.Delete(appointment.Id);
        await LoadWeek(WeekStart);

        ShowNotice(result == DeleteResult.Blocked
            ? Texts.AppointmentNotDeletedHasSale
            : Texts.AppointmentDeleted);
    }

    /// <summary>
    /// Queried first, rewritten afterwards. Clearing and then awaiting let two quick
    /// clicks on different day headers both append, listing the day twice, because the
    /// grid raises its callback fire-and-forget.
    /// </summary>
    private async Task LoadDayDetail()
    {
        var rows = SelectedDay is DateOnly day
            ? (await _appointments.GetByDay(day))
                .Where(a => SelectedWorkerFilter is null || SelectedWorkerFilter.Matches(a))
                .Select(ToRow).ToList()
            : [];

        DayAppointments.Clear();
        foreach (var row in rows) DayAppointments.Add(row);

        OnPropertyChanged(nameof(HasSelectedDay));
        OnPropertyChanged(nameof(DayWithoutAppointments));
    }

    private static DayRow ToRow(Appointment appointment) => new(
        appointment,
        appointment.Time.ToString("HH\\:mm"),
        string.Format(Texts.MinutesShort, appointment.DurationMin),
        appointment.DisplayName,
        appointment.ClientId is null,
        appointment.Service?.Name ?? Texts.NoService,
        appointment.Worker?.Name ?? Texts.Unassigned,
        appointment.Worker?.Color ?? "#00000000",
        Labels.Text(appointment.Status),
        appointment.Status == AppointmentStatus.Pending);

    private static string Capitalize(string text)
        => text.Length == 0 ? text : char.ToUpper(text[0], AppLanguage.Culture) + text[1..];

    private async Task LoadWeek(DateOnly monday)
    {
        Loading = true;
        try
        {
            await Grid.LoadWeek(monday, SelectedWorkerFilter is { } filter ? filter.Matches : null);

            // The detail panel would otherwise keep showing the appointments of a day
            // that is no longer on screen, or stale rows after an edit.
            if (SelectedDay is DateOnly day && Grid.Days.Any(d => d.Date == day))
                await LoadDayDetail();
            else
                CloseDetail();
        }
        finally { Loading = false; }
    }
}
