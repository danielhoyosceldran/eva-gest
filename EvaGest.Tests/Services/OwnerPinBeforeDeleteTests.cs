using AwesomeAssertions;
using EvaGest.Models;
using EvaGest.Services;
using EvaGest.Tests.Infra;
using EvaGest.ViewModels.Dialogs;
using EvaGest.ViewModels.Pages;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EvaGest.Tests.Services;

/// <summary>
/// F-05: any worker could delete a client with owner mode closed, and some deletes and
/// voids asked nothing at all (voiding from the sale dialog, removing a closed day).
/// Every delete and void must now ask for the owner's PIN — even with owner mode already
/// open — and when the PIN is not given, nothing changes. The one exception is an
/// appointment: it is only a booking, so it is deleted after a plain "are you sure?".
///
/// The plain confirmation answers "yes" throughout: what stops the delete has to be the
/// PIN, not an ordinary "are you sure?".
/// </summary>
public class OwnerPinBeforeDeleteTests
{
    private static readonly DateOnly Monday = new(2026, 10, 12);

    /// <summary>The user would confirm, but does not type the owner's PIN.</summary>
    private static TestDialogService PinRefused() => new() { ResultConfirm = true, ResultPinConfirm = false };

    /// <summary>The user confirms and the owner's PIN is accepted.</summary>
    private static TestDialogService PinGiven() => new() { ResultConfirm = true, ResultPinConfirm = true };

    private static async Task<int> AddMethod(TestDatabase testDb, string name = "Efectiu")
    {
        await using var db = testDb.Context();
        var method = Make.Method(name);
        db.PaymentMethods.Add(method);
        await db.SaveChangesAsync();
        return method.Id;
    }

    // ── Clients ──────────────────────────────────────────────────────────────

    private static async Task<(ClientRecordViewModel vm, int id)> ClientRecord(TestDatabase testDb, IDialogService dialogs)
    {
        var factory = new TestFactory(testDb.Options);
        var clients = new ClientService(factory);
        int id = await clients.Create(Make.Client("Joan Garcia"));
        var vm = new ClientRecordViewModel(clients, new ReportsService(factory), dialogs,
            (await clients.GetById(id))!, showAmounts: false);
        await vm.Load();
        return (vm, id);
    }

    [Fact]
    public async Task Deleting_a_client_without_the_owners_pin_keeps_the_client()
    {
        await using var testDb = new TestDatabase();
        var dialogs = PinRefused();
        var (vm, id) = await ClientRecord(testDb, dialogs);

        await vm.DeleteCommand.ExecuteAsync(null);

        dialogs.PinConfirmationsRequested.Should().ContainSingle("deleting a client needs the owner's PIN");
        await using var check = testDb.Context();
        (await check.Clients.AnyAsync(c => c.Id == id)).Should().BeTrue();
    }

    [Fact]
    public async Task Deleting_a_client_with_the_owners_pin_deletes_it()
    {
        await using var testDb = new TestDatabase();
        var dialogs = PinGiven();
        var (vm, id) = await ClientRecord(testDb, dialogs);

        await vm.DeleteCommand.ExecuteAsync(null);

        dialogs.PinConfirmationsRequested.Should().ContainSingle();
        await using var check = testDb.Context();
        (await check.Clients.AnyAsync(c => c.Id == id)).Should().BeFalse();
    }

    // ── Appointments ─────────────────────────────────────────────────────────

    private static async Task<int> AddAppointment(AppointmentService appointments)
        => await appointments.Create(new Appointment
        {
            Date = Monday, Time = new TimeOnly(10, 0), DurationMin = 30, GuestName = "Anna",
            Status = AppointmentStatus.Pending
        });

    [Fact]
    public async Task Deleting_an_appointment_from_the_agenda_asks_no_pin_and_deletes_it()
    {
        await using var testDb = new TestDatabase();
        var factory = new TestFactory(testDb.Options);
        var settings = new SettingsService(factory);
        var appointments = new AppointmentService(factory);
        int id = await AddAppointment(appointments);
        var dialogs = PinRefused();
        var page = new AgendaViewModel(appointments, new SaleService(factory, settings),
            new AvailabilityService(factory), new ClientService(factory), new CatalogService(factory),
            new WorkerService(factory), settings, new TestSoundService(), dialogs);

        await page.DeleteAppointmentCommand.ExecuteAsync((await appointments.GetById(id))!);

        dialogs.PinConfirmationsRequested.Should().BeEmpty("deleting an appointment needs no password");
        dialogs.ConfirmacionsRequested.Should().ContainSingle();
        (await appointments.GetById(id)).Should().BeNull();
    }

    [Fact]
    public async Task Deleting_an_appointment_from_its_dialog_asks_no_pin_and_deletes_it()
    {
        await using var testDb = new TestDatabase();
        var factory = new TestFactory(testDb.Options);
        var appointments = new AppointmentService(factory);
        int id = await AddAppointment(appointments);
        var dialogs = PinRefused();
        var vm = new AppointmentDialogViewModel(appointments, new AvailabilityService(factory),
            new ClientService(factory), new CatalogService(factory), new WorkerService(factory),
            new SettingsService(factory), dialogs, (await appointments.GetById(id))!);
        await vm.Initialization;

        await vm.DeleteCommand.ExecuteAsync(null);

        dialogs.PinConfirmationsRequested.Should().BeEmpty("deleting an appointment needs no password");
        dialogs.ConfirmacionsRequested.Should().ContainSingle();
        (await appointments.GetById(id)).Should().BeNull();
    }

    // ── Sales ────────────────────────────────────────────────────────────────

    private static async Task<(SaleService sales, SettingsService settings, int saleId)> ASale(TestDatabase testDb)
    {
        var factory = new TestFactory(testDb.Options);
        var settings = new SettingsService(factory);
        var sales = new SaleService(factory, settings);
        int methodId = await AddMethod(testDb);
        int saleId = await sales.Create(Make.Sale(DateOnly.FromDateTime(DateTime.Today), methodId,
            SaleStatus.Active, Make.Line(2_500)), [Make.Line(2_500)]);
        return (sales, settings, saleId);
    }

    private static SalesViewModel SalesPage(TestDatabase testDb, SaleService sales, SettingsService settings,
        IDialogService dialogs)
    {
        var factory = new TestFactory(testDb.Options);
        return new SalesViewModel(sales, new ClientService(factory), new CatalogService(factory),
            new WorkerService(factory), new TestSoundService(), settings, new ExportService(factory), dialogs);
    }

    [Fact]
    public async Task Voiding_a_sale_from_the_sales_page_without_the_pin_leaves_it_active()
    {
        await using var testDb = new TestDatabase();
        var (sales, settings, id) = await ASale(testDb);
        var dialogs = PinRefused();
        var page = SalesPage(testDb, sales, settings, dialogs);
        await page.Load();

        await page.VoidSaleCommand.ExecuteAsync(page.Sales.Single().Sale);

        dialogs.PinConfirmationsRequested.Should().ContainSingle();
        (await sales.GetById(id))!.Status.Should().Be(SaleStatus.Active);
    }

    [Fact]
    public async Task Deleting_a_sale_from_the_sales_page_without_the_pin_leaves_it_alone()
    {
        await using var testDb = new TestDatabase();
        var (sales, settings, id) = await ASale(testDb);
        var dialogs = PinRefused();
        var page = SalesPage(testDb, sales, settings, dialogs);
        await page.Load();

        await page.DeleteSaleCommand.ExecuteAsync(page.Sales.Single().Sale);

        dialogs.PinConfirmationsRequested.Should().ContainSingle();
        var stored = await sales.GetById(id);
        stored.Should().NotBeNull();
        stored!.Status.Should().Be(SaleStatus.Active);
    }

    private static async Task<SaleDialogViewModel> EditSaleDialog(TestDatabase testDb, SaleService sales,
        SettingsService settings, int id, IDialogService dialogs)
    {
        var factory = new TestFactory(testDb.Options);
        return await SaleDialogViewModel.Edit(sales, new ClientService(factory), new CatalogService(factory),
            new WorkerService(factory), new TestSoundService(), settings, dialogs, (await sales.GetById(id))!);
    }

    [Fact]
    public async Task Voiding_a_sale_from_its_dialog_without_the_pin_leaves_it_active()
    {
        await using var testDb = new TestDatabase();
        var (sales, settings, id) = await ASale(testDb);
        var dialogs = PinRefused();
        var vm = await EditSaleDialog(testDb, sales, settings, id, dialogs);

        await vm.VoidSaleCommand.ExecuteAsync(null);

        dialogs.PinConfirmationsRequested.Should().ContainSingle("voiding from the dialog used to ask nothing");
        (await sales.GetById(id))!.Status.Should().Be(SaleStatus.Active);
    }

    [Fact]
    public async Task Voiding_a_sale_from_its_dialog_with_the_pin_voids_it()
    {
        await using var testDb = new TestDatabase();
        var (sales, settings, id) = await ASale(testDb);
        var dialogs = PinGiven();
        var vm = await EditSaleDialog(testDb, sales, settings, id, dialogs);

        await vm.VoidSaleCommand.ExecuteAsync(null);

        dialogs.PinConfirmationsRequested.Should().ContainSingle();
        (await sales.GetById(id))!.Status.Should().Be(SaleStatus.Voided);
    }

    // ── Till ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Deleting_a_cash_movement_without_the_pin_keeps_it()
    {
        await using var testDb = new TestDatabase();
        int methodId = await AddMethod(testDb);
        var factory = new TestFactory(testDb.Options);
        var till = new TillService(factory);
        int id = await till.Create(new CashMovement
        {
            Date = DateOnly.FromDateTime(DateTime.Today), Type = MovementType.In, AmountCents = 2_000,
            PaymentMethodId = methodId, Concept = "Fons"
        });
        var dialogs = PinRefused();
        var page = new TillViewModel(till, new CatalogService(factory), new WorkerService(factory),
            new SettingsService(factory), dialogs);
        await page.Load();

        await page.DeleteCommand.ExecuteAsync(page.Movements.Single(r => r.Movement.Id == id));

        dialogs.PinConfirmationsRequested.Should().ContainSingle();
        await using var check = testDb.Context();
        var stored = await check.CashMovements.SingleAsync(m => m.Id == id);
        stored.Status.Should().Be(MovementStatus.Active);
    }

    // ── Catalogue and workers ────────────────────────────────────────────────

    private static async Task<(CatalogViewModel page, TestFactory factory)> CatalogPage(
        TestDatabase testDb, IDialogService dialogs)
    {
        var factory = new TestFactory(testDb.Options);
        await using (var db = testDb.Context())
        {
            db.Services.Add(Make.Service("Tall"));
            db.Products.Add(Make.Product("Cera"));
            db.PaymentMethods.AddRange(Make.Method("Efectiu"), Make.Method("Targeta"));
            db.ExpenseCategories.Add(new ExpenseCategory { Name = "Material", Active = true });
            await db.SaveChangesAsync();
        }
        var page = new CatalogViewModel(new CatalogService(factory), new SettingsService(factory), dialogs);
        await page.Load();
        return (page, factory);
    }

    [Fact]
    public async Task Deleting_a_service_without_the_pin_keeps_it()
    {
        await using var testDb = new TestDatabase();
        var dialogs = PinRefused();
        var (page, _) = await CatalogPage(testDb, dialogs);

        await page.DeleteServiceCommand.ExecuteAsync(page.Services.Single().Service);

        dialogs.PinConfirmationsRequested.Should().ContainSingle();
        await using var check = testDb.Context();
        (await check.Services.SingleAsync()).Active.Should().BeTrue();
    }

    [Fact]
    public async Task Deleting_a_product_without_the_pin_keeps_it()
    {
        await using var testDb = new TestDatabase();
        var dialogs = PinRefused();
        var (page, _) = await CatalogPage(testDb, dialogs);

        await page.DeleteProductCommand.ExecuteAsync(page.Products.Single().Product);

        dialogs.PinConfirmationsRequested.Should().ContainSingle();
        await using var check = testDb.Context();
        (await check.Products.SingleAsync()).Active.Should().BeTrue();
    }

    [Fact]
    public async Task Deleting_a_payment_method_without_the_pin_keeps_it()
    {
        await using var testDb = new TestDatabase();
        var dialogs = PinRefused();
        var (page, _) = await CatalogPage(testDb, dialogs);

        await page.DeleteMethodCommand.ExecuteAsync(page.Methods.Single(m => m.Name == "Targeta"));

        dialogs.PinConfirmationsRequested.Should().ContainSingle();
        await using var check = testDb.Context();
        (await check.PaymentMethods.CountAsync(m => m.Active)).Should().Be(2);
    }

    [Fact]
    public async Task Deleting_an_expense_category_without_the_pin_keeps_it()
    {
        await using var testDb = new TestDatabase();
        var dialogs = PinRefused();
        var (page, _) = await CatalogPage(testDb, dialogs);

        await page.DeleteCategoryCommand.ExecuteAsync(page.Categories.Single());

        dialogs.PinConfirmationsRequested.Should().ContainSingle();
        await using var check = testDb.Context();
        (await check.ExpenseCategories.SingleAsync()).Active.Should().BeTrue();
    }

    [Fact]
    public async Task Deleting_a_worker_without_the_pin_keeps_her()
    {
        await using var testDb = new TestDatabase();
        var workers = new WorkerService(new TestFactory(testDb.Options));
        var marta = await workers.Create(Make.Worker("Marta"),
            new Dictionary<Weekday, List<(TimeOnly start, TimeOnly fi)>>());
        var dialogs = PinRefused();
        var page = new WorkersViewModel(workers, dialogs);
        await page.Load();

        await page.DeleteWorkerCommand.ExecuteAsync(marta);

        dialogs.PinConfirmationsRequested.Should().ContainSingle();
        (await workers.GetAll()).Should().ContainSingle(w => w.Id == marta.Id && w.Active);
    }

    // ── Settings ─────────────────────────────────────────────────────────────

    private sealed class Folder : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "EvaGestPinSettings_" + Guid.NewGuid());

        public Folder() => Directory.CreateDirectory(Path);

        public void Dispose() => Directory.Delete(Path, recursive: true);
    }

    private static async Task<SettingsViewModel> SettingsPage(TestDatabase testDb, Folder folder, IDialogService dialogs)
    {
        var factory = new TestFactory(testDb.Options);
        var settings = new SettingsService(factory);
        var paths = new AppPaths(Path.Combine(folder.Path, "barberia.db"), folder.Path);
        return new SettingsViewModel(new BackupService(paths, settings), new ExportService(factory), settings,
            new AvailabilityService(factory), dialogs, await TestOwner.Unlocked());
    }

    [Fact]
    public async Task Removing_a_closed_day_without_the_pin_keeps_it_even_with_owner_mode_open()
    {
        await using var testDb = new TestDatabase();
        using var folder = new Folder();
        var dialogs = PinRefused();
        var page = await SettingsPage(testDb, folder, dialogs);
        await page.Load();
        page.NewClosedDate = new DateOnly(2026, 12, 25);
        page.NewClosedReason = "Nadal";
        await page.AddClosedDayCommand.ExecuteAsync(null);

        await page.RemoveClosedDayCommand.ExecuteAsync(page.ClosedDays.Single());

        dialogs.PinConfirmationsRequested.Should().ContainSingle("removing a closed day used to ask nothing");
        page.ClosedDays.Should().ContainSingle();
        await using var check = testDb.Context();
        (await check.ClosedDays.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Cutting_the_backups_kept_without_the_pin_deletes_none()
    {
        await using var testDb = new TestDatabase();
        using var folder = new Folder();
        string backupsFolder = Path.Combine(folder.Path, "Backups");
        Directory.CreateDirectory(backupsFolder);
        for (int i = 1; i <= 5; i++)
            File.WriteAllText(Path.Combine(backupsFolder, $"202601{i:00}_120000000_manual.db"), "x");
        var dialogs = PinRefused();
        var page = await SettingsPage(testDb, folder, dialogs);
        await page.Load();

        page.BackupsToKeepText = "1";
        await page.SaveBackupOptionsCommand.ExecuteAsync(null);

        dialogs.PinConfirmationsRequested.Should().ContainSingle();
        Directory.GetFiles(backupsFolder, "*.db").Should().HaveCount(5);
    }
}
