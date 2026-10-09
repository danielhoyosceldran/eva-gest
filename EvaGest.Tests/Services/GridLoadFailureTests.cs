using AwesomeAssertions;
using EvaGest.Models;
using EvaGest.Services;
using EvaGest.Tests.Infra;
using EvaGest.ViewModels.Elements;
using Xunit;

namespace EvaGest.Tests.Services;

/// <summary>
/// E-12. The week grid moved its range and title before reading the appointments, so a
/// failed read left the header naming the new days above the columns of the old ones.
/// </summary>
public class GridLoadFailureTests
{
    private static readonly DateOnly Monday = new(2026, 10, 5);

    private static async Task<(WeekGridViewModel Grid, FailingService.Switch Failing)> Build(TestDatabase testDb)
    {
        var factory = new TestFactory(testDb.Options);
        await new SettingsService(factory).Save(ConfigKeys.AgendaDays, WeekGridViewModel.ThreeDays.ToString());
        var appointments = FailingService.Wrap<IAppointmentService>(new AppointmentService(factory), out var failing);
        var grid = new WeekGridViewModel(appointments, new AvailabilityService(factory),
            new SettingsService(factory), ModeGrid.Agenda, (_, _) => { });
        return (grid, failing);
    }

    [Fact]
    public async Task A_failed_move_leaves_the_header_on_the_days_still_shown()
    {
        await using var testDb = new TestDatabase();
        var (grid, failing) = await Build(testDb);
        await grid.LoadRange(Monday);
        string shownText = grid.RangeText;

        failing.Failing = true;
        await grid.Invoking(g => g.LoadRange(Monday.AddDays(3))).Should().ThrowAsync<Exception>();

        grid.RangeStart.Should().Be(Monday);
        grid.RangeText.Should().Be(shownText);
        grid.Days[0].Date.Should().Be(Monday, "header and columns must agree");
    }
}
