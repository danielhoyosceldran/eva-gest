using AwesomeAssertions;
using EvaGest.Models;
using EvaGest.Resources;
using EvaGest.Services;
using EvaGest.Tests.Infra;
using EvaGest.ViewModels.Dialogs;
using EvaGest.ViewModels.Pages;
using Xunit;

namespace EvaGest.Tests.Services;

/// <summary>
/// E-09. The catalogue, clients, till and workers pages wrote only after their dialog
/// had closed, so a write that failed showed the generic error with everything typed in
/// the dialog already gone. The dialog now runs the page's write itself (Persist) and
/// stays open, with its form intact, when it fails.
/// </summary>
public class DialogSaveFailureTests
{
    [Fact]
    public async Task A_worker_whose_save_fails_stays_in_the_open_dialog_with_what_was_typed()
    {
        await using var testDb = new TestDatabase();
        var service = FailingService.Wrap<IWorkerService>(
            new WorkerService(new TestFactory(testDb.Options)), out var failing);
        WorkerDialogViewModel? dialog = null;
        bool closed = false;
        var dialogs = new TestDialogService
        {
            ResultDialog = false,   // the user gives up after reading the error
            FillDialog = async d =>
            {
                dialog = (WorkerDialogViewModel)d;
                dialog.Close += _ => closed = true;
                dialog.Name = "Berta";
                dialog.ScheduleDays[0].WorksMorning = true;
                dialog.ScheduleDays[0].MorningStart = "09:00";
                dialog.ScheduleDays[0].MorningEnd = "14:00";

                failing.Failing = true;   // the database refuses the write
                await dialog.SaveCommand.ExecuteAsync(null);
                failing.Failing = false;
            }
        };
        var page = new WorkersViewModel(service, dialogs);
        await page.Load();

        await page.Invoking(p => p.NewWorkerCommand.ExecuteAsync(null)).Should().NotThrowAsync();

        closed.Should().BeFalse("closing would throw away the form");
        dialog!.ErrorValidation.Should().Be(Texts.SaveFailedKeepEditing);
        dialog.Name.Should().Be("Berta");
        page.Rows.Should().BeEmpty();
    }

    [Fact]
    public async Task A_retry_after_a_failed_save_goes_through_and_closes()
    {
        await using var testDb = new TestDatabase();
        var service = FailingService.Wrap<IWorkerService>(
            new WorkerService(new TestFactory(testDb.Options)), out var failing);
        bool closed = false;
        var dialogs = new TestDialogService
        {
            ResultDialog = true,
            FillDialog = async d =>
            {
                var dialog = (WorkerDialogViewModel)d;
                dialog.Close += _ => closed = true;
                dialog.Name = "Berta";
                dialog.ScheduleDays[0].WorksMorning = true;
                dialog.ScheduleDays[0].MorningStart = "09:00";
                dialog.ScheduleDays[0].MorningEnd = "14:00";

                failing.Failing = true;
                await dialog.SaveCommand.ExecuteAsync(null);
                failing.Failing = false;
                await dialog.SaveCommand.ExecuteAsync(null);   // Guardar again
            }
        };
        var page = new WorkersViewModel(service, dialogs);
        await page.Load();

        await page.NewWorkerCommand.ExecuteAsync(null);

        closed.Should().BeTrue();
        page.Rows.Should().ContainSingle(r => r.Worker.Name == "Berta");
    }

    [Fact]
    public async Task A_cash_movement_whose_method_vanished_keeps_the_dialog_open()
    {
        // A real refusal from the database: the method picked in the dialog no longer
        // exists by the time the movement is written (foreign key).
        await using var testDb = new TestDatabase();
        var factory = new TestFactory(testDb.Options);
        var settings = new SettingsService(factory);
        await new SeedService(factory, settings).Seed();
        var catalog = new CatalogService(factory);
        var till = new TillService(factory);
        MovementDialogViewModel? dialog = null;
        var dialogs = new TestDialogService
        {
            FillDialog = async d =>
            {
                dialog = (MovementDialogViewModel)d;
                dialog.PriceText = "12,50";
                dialog.Concept = "Pa";
                dialog.Method = new PaymentMethod { Id = 9999, Name = "Esborrat" };
                await dialog.SaveCommand.ExecuteAsync(null);
            }
        };
        var page = new TillViewModel(till, catalog, new WorkerService(factory), settings, dialogs);
        await page.Load();

        await page.Invoking(p => p.NewCashOutCommand.ExecuteAsync(null)).Should().NotThrowAsync();

        dialog!.ErrorValidation.Should().Be(Texts.SaveFailedKeepEditing);
        dialog.PriceText.Should().Be("12,50");
    }
}
