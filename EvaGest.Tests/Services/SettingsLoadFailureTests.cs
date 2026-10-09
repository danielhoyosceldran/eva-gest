using AwesomeAssertions;
using EvaGest.Models;
using EvaGest.Resources;
using EvaGest.Services;
using EvaGest.Tests.Infra;
using EvaGest.ViewModels.Pages;
using Xunit;

namespace EvaGest.Tests.Services;

/// <summary>
/// E-13. The settings page marked itself loaded in a finally, so a load that failed half
/// way left the fields it never read at their defaults, and the checkboxes that save as
/// they change then wrote over the stored values as if those defaults were real.
/// </summary>
public class SettingsLoadFailureTests
{
    private static (SettingsViewModel Page, TestSettings Stored, FailingService.Switch Failing) Build(TestDatabase testDb)
    {
        var stored = new TestSettings((ConfigKeys.ConfirmationSound, "0"));
        var settings = FailingService.Wrap<ISettingsService>(stored, out var failing);
        var factory = new TestFactory(testDb.Options);
        var page = new SettingsViewModel(
            new BackupService(new AppPaths("live.db", Path.GetTempPath()), stored),
            new ExportService(factory), settings, new AvailabilityService(factory),
            new TestDialogService(), TestOwner.New());
        return (page, stored, failing);
    }

    [Fact]
    public async Task A_failed_load_says_so_on_the_page()
    {
        await using var testDb = new TestDatabase();
        var (page, _, failing) = Build(testDb);

        failing.Failing = true;
        await page.Invoking(p => p.Load()).Should().NotThrowAsync();

        page.Notice.Should().Be(Texts.SettingsNotLoaded);
        page.Loading.Should().BeFalse();
    }

    [Fact]
    public async Task After_a_failed_load_a_checkbox_does_not_write_over_the_stored_value()
    {
        await using var testDb = new TestDatabase();
        var (page, stored, failing) = Build(testDb);

        failing.Failing = true;
        await page.Load();
        failing.Failing = false;

        // The box shows the default (on), not the stored "0"; the user unticks... and
        // ticks it again, which would store "1" over the real "0".
        page.ConfirmationSound = false;
        page.ConfirmationSound = true;
        await page.PendingSave;

        (await stored.Get(ConfigKeys.ConfirmationSound)).Should().Be("0");
    }

    [Fact]
    public async Task A_later_successful_load_clears_the_note_and_saves_again()
    {
        await using var testDb = new TestDatabase();
        var (page, stored, failing) = Build(testDb);
        failing.Failing = true;
        await page.Load();
        failing.Failing = false;

        await page.Load();
        page.ConfirmationSound = true;
        await page.PendingSave;

        page.Notice.Should().BeNull();
        (await stored.Get(ConfigKeys.ConfirmationSound)).Should().Be("1");
    }
}
