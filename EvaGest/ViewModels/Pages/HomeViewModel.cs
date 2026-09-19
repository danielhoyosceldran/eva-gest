using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EvaGest.Models;
using EvaGest.Services;
using EvaGest.ViewModels.Dialogs;
using EvaGest.Resources;

namespace EvaGest.ViewModels.Pages;

/// <summary>
/// Start (pantalles 2.1): the day's state at a glance, and the five quick actions
/// that let the user skip navigating anywhere else for routine work.
/// </summary>
public partial class HomeViewModel(
    IAppointmentService appointments, ISaleService sales, ITillService till, IClientService clients,
    IAvailabilityService availability, ICatalogService catalog, IWorkerService workers,
    ISettingsService settings, ISoundService so, IDialogService dialogs) : PageViewModelBase
{
    public override string Title => Texts.NavHome;

    [ObservableProperty] private string _dateText = string.Empty;
    [ObservableProperty] private string? _birthdayNoticeText;

    [ObservableProperty] private int _todayAppointments;
    [ObservableProperty] private int _todaySales;
    [ObservableProperty] private string _chargedTodayText = "—";
    [ObservableProperty] private string _cashInText = "—";
    [ObservableProperty] private string _cashOutText = "—";
    [ObservableProperty] private string _balanceText = "—";

    public ObservableCollection<Appointment> DayAppointments { get; } = [];

    public async Task Load()
    {
        Loading = true;
        try
        {
            var today = DateOnly.FromDateTime(DateTime.Today);
            DateText = today.ToString(Texts.LongDateFormat, AppLanguage.Culture);
            DateText = char.ToUpper(DateText[0], AppLanguage.Culture) + DateText[1..];

            var birthdays = await clients.BirthdaysToday();
            BirthdayNoticeText = birthdays.Count > 0
                ? Texts.BirthdaysToday + string.Join(", ", birthdays.Select(c => c.Name))
                : null;

            DayAppointments.Clear();
            foreach (var c in await appointments.GetByDay(today)) DayAppointments.Add(c);
            TodayAppointments = DayAppointments.Count;

            var summary = await till.Summary(today, today);
            TodaySales = (await sales.Search(new SalesFilter(today, today, Status: SaleStatus.Active))).Count;
            ChargedTodayText = Money.Format((int)summary.SalesCents);
            CashInText = Money.Format((int)summary.CashInCents);
            CashOutText = Money.Format((int)summary.CashOutCents);
            BalanceText = Money.Format((int)summary.BalanceCents);
        }
        finally { Loading = false; }
    }

    /// <summary>
    /// Marking an appointment as done is where the agenda meets the till (CU-02): the
    /// sale dialog opens straight away with the appointment's client, service and
    /// worker filled in. The state change is the sale's, not this command's:
    /// SaleService.Create moves the appointment to Completed when the sale that names
    /// it is saved, so closing the dialog without charging leaves the appointment
    /// Pending and it can be charged again later.
    /// </summary>
    [RelayCommand]
    private async Task MarkCompleted(Appointment appointment)
    {
        var vm = await SaleDialogViewModel.FromAppointment(
            sales, clients, catalog, workers, so, settings, dialogs, appointment);
        await dialogs.ShowDialog(vm);

        await Load();
    }

    [RelayCommand]
    private async Task EditAppointment(Appointment appointment)
    {
        var vm = new AppointmentDialogViewModel(appointments, availability, clients, catalog, workers,
            settings, dialogs, appointment);
        if (await dialogs.ShowDialog(vm)) await Load();
    }

    [RelayCommand] private Task MarkCancelled(Appointment appointment) => ChangeAppointmentStatus(appointment, AppointmentStatus.Cancelled);
    [RelayCommand] private Task MarkNoShow(Appointment appointment) => ChangeAppointmentStatus(appointment, AppointmentStatus.NoShow);

    /// <summary>
    /// Undoes a Cancel·lada, No assistida or Completed mark so the appointment goes
    /// back to Pending. Blocked when it still carries an active sale: the sale names
    /// the appointment and already moved money, so it has to be voided first (Sales).
    /// </summary>
    [RelayCommand]
    private async Task MarkPending(Appointment appointment)
    {
        bool changed = await appointments.ChangeStatus(appointment.Id, AppointmentStatus.Pending);
        await Load();

        if (!changed)
            ShowNotice(Texts.AppointmentHasSaleCannotReopen);
    }

    private async Task ChangeAppointmentStatus(Appointment appointment, AppointmentStatus newStatus)
    {
        await appointments.ChangeStatus(appointment.Id, newStatus);
        await Load();
    }

    [RelayCommand]
    private async Task NewAppointment()
    {
        var vm = new AppointmentDialogViewModel(appointments, availability, clients, catalog, workers,
            settings, dialogs, DateOnly.FromDateTime(DateTime.Today));
        if (await dialogs.ShowDialog(vm)) await Load();
    }

    [RelayCommand]
    private async Task NewSale()
    {
        var vm = await SaleDialogViewModel.New(sales, clients, catalog, workers, so, settings, dialogs);
        if (await dialogs.ShowDialog(vm)) await Load();
    }

    [RelayCommand]
    private async Task NewClient()
    {
        var vm = new ClientDialogViewModel(clients);
        if (await dialogs.ShowDialog(vm))
        {
            var model = vm.AModel();
            if (model.Id == 0) await clients.Create(model);
            else await clients.Update(model);
        }
    }

    [RelayCommand]
    private async Task NewCashIn() => await OpenMovementDialog(MovementType.In);

    [RelayCommand]
    private async Task NewCashOut() => await OpenMovementDialog(MovementType.Out);

    private async Task OpenMovementDialog(MovementType type)
    {
        var vm = new MovementDialogViewModel(type);
        await vm.LoadMethods(catalog);
        await vm.LoadCategories(catalog);
        await vm.LoadWorkers(workers);
        if (await dialogs.ShowDialog(vm))
        {
            await till.Create(vm.AModel());
            await Load();
        }
    }

    [RelayCommand]
    private async Task OpenHelp()
    {
        var vm = new HelpViewModel(FaqSection.Home);
        await dialogs.ShowDialog(vm);
    }
}
