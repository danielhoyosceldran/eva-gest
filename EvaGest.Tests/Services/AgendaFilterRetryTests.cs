using AwesomeAssertions;
using EvaGest.Services;
using EvaGest.Tests.Infra;
using EvaGest.ViewModels.Pages;
using Xunit;

namespace EvaGest.Tests.Services;

/// <summary>
/// E-11. The Agenda builds its worker filter once, on the first visit. It added the two
/// fixed entries before reading the workers, so when that read failed the list was left
/// with two entries and was never rebuilt: the workers stayed out of the filter for the
/// whole session.
/// </summary>
public class AgendaFilterRetryTests
{
    [Fact]
    public async Task A_failed_first_load_of_the_workers_is_retried_on_the_next_visit()
    {
        await using var testDb = new TestDatabase();
        var factory = new TestFactory(testDb.Options);
        var config = new SettingsService(factory);
        await new SeedService(factory, config).Seed();
        var realWorkers = new WorkerService(factory);
        await realWorkers.Create(Make.Worker("Marta"), new Dictionary<Models.Weekday, List<(TimeOnly, TimeOnly)>>());
        var workers = FailingService.Wrap<IWorkerService>(realWorkers, out var failing);

        var agenda = new AgendaViewModel(
            new AppointmentService(factory), new SaleService(factory, config),
            new AvailabilityService(factory), new ClientService(factory),
            new CatalogService(factory), workers, config,
            new TestSoundService(), new TestDialogService());

        failing.Failing = true;
        await agenda.Invoking(a => a.Load()).Should().ThrowAsync<Exception>();
        failing.Failing = false;

        await agenda.Load();   // the next time the page is opened

        agenda.WorkerFilterOptions.Select(o => o.Name).Should().Contain("Marta");
        agenda.WorkerFilterOptions.Should().HaveCount(3, "all, unassigned and the one worker, once each");
        agenda.SelectedWorkerFilter.Should().BeSameAs(agenda.WorkerFilterOptions[0]);
    }
}
