using AwesomeAssertions;
using EvaGest.Data;
using EvaGest.Models;
using EvaGest.Resources;
using EvaGest.Services;
using EvaGest.Tests.Infra;
using EvaGest.ViewModels;
using EvaGest.ViewModels.Dialogs;
using EvaGest.ViewModels.Pages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EvaGest.Tests.Scenarios;

/// <summary>
/// Block N — a new install. The app is started the way App.OnStartup starts it, over a
/// folder that holds nothing yet: appsettings.json is read, the folders are made, the
/// services are wired by <see cref="AppComposition"/>, the database is created by its
/// migrations and seeded, and the shell is resolved from the container. Every other test
/// builds its schema with EnsureCreated and its services by hand, so this is the one place
/// the first day of a new shop is walked from end to end.
/// </summary>
public sealed class FreshDatabaseTests : IDisposable
{
    private readonly string _appFolder;
    private readonly string _dataFolder;
    private readonly List<ServiceProvider> _providers = [];

    public FreshDatabaseTests()
    {
        string root = Path.Combine(Path.GetTempPath(), $"EvaGestFresh_{Guid.NewGuid():N}");
        _appFolder = Path.Combine(root, "App");
        _dataFolder = Path.Combine(root, "Data");
        Directory.CreateDirectory(_appFolder);

        // The installer ships an appsettings.json naming where the database lives; here it
        // points at a folder that does not exist yet, as on a machine that never ran EvaGest.
        string dbPath = Path.Combine(_dataFolder, "evagest.db").Replace("\\", "\\\\");
        File.WriteAllText(Path.Combine(_appFolder, "appsettings.json"),
            $$"""{ "DatabasePath": "{{dbPath}}" }""");
    }

    public void Dispose()
    {
        foreach (var provider in _providers) provider.Dispose();
        SqlitePools.Release(Path.Combine(_dataFolder, "evagest.db"));
        try { Directory.Delete(Path.GetDirectoryName(_appFolder)!, recursive: true); }
        catch (IOException) { /* a leftover temp folder is harmless */ }
    }

    /// <summary>
    /// One start of the app, as App.OnStartup does it up to the main window: folders,
    /// container, database. The real dialogs and the chime are swapped for the scripted
    /// user and a silent sound; everything else is what the installed app resolves.
    /// </summary>
    private async Task<(ServiceProvider services, StartupFolders.Layout layout)> Start(ScriptedUser user)
    {
        var layout = StartupFolders.Prepare(_appFolder);

        var collection = AppComposition.Register(new ServiceCollection(), layout.DbPath, layout.BaseFolder);
        collection.AddSingleton<IDialogService>(user);
        collection.AddSingleton<ISoundService, TestSoundService>();
        var services = collection.BuildServiceProvider();
        _providers.Add(services);

        await AppComposition.PrepareDatabase(services);
        return (services, layout);
    }

    private static async Task<T> Open<T>(MainWindowViewModel shell, Action navigate) where T : PageViewModelBase
    {
        navigate();
        shell.CurrentPage.Should().BeOfType<T>("the owner is unlocked, so the page must open");
        return (T)shell.CurrentPage;
    }

    [Fact] // N-01
    public async Task The_first_start_creates_the_folders_and_a_seeded_database()
    {
        File.Exists(Path.Combine(_dataFolder, "evagest.db")).Should().BeFalse("precondition: a new machine");

        var (services, layout) = await Start(new ScriptedUser());

        File.Exists(layout.DbPath).Should().BeTrue();
        Directory.Exists(Path.Combine(_dataFolder, "Backups")).Should().BeTrue();
        Directory.Exists(layout.LogsFolder).Should().BeTrue();

        // No copy before migrating: there was nothing to lose (CU-12)
        (await services.GetRequiredService<IBackupService>().ListAll())
            .Should().NotContain(b => b.IsBeforeUpdate);

        var factory = services.GetRequiredService<IDbContextFactory<ShopDbContext>>();
        await using var db = await factory.CreateDbContextAsync();
        (await db.Database.GetPendingMigrationsAsync()).Should().BeEmpty();

        // Without a payment method the shop could not take a single sale
        (await db.PaymentMethods.Select(m => m.Name).ToListAsync())
            .Should().BeEquivalentTo(["Efectiu", "Targeta", "Bizum"]);

        var settings = services.GetRequiredService<ISettingsService>();
        (await settings.GetInt(ConfigKeys.DefaultVatBp, 0)).Should().Be(2100);
        (await settings.CurrentVatMode()).Should().Be(VatMode.Included);
        (await settings.Get(ConfigKeys.Language)).Should().Be(nameof(Language.Catalan));
        (await settings.GetInt(ConfigKeys.DefaultAppointmentDurationMin, 0)).Should().Be(30);

        // Nothing else is invented: no clients, no catalogue, no workers, no money
        (await db.Clients.CountAsync()).Should().Be(0);
        (await db.Services.CountAsync()).Should().Be(0);
        (await db.Products.CountAsync()).Should().Be(0);
        (await db.Workers.CountAsync()).Should().Be(0);
        (await db.Sales.CountAsync()).Should().Be(0);
        (await db.CashMovements.CountAsync()).Should().Be(0);
    }

    [Fact] // N-02
    public async Task A_second_start_neither_copies_nor_seeds_twice_and_keeps_what_the_user_changed()
    {
        var (first, _) = await Start(new ScriptedUser());

        // The owner renames a method and deletes another on the first day
        var catalog = first.GetRequiredService<ICatalogService>();
        var methods = await catalog.GetMethods();
        var card = methods.Single(m => m.Name == "Targeta");
        card.Name = "Datàfon";
        await catalog.UpdateMethod(card);
        await catalog.DeleteMethod(methods.Single(m => m.Name == "Bizum").Id);
        await first.GetRequiredService<ISettingsService>().Save(ConfigKeys.DefaultVatBp, "1000");
        first.Dispose();
        _providers.Remove(first);

        var (second, _) = await Start(new ScriptedUser());

        (await second.GetRequiredService<IBackupService>().ListAll())
            .Should().NotContain(b => b.IsBeforeUpdate, "nothing was pending, so nothing needed copying");

        (await second.GetRequiredService<ICatalogService>().GetMethods()).Select(m => m.Name)
            .Should().BeEquivalentTo(["Efectiu", "Datàfon"], "a deleted Bizum must not come back on every start");
        (await second.GetRequiredService<ISettingsService>().GetInt(ConfigKeys.DefaultVatBp, 0))
            .Should().Be(1000, "the seed fills in missing keys only, never overwrites a chosen value");
    }

    [Fact] // N-03
    public async Task On_a_new_install_the_owner_is_asked_for_a_pin_and_the_private_pages_stay_shut_until_then()
    {
        var user = new ScriptedUser();
        var (services, _) = await Start(user);
        var shell = services.GetRequiredService<MainWindowViewModel>();

        // The owner closes the PIN dialog without creating one: the app still opens
        user.Then<OwnerPinDialogViewModel>(vm => vm.CancelCommand.Execute(null));
        await shell.EnsureOwnerPin();

        shell.IsOwnerUnlocked.Should().BeFalse();
        shell.NavigateTillCommand.Execute(null);
        shell.CurrentPage.Should().BeOfType<HomeViewModel>("the till is private until a PIN exists");

        // Later, from the sidebar: a PIN typed differently twice, then right
        user.Then<OwnerPinDialogViewModel>(async vm =>
            {
                vm.NewPin = "2468";
                vm.RepeatPin = "2486";
                await vm.SaveCommand.ExecuteAsync(null);
                vm.ErrorValidation.Should().Be(Texts.PinsDoNotMatch);

                vm.RepeatPin = "2468";
                await vm.SaveCommand.ExecuteAsync(null);
            })
            .Then<RecoveryCodeDialogViewModel>(_ => { });
        await shell.UnlockOwnerCommand.ExecuteAsync(null);

        shell.IsOwnerUnlocked.Should().BeTrue();
        user.ScriptFinished.Should().BeTrue("the recovery code must be shown right after the PIN is made");
        (await services.GetRequiredService<IOwnerAccessService>().HasPin()).Should().BeTrue();

        shell.NavigateTillCommand.Execute(null);
        shell.CurrentPage.Should().BeOfType<TillViewModel>();
    }

    [Fact] // N-04
    public async Task Every_page_opens_on_an_empty_database_and_shows_its_empty_state()
    {
        var user = new ScriptedUser();
        var (services, _) = await Start(user);
        var shell = services.GetRequiredService<MainWindowViewModel>();

        await shell.RunAutomaticBackup();
        shell.BackupFailedNotice.Should().BeNull("a backup of an empty database must still work");

        await shell.CheckOverdueAppointments();
        shell.OverdueAppointmentsNotice.Should().BeNull();

        await services.GetRequiredService<IOwnerAccessService>().CreatePin("1357");

        var home = await Open<HomeViewModel>(shell, () => shell.NavigateHomeCommand.Execute(null));
        await home.Load();
        home.DayAppointments.Should().BeEmpty();
        home.TodaySales.Should().Be(0);
        home.BirthdayNoticeText.Should().BeNull();
        home.DateText.Should().NotBeNullOrWhiteSpace();

        var agenda = await Open<AgendaViewModel>(shell, () => shell.NavigateAgendaCommand.Execute(null));
        await agenda.Load();
        agenda.WorkerFilterOptions.Should().HaveCount(2, "only All and Unassigned exist with no workers");
        agenda.Grid.Days.Should().NotBeEmpty("the grid still draws the days with no opening hours set");

        var clients = await Open<ClientsViewModel>(shell, () => shell.NavigateClientsCommand.Execute(null));
        await clients.Load();
        clients.Active.Should().BeEmpty();
        clients.Asleep.Should().BeEmpty();

        var workers = await Open<WorkersViewModel>(shell, () => shell.NavigateWorkersCommand.Execute(null));
        await workers.Load();
        workers.HasNone.Should().BeTrue();

        var catalog = await Open<CatalogViewModel>(shell, () => shell.NavigateCatalogCommand.Execute(null));
        await catalog.Load();
        catalog.Services.Should().BeEmpty();
        catalog.Products.Should().BeEmpty();
        catalog.Methods.Should().HaveCount(3);

        var sales = await Open<SalesViewModel>(shell, () => shell.NavigateSalesCommand.Execute(null));
        await sales.Load();
        sales.HasNoResults.Should().BeTrue();
        sales.TotalTotalText.Should().Be(Money.Format(0));

        var till = await Open<TillViewModel>(shell, () => shell.NavigateTillCommand.Execute(null));
        await till.Load();
        till.BalanceText.Should().Be(Money.Format(0));
        till.Movements.Should().BeEmpty();
        till.ShowRateBreakdown.Should().BeFalse();

        var reports = await Open<ReportsViewModel>(shell, () => shell.NavigateReportsCommand.Execute(null));
        await reports.Load();
        reports.ClientOfTheMonthText.Should().Be(Texts.NoSalesThisMonth);
        reports.Evolution.Should().HaveCount(12, "a year of empty months, not a crash on an empty maximum");
        reports.TopVisits.Should().BeEmpty();
        reports.WorkerRanking.Should().BeEmpty();

        var settings = await Open<SettingsViewModel>(shell, () => shell.NavigateSettingsCommand.Execute(null));
        await settings.Load();
        settings.Notice.Should().BeNull("the settings page must load completely on a new install");
        settings.DefaultVatText.Should().Be("21");
        settings.HasNoClosedDays.Should().BeTrue();

        user.Opened.Should().BeEmpty("no page may pop up a dialog just by opening");
    }

    [Fact] // N-05
    public async Task The_first_day_of_a_new_shop_from_an_empty_catalogue_to_the_till()
    {
        var user = new ScriptedUser();
        var (services, _) = await Start(user);
        var shell = services.GetRequiredService<MainWindowViewModel>();
        await services.GetRequiredService<IOwnerAccessService>().CreatePin("1357");

        // 1. Before any service exists, a sale can still be taken with a custom concept
        var home = await Open<HomeViewModel>(shell, () => shell.NavigateHomeCommand.Execute(null));
        await home.Load();
        user.Then<SaleDialogViewModel>(async vm =>
        {
            vm.ActiveServices.Should().BeEmpty();
            vm.QuickServices.Should().BeEmpty();
            vm.PaymentMethod.Should().BeNull("nothing is preselected, so the method is a choice");

            vm.TextClient = "Primer client";
            vm.CatalogAdder.Text = "Tall de prova";
            vm.CatalogAdder.PickHighlightedCommand.Execute(null);
            vm.Lines.Should().ContainSingle();
            vm.Lines[0].PriceText = "12,00";
            vm.Lines[0].VatText.Should().Be("21", "a custom line starts on the shop's default rate");
            vm.PaymentMethod = vm.ActiveMethods.Single(m => m.Name == "Efectiu");
            await vm.ChargeCommand.ExecuteAsync(null);
        });
        await home.NewSaleCommand.ExecuteAsync(null);
        home.TodaySales.Should().Be(1);

        // 2. The owner fills in the catalogue: a service typed with a mistake first
        var catalog = await Open<CatalogViewModel>(shell, () => shell.NavigateCatalogCommand.Execute(null));
        await catalog.Load();
        user.Then<ServiceDialogViewModel>(async vm =>
        {
            vm.Name = "Tall";
            vm.PriceText = "15.000";     // a thousand-separator typo, not 15 euros
            await vm.SaveCommand.ExecuteAsync(null);
            vm.ErrorValidation.Should().NotBeNull();

            vm.PriceText = "15,00";
            vm.DurationMinText = "30";
            await vm.SaveCommand.ExecuteAsync(null);
        });
        await catalog.NewServiceCommand.ExecuteAsync(null);
        catalog.Services.Should().ContainSingle(s => s.Service.PriceCents == 1500);

        // 3. A worker, who forgets the schedule on the first try
        var workers = await Open<WorkersViewModel>(shell, () => shell.NavigateWorkersCommand.Execute(null));
        await workers.Load();
        user.Then<WorkerDialogViewModel>(async vm =>
        {
            vm.Name = "Eva";
            await vm.SaveCommand.ExecuteAsync(null);
            vm.ErrorValidation.Should().Be(Texts.AtLeastOneDayRequired);

            vm.ScheduleDays[0].WorksMorning = true;
            vm.ScheduleDays[0].MorningStart = "09:00";
            vm.ScheduleDays[0].MorningEnd = "14:00";
            vm.CopyToWeekdaysCommand.Execute(null);
            await vm.SaveCommand.ExecuteAsync(null);
        });
        await workers.NewWorkerCommand.ExecuteAsync(null);
        workers.HasNone.Should().BeFalse();

        // 4. The first registered client, then an appointment for them today
        var clients = await Open<ClientsViewModel>(shell, () => shell.NavigateClientsCommand.Execute(null));
        user.Then<ClientDialogViewModel>(async vm =>
        {
            vm.Name = "Joan Puig";
            await vm.SaveCommand.ExecuteAsync(null);
            vm.ErrorValidation.Should().Be(Texts.NameAndMobileRequired, "the mobile was forgotten");

            vm.Mobile = "612 345 678";
            await vm.SaveCommand.ExecuteAsync(null);
        });
        await clients.NewClientCommand.ExecuteAsync(null);
        clients.Active.Should().ContainSingle(c => c.Name == "Joan Puig");

        home = await Open<HomeViewModel>(shell, () => shell.NavigateHomeCommand.Execute(null));
        user.Answers(true)    // an overlap or opening-hours question, if one comes, is accepted
            .Then<AppointmentDialogViewModel>(async vm =>
            {
                await vm.Initialization;
                // Typed loosely, without the accent or the surname, then picked with Enter
                vm.ClientPicker.Text = "joan";
                vm.ClientPicker.PickHighlightedCommand.Execute(null);
                vm.SelectedClient.Should().NotBeNull();
                vm.Service = vm.ActiveServices.Single();
                vm.Worker = vm.ActiveWorkers.Single();
                vm.TimeText = "11:00";
                await vm.SaveCommand.ExecuteAsync(null);
            });
        await home.NewAppointmentCommand.ExecuteAsync(null);
        home.DayAppointments.Should().ContainSingle();

        // 5. The appointment is charged from Home, prefilled from the booking
        var appointment = home.DayAppointments[0];
        user.Then<SaleDialogViewModel>(async vm =>
        {
            vm.SelectedClient!.Name.Should().Be("Joan Puig");
            vm.Worker!.Name.Should().Be("Eva");
            vm.Lines.Should().ContainSingle(l => l.Description == "Tall" && l.PriceText == "15,00");
            vm.PaymentMethod = vm.ActiveMethods.Single(m => m.Name == "Targeta");
            await vm.ChargeCommand.ExecuteAsync(null);
        });
        await home.MarkCompletedCommand.ExecuteAsync(appointment);
        home.DayAppointments.Single().Status.Should().Be(AppointmentStatus.Completed);
        home.TodaySales.Should().Be(2);

        // 6. The till and the reports see the whole day
        var till = await Open<TillViewModel>(shell, () => shell.NavigateTillCommand.Execute(null));
        await till.Load();
        till.Summary!.SalesCents.Should().Be(1200 + 1500);
        (till.Summary.BaseCents + till.Summary.VatCents).Should().Be(till.Summary.SalesCents);
        till.VatRates.Should().ContainSingle("both sales were at 21 %");

        var reports = await Open<ReportsViewModel>(shell, () => shell.NavigateReportsCommand.Execute(null));
        await reports.Load();
        reports.ClientOfTheMonthText.Should().Contain("Joan Puig", "the guest's sale does not make them a client");
        reports.WorkerRanking.Should().ContainSingle(r => r.Name == "Eva" && r.SalesHandled == 1);

        user.ScriptFinished.Should().BeTrue();

        // 7. Closing and starting again: everything is still there
        shell.NavigateHomeCommand.Execute(null);
        var (again, _) = await Start(new ScriptedUser());
        var summary = await again.GetRequiredService<ITillService>().Summary(
            DateOnly.FromDateTime(DateTime.Today), DateOnly.FromDateTime(DateTime.Today));
        summary.SalesCents.Should().Be(2700);
    }
}
