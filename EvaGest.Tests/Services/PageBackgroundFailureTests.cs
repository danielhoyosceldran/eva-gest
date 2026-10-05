using System.ComponentModel;
using AwesomeAssertions;
using EvaGest.Models;
using EvaGest.Services;
using EvaGest.Tests.Infra;
using EvaGest.ViewModels.Pages;
using Xunit;

namespace EvaGest.Tests.Services;

/// <summary>
/// F-09: grid clicks and the reloads started by a filter change were fire-and-forget
/// (<c>_ = SomeTask()</c>). When that work failed, nobody observed it: the user saw
/// nothing. Such a failure must now be surfaced on the page as its notice.
///
/// Each page loads normally first; then its service starts failing (as a real service
/// does, with a faulted task) and the user changes a filter or clicks the grid.
/// </summary>
public class PageBackgroundFailureTests
{
    /// <summary>Records every notice the page puts up, since a notice clears itself after
    /// a few seconds and a poll could miss it.</summary>
    private sealed class NoticeWatcher
    {
        private readonly PageViewModelBase _page;
        private readonly List<string> _seen = [];

        public NoticeWatcher(PageViewModelBase page)
        {
            _page = page;
            page.PropertyChanged += Seen;
        }

        private void Seen(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(PageViewModelBase.Notice) && _page.Notice is { } notice)
                lock (_seen) _seen.Add(notice);
        }

        /// <summary>The first notice put up within <paramref name="seconds"/>, or null.</summary>
        public async Task<string?> First(double seconds = 3)
        {
            var until = DateTime.UtcNow.AddSeconds(seconds);
            while (DateTime.UtcNow < until)
            {
                lock (_seen) if (_seen.Count > 0) return _seen[0];
                if (_page.Notice is { } now) return now;
                await Task.Delay(10);
            }
            lock (_seen) return _seen.FirstOrDefault() ?? _page.Notice;
        }
    }

    [Fact]
    public async Task A_client_search_that_fails_is_shown_on_the_clients_page()
    {
        await using var testDb = new TestDatabase();
        var factory = new TestFactory(testDb.Options);
        var clients = FailingService.Wrap<IClientService>(new ClientService(factory), out var failing);
        var page = new ClientsViewModel(clients, new ReportsService(factory), new TestDialogService(),
            await TestOwner.Unlocked());
        await page.Load();
        var watcher = new NoticeWatcher(page);

        failing.Failing = true;
        page.SearchText = "Joan";

        (await watcher.First()).Should().NotBeNullOrWhiteSpace("the search failed and the user must be told");
    }

    [Fact]
    public async Task Clicking_an_empty_agenda_slot_whose_action_fails_is_shown_on_the_agenda()
    {
        await using var testDb = new TestDatabase();
        var factory = new TestFactory(testDb.Options);
        var settings = new SettingsService(factory);
        var dialogs = new RecordingDialogService();
        var page = new AgendaViewModel(new AppointmentService(factory), new SaleService(factory, settings),
            new AvailabilityService(factory), new ClientService(factory), new CatalogService(factory),
            new WorkerService(factory), settings, new TestSoundService(), dialogs);
        await page.Load();
        var watcher = new NoticeWatcher(page);

        // Whatever the click leads to (the new-appointment dialog, or a question about a
        // closed slot), showing it fails.
        dialogs.FailWith = new InvalidOperationException("the dialog could not be shown");
        var nextWeek = DateOnly.FromDateTime(DateTime.Today).AddDays(7);
        page.Grid.ActivateSlot(nextWeek, new TimeOnly(10, 0));

        (await watcher.First()).Should().NotBeNullOrWhiteSpace("the click failed and the user must be told");
    }

    [Fact]
    public async Task A_reload_after_changing_the_agenda_worker_filter_that_fails_is_shown()
    {
        await using var testDb = new TestDatabase();
        var factory = new TestFactory(testDb.Options);
        var settings = new SettingsService(factory);
        var workers = new WorkerService(factory);
        await workers.Create(Make.Worker("Marta"), new Dictionary<Weekday, List<(TimeOnly start, TimeOnly fi)>>());
        var appointments = FailingService.Wrap<IAppointmentService>(new AppointmentService(factory), out var failing);
        var page = new AgendaViewModel(appointments, new SaleService(factory, settings),
            new AvailabilityService(factory), new ClientService(factory), new CatalogService(factory),
            workers, settings, new TestSoundService(), new TestDialogService());
        await page.Load();
        var watcher = new NoticeWatcher(page);

        failing.Failing = true;
        page.SelectedWorkerFilter = page.WorkerFilterOptions.Last(o => !ReferenceEquals(o, page.SelectedWorkerFilter));

        (await watcher.First()).Should().NotBeNullOrWhiteSpace("the reload failed and the user must be told");
    }

    [Fact]
    public async Task A_reload_after_changing_a_sales_filter_that_fails_is_shown()
    {
        await using var testDb = new TestDatabase();
        var factory = new TestFactory(testDb.Options);
        var settings = new SettingsService(factory);
        var sales = FailingService.Wrap<ISaleService>(new SaleService(factory, settings), out var failing);
        var page = new SalesViewModel(sales, new ClientService(factory), new CatalogService(factory),
            new WorkerService(factory), new TestSoundService(), settings, new ExportService(factory),
            new TestDialogService());
        await page.Load();
        var watcher = new NoticeWatcher(page);

        failing.Failing = true;
        page.Status = SaleStatus.Voided;

        (await watcher.First()).Should().NotBeNullOrWhiteSpace("the reload failed and the user must be told");
    }

    [Fact]
    public async Task A_reload_after_changing_the_till_period_that_fails_is_shown()
    {
        await using var testDb = new TestDatabase();
        var factory = new TestFactory(testDb.Options);
        var settings = new SettingsService(factory);
        var till = FailingService.Wrap<ITillService>(new TillService(factory), out var failing);
        var page = new TillViewModel(till, new CatalogService(factory), new WorkerService(factory), settings,
            new TestDialogService());
        await page.Load();
        var watcher = new NoticeWatcher(page);

        failing.Failing = true;
        page.Period = TillPeriod.PreviousMonth;

        (await watcher.First()).Should().NotBeNullOrWhiteSpace("the reload failed and the user must be told");
    }

    [Fact]
    public async Task A_reload_after_changing_the_reports_period_that_fails_is_shown()
    {
        await using var testDb = new TestDatabase();
        var factory = new TestFactory(testDb.Options);
        var reports = FailingService.Wrap<IReportsService>(new ReportsService(factory), out var failing);
        var page = new ReportsViewModel(reports);
        await page.Load();
        var watcher = new NoticeWatcher(page);

        failing.Failing = true;
        page.From = page.From.AddMonths(-1);

        (await watcher.First()).Should().NotBeNullOrWhiteSpace("the reload failed and the user must be told");
    }
}
