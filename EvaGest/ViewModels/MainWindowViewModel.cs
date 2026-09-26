using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EvaGest.Helpers;
using EvaGest.Resources;
using EvaGest.Services;
using EvaGest.ViewModels.Dialogs;
using EvaGest.ViewModels.Pages;
using Serilog;

namespace EvaGest.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    private readonly IDialogService _dialogs;
    private readonly IAppointmentService _appointments;

    [ObservableProperty]
    private PageViewModelBase _currentPage;

    /// <summary>
    /// Set whenever a still-Pending appointment is more than an hour past its start
    /// time, so a cite never silently falls through without the user noticing. Shown
    /// in the shell rather than a page ViewModel because it must stay visible no
    /// matter which page is open, and it is not auto-cleared like <see cref="PageViewModelBase.Notice"/>:
    /// it goes away once <see cref="CheckOverdueAppointments"/> next finds nothing
    /// overdue (the appointment got charged, cancelled, marked no-show or moved), or
    /// for a while when the user closes it (<see cref="DismissOverdueNoticeCommand"/>).
    /// </summary>
    [ObservableProperty]
    private string? _overdueAppointmentsNotice;

    private readonly OverdueNoticeSnooze _snooze = new();
    private List<int> _overdueIds = [];

    // Kept alive for the session so navigating away and back does not lose state.
    private readonly HomeViewModel _start;
    private readonly AgendaViewModel _agenda;
    private readonly ClientsViewModel _clients;
    private readonly WorkersViewModel _workers;
    private readonly CatalogViewModel _catalog;
    private readonly SalesViewModel _sales;
    private readonly TillViewModel _till;
    private readonly ReportsViewModel _reports;
    private readonly SettingsViewModel _settings;

    public MainWindowViewModel(HomeViewModel start, CatalogViewModel catalog, ClientsViewModel clients,
        WorkersViewModel workers, AgendaViewModel agenda, SalesViewModel sales,
        TillViewModel till, ReportsViewModel reports, SettingsViewModel settings,
        IDialogService dialogs, IAppointmentService appointments, IAppointmentChangeNotifier appointmentChanges)
    {
        _start = start;
        _catalog = catalog;
        _clients = clients;
        _workers = workers;
        _agenda = agenda;
        _sales = sales;
        _till = till;
        _reports = reports;
        _settings = settings;
        _dialogs = dialogs;
        _appointments = appointments;
        _currentPage = _start;

        // Every page's IAppointmentService is its own transient instance; this singleton
        // is what lets an edit or delete made on the Agenda or Home page reach the
        // overdue check here right away, instead of waiting for the next timer tick.
        appointmentChanges.Changed += async () => await CheckOverdueAppointments();
    }

    /// <summary>
    /// Polled from the shell's code-behind on a timer (UI-thread concern, not a
    /// ViewModel one). Caught rather than left to the global handler: a transient DB
    /// hiccup on a background poll must not surface an error dialog over whatever the
    /// user is doing, but it still has to leave a record.
    /// </summary>
    public async Task CheckOverdueAppointments()
    {
        try
        {
            var now = DateTime.Now;
            var overdue = await _appointments.GetOverduePending(now);
            _overdueIds = overdue.Select(a => a.Id).ToList();

            OverdueAppointmentsNotice = _snooze.ShouldShow(_overdueIds, now)
                ? Texts.OverdueAppointmentsNotice + string.Join(", ", overdue.Select(a => a.DisplayName))
                : null;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Overdue appointment check failed");
        }
    }

    /// <summary>
    /// The notice's close button. Hides it for <see cref="OverdueNoticeSnooze.Duration"/>;
    /// the next check after that brings it back if the same appointments are still Pending.
    /// </summary>
    [RelayCommand]
    private void DismissOverdueNotice()
    {
        _snooze.Dismiss(_overdueIds, DateTime.Now);
        OverdueAppointmentsNotice = null;
    }

    [RelayCommand] private void NavigateHome() => CurrentPage = _start;
    [RelayCommand] private void NavigateAgenda() => CurrentPage = _agenda;
    [RelayCommand] private void NavigateClients() => CurrentPage = _clients;
    [RelayCommand] private void NavigateWorkers() => CurrentPage = _workers;
    [RelayCommand] private void NavigateCatalog() => CurrentPage = _catalog;
    [RelayCommand] private void NavigateSales() => CurrentPage = _sales;
    [RelayCommand] private void NavigateTill() => CurrentPage = _till;
    [RelayCommand] private void NavigateReports() => CurrentPage = _reports;
    [RelayCommand] private void NavigateSettings() => CurrentPage = _settings;

    [RelayCommand]
    private async Task OpenHelp()
    {
        var vm = new HelpViewModel();
        await _dialogs.ShowDialog(vm);
    }
}
