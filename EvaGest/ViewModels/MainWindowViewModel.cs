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
    private readonly IOwnerAccessService _owner;

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

    /// <summary>
    /// The owner's pages: only reachable while owner mode is open. Everything else
    /// (Home, Agenda, Clients) is public to every worker.
    /// </summary>
    private readonly HashSet<PageViewModelBase> _privatePages;

    /// <summary>True while owner mode is open: the private pages show in the sidebar and
    /// the Home page shows the day's money.</summary>
    public bool IsOwnerUnlocked => _owner.IsUnlocked;

    public MainWindowViewModel(HomeViewModel start, CatalogViewModel catalog, ClientsViewModel clients,
        WorkersViewModel workers, AgendaViewModel agenda, SalesViewModel sales,
        TillViewModel till, ReportsViewModel reports, SettingsViewModel settings,
        IDialogService dialogs, IAppointmentService appointments, IAppointmentChangeNotifier appointmentChanges,
        IOwnerAccessService owner)
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
        _owner = owner;
        _currentPage = _start;
        _privatePages = [_workers, _catalog, _sales, _till, _reports, _settings];

        _owner.Changed += OnOwnerModeChanged;

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

    [RelayCommand] private void NavigateHome() => Navigate(_start);
    [RelayCommand] private void NavigateAgenda() => Navigate(_agenda);
    [RelayCommand] private void NavigateClients() => Navigate(_clients);
    [RelayCommand] private void NavigateWorkers() => Navigate(_workers);
    [RelayCommand] private void NavigateCatalog() => Navigate(_catalog);
    [RelayCommand] private void NavigateSales() => Navigate(_sales);
    [RelayCommand] private void NavigateTill() => Navigate(_till);
    [RelayCommand] private void NavigateReports() => Navigate(_reports);
    [RelayCommand] private void NavigateSettings() => Navigate(_settings);

    /// <summary>
    /// The sidebar hides the private pages while owner mode is closed; this check is
    /// the second lock, so a command fired some other way (a stale key binding, a
    /// future shortcut) still cannot open one.
    /// </summary>
    private void Navigate(PageViewModelBase page)
    {
        if (_privatePages.Contains(page) && !_owner.IsUnlocked) return;
        CurrentPage = page;
    }

    /// <summary>
    /// Closing owner mode while a private page is open sends the user back to Home,
    /// so the figures do not stay on screen after the lock. Home is told too, because
    /// it hides its money cards when owner mode is closed.
    /// </summary>
    private void OnOwnerModeChanged()
    {
        OnPropertyChanged(nameof(IsOwnerUnlocked));
        _start.OwnerModeChanged();

        if (!_owner.IsUnlocked && _privatePages.Contains(CurrentPage))
            CurrentPage = _start;
    }

    /// <summary>
    /// Called by the shell once it is on screen. With no PIN yet (new install, or the
    /// first start after owner mode arrived) the owner is asked to create one straight
    /// away. Cancelling is allowed: the private pages then stay closed, and the sidebar
    /// button asks again.
    /// </summary>
    public async Task EnsureOwnerPin()
    {
        if (!await _owner.HasPin()) await CreateOwnerPin();
    }

    /// <summary>The sidebar's "Mode propietària" button.</summary>
    [RelayCommand]
    private async Task UnlockOwner()
    {
        if (!await _owner.HasPin())
        {
            await CreateOwnerPin();
            return;
        }

        var vm = new OwnerUnlockDialogViewModel(_owner);
        if (await _dialogs.ShowDialog(vm) && vm.Recovered)
            await CreateOwnerPin();
    }

    [RelayCommand]
    private void LockOwner() => _owner.Lock();

    /// <summary>New PIN, then its recovery code, shown the only time it can be.</summary>
    private async Task CreateOwnerPin()
    {
        var vm = new OwnerPinDialogViewModel(_owner, OwnerPinMode.Create);
        if (await _dialogs.ShowDialog(vm) && vm.NewRecoveryCode is { } code)
            await _dialogs.ShowDialog(new RecoveryCodeDialogViewModel(code));
    }

    /// <summary>Any key or click anywhere in the app (hooked in the shell's code-behind)
    /// restarts owner mode's idle countdown.</summary>
    public void RegisterActivity() => _owner.RegisterActivity();

    /// <summary>Polled from the shell's timer: closes owner mode after 5 idle minutes.</summary>
    public void LockOwnerIfIdle() => _owner.LockIfIdle();

    [RelayCommand]
    private async Task OpenHelp()
    {
        var vm = new HelpViewModel();
        await _dialogs.ShowDialog(vm);
    }
}
