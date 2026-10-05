using AwesomeAssertions;
using EvaGest.Data;
using EvaGest.Models;
using EvaGest.Resources;
using EvaGest.Services;
using EvaGest.Tests.Infra;
using EvaGest.ViewModels.Pages;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Xunit;

namespace EvaGest.Tests.Services;

/// <summary>
/// F-04: the database cascaded a payment-method delete to every sale and cash movement
/// paid with it (only a check in the app stopped it), and a used method could not really
/// be deleted. Now a used method is deleted for good after a warning; its sales and
/// movements are kept with no method, amounts unchanged, each change audited, and shown
/// as "Mètode eliminat". The last active method still cannot go, and new sales and
/// movements still need a method.
/// </summary>
public sealed class DeletedPaymentMethodTests : IDisposable
{
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.Today);

    private readonly string _folder = Path.Combine(Path.GetTempPath(), "EvaGestDeletedMethod_" + Guid.NewGuid());

    public DeletedPaymentMethodTests() => Directory.CreateDirectory(_folder);

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    private sealed record Data(int CashId, int BizumId, int[] SaleIds, int MovementId);

    /// <summary>Two methods; two sales and one cash movement paid by Bizum.</summary>
    private static async Task<Data> Seed(DbContextOptions<ShopDbContext> options)
    {
        await using var db = new ShopDbContext(options);
        var cash = Make.Method("Efectiu");
        var bizum = Make.Method("Bizum");
        db.PaymentMethods.AddRange(cash, bizum);
        await db.SaveChangesAsync();

        var first = Make.Sale(Today, bizum.Id, SaleStatus.Active, Make.Line(1_500));
        var second = Make.Sale(Today, bizum.Id, SaleStatus.Active, Make.Line(2_200, 1000));
        var movement = new CashMovement
        {
            Date = Today, Type = MovementType.In, AmountCents = 5_000, PaymentMethodId = bizum.Id, Concept = "Fons"
        };
        db.Sales.AddRange(first, second);
        db.CashMovements.Add(movement);
        await db.SaveChangesAsync();

        return new Data(cash.Id, bizum.Id, [first.Id, second.Id], movement.Id);
    }

    // ── The service ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Deleting_a_used_method_removes_it_and_keeps_its_sales_and_movements_without_one()
    {
        await using var testDb = new TestDatabase();
        var d = await Seed(testDb.Options);
        List<(int Id, int Base, int Vat, int Total)> totalsBefore;
        await using (var db = testDb.Context())
            totalsBefore = await db.Sales.OrderBy(s => s.Id)
                .Select(s => ValueTuple.Create(s.Id, s.BaseCents, s.VatCents, s.TotalCents)).ToListAsync();

        var result = await new CatalogService(new TestFactory(testDb.Options)).DeleteMethod(d.BizumId);

        result.Should().Be(DeleteResult.Deleted, "a used method is deleted for good now, not deactivated");
        await using var check = testDb.Context();
        (await check.PaymentMethods.AnyAsync(m => m.Id == d.BizumId)).Should().BeFalse();

        var sales = await check.Sales.OrderBy(s => s.Id).ToListAsync();
        sales.Select(s => s.Id).Should().Equal(d.SaleIds, "no sale is lost with its method");
        sales.Should().OnlyContain(s => s.PaymentMethodId == null);
        sales.Select(s => (s.Id, s.BaseCents, s.VatCents, s.TotalCents)).Should().Equal(totalsBefore);

        var movement = await check.CashMovements.SingleAsync();
        movement.Id.Should().Be(d.MovementId);
        movement.PaymentMethodId.Should().BeNull();
        movement.AmountCents.Should().Be(5_000);
        movement.Status.Should().Be(MovementStatus.Active);
    }

    [Fact]
    public async Task Each_record_left_without_its_method_is_written_to_the_audit_trail()
    {
        await using var testDb = new TestDatabase();
        var d = await Seed(testDb.Options);

        await new CatalogService(new TestFactory(testDb.Options)).DeleteMethod(d.BizumId);

        await using var check = testDb.Context();
        var entries = await check.AuditEntries.ToListAsync();
        foreach (int saleId in d.SaleIds)
            entries.Should().Contain(e => e.Entity == AuditTrail.SaleEntity && e.EntityId == saleId,
                $"sale {saleId} changed and the change must be traceable");
        entries.Should().Contain(e => e.Entity == AuditTrail.CashMovementEntity && e.EntityId == d.MovementId);
    }

    [Fact]
    public async Task The_last_active_method_still_cannot_be_deleted()
    {
        await using var testDb = new TestDatabase();
        int onlyId;
        await using (var db = testDb.Context())
        {
            var only = Make.Method("Efectiu");
            db.PaymentMethods.Add(only);
            await db.SaveChangesAsync();
            db.Sales.Add(Make.Sale(Today, only.Id, SaleStatus.Active, Make.Line(1_000)));
            await db.SaveChangesAsync();
            onlyId = only.Id;
        }

        var result = await new CatalogService(new TestFactory(testDb.Options)).DeleteMethod(onlyId);

        result.Should().Be(DeleteResult.Blocked);
        await using var check = testDb.Context();
        (await check.PaymentMethods.AnyAsync(m => m.Id == onlyId)).Should().BeTrue();
        (await check.Sales.SingleAsync()).PaymentMethodId.Should().Be(onlyId);
    }

    // ── The database itself, without the service in the way ─────────────────

    private static async Task RemoveMethodRowDirectly(DbContextOptions<ShopDbContext> options, int methodId)
    {
        await using var db = new ShopDbContext(options);
        db.PaymentMethods.Remove(await db.PaymentMethods.SingleAsync(m => m.Id == methodId));
        await db.SaveChangesAsync();
    }

    private static async Task ShouldStillHoldEverything(DbContextOptions<ShopDbContext> options, Data d)
    {
        await using var check = new ShopDbContext(options);
        (await check.Sales.Select(s => s.Id).OrderBy(i => i).ToListAsync()).Should().Equal(d.SaleIds);
        (await check.SaleLines.CountAsync()).Should().Be(2);
        (await check.CashMovements.Select(m => m.Id).ToListAsync()).Should().Equal(d.MovementId);
    }

    [Fact]
    public async Task Removing_a_method_row_directly_deletes_no_sale_or_movement()
    {
        await using var testDb = new TestDatabase();
        var d = await Seed(testDb.Options);

        await RemoveMethodRowDirectly(testDb.Options, d.BizumId);

        await ShouldStillHoldEverything(testDb.Options, d);
    }

    [Fact]
    public async Task On_the_schema_the_migrations_build_removing_a_method_row_deletes_no_sale_or_movement()
    {
        // TestDatabase builds its schema from the model (EnsureCreated); the shop's file
        // is built by the migrations, so the foreign keys they create are checked too.
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ShopDbContext>()
            .UseSqlite(connection).UseSnakeCaseNamingConvention().Options;
        await using (var db = new ShopDbContext(options)) await db.Database.MigrateAsync();
        await using (var pragma = connection.CreateCommand())
        {
            pragma.CommandText = "PRAGMA foreign_keys = ON;";
            await pragma.ExecuteNonQueryAsync();
        }
        var d = await Seed(options);

        // Straight SQL, so not even EF's own model can step in.
        await using (var delete = connection.CreateCommand())
        {
            delete.CommandText = $"DELETE FROM payment_methods WHERE id = {d.BizumId};";
            await delete.ExecuteNonQueryAsync();
        }

        await ShouldStillHoldEverything(options, d);
        await using var check = new ShopDbContext(options);
        (await check.Sales.Select(s => s.PaymentMethodId).ToListAsync()).Should().OnlyContain(id => id == null);
    }

    [Fact]
    public async Task Upgrading_a_shop_database_with_sales_keeps_every_sale_line_and_movement()
    {
        // The schema change rebuilds the sales and cash_movements tables (SQLite cannot
        // alter a foreign key in place). A shop that installs the new version must keep
        // every row, the sale lines hanging off its sales included.
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ShopDbContext>()
            .UseSqlite(connection).UseSnakeCaseNamingConvention().Options;
        await using (var db = new ShopDbContext(options))
            await db.GetService<Microsoft.EntityFrameworkCore.Migrations.IMigrator>()
                .MigrateAsync("20261004163034_FilterAppointmentSaleIndexToActive");
        await using (var pragma = connection.CreateCommand())
        {
            pragma.CommandText = "PRAGMA foreign_keys = ON;";
            await pragma.ExecuteNonQueryAsync();
        }
        var d = await Seed(options);
        int breakdowns;
        await using (var db = new ShopDbContext(options)) breakdowns = await db.SaleBreakdowns.CountAsync();

        await using (var db = new ShopDbContext(options)) await db.Database.MigrateAsync();

        await ShouldStillHoldEverything(options, d);
        await using (var check = new ShopDbContext(options))
        {
            (await check.SaleBreakdowns.CountAsync()).Should().Be(breakdowns);
            (await check.Sales.Select(s => s.PaymentMethodId).ToListAsync()).Should().OnlyContain(id => id == d.BizumId);
        }

        // And on the upgraded file, removing the method keeps them too.
        await using (var delete = connection.CreateCommand())
        {
            delete.CommandText = $"DELETE FROM payment_methods WHERE id = {d.BizumId};";
            await delete.ExecuteNonQueryAsync();
        }
        await ShouldStillHoldEverything(options, d);
    }

    // ── The catalogue page ───────────────────────────────────────────────────

    [Fact]
    public async Task Deleting_a_used_method_from_the_catalogue_says_how_many_records_will_lose_it()
    {
        await using var testDb = new TestDatabase();
        var d = await Seed(testDb.Options);
        var factory = new TestFactory(testDb.Options);
        var catalog = new CatalogService(factory);
        var dialogs = new RecordingDialogService { ConfirmAnswer = false, PinConfirmAnswer = false };
        var page = new CatalogViewModel(catalog, new SettingsService(factory), dialogs);
        await page.Load();

        await page.DeleteMethodCommand.ExecuteAsync(page.Methods.Single(m => m.Id == d.BizumId));

        int uses = await catalog.CountMethodUses(d.BizumId);
        uses.Should().Be(3, "two sales and one cash movement are paid by Bizum");
        dialogs.AllQuestionMessages.Should().Contain(m => m.Contains(uses.ToString()),
            "the warning must say how many records will be left without a method");
    }

    [Fact]
    public async Task Refusing_the_warning_deletes_nothing()
    {
        await using var testDb = new TestDatabase();
        var d = await Seed(testDb.Options);
        var factory = new TestFactory(testDb.Options);
        var dialogs = new RecordingDialogService { ConfirmAnswer = false, PinConfirmAnswer = false };
        var page = new CatalogViewModel(new CatalogService(factory), new SettingsService(factory), dialogs);
        await page.Load();

        await page.DeleteMethodCommand.ExecuteAsync(page.Methods.Single(m => m.Id == d.BizumId));

        await using var check = testDb.Context();
        (await check.PaymentMethods.AnyAsync(m => m.Id == d.BizumId)).Should().BeTrue();
        (await check.Sales.Where(s => s.PaymentMethodId == d.BizumId).CountAsync()).Should().Be(2);
    }

    // ── Where a record without a method is shown ─────────────────────────────

    [Fact]
    public async Task The_sales_page_shows_a_sale_whose_method_was_deleted_as_deleted_method()
    {
        await using var testDb = new TestDatabase();
        var d = await Seed(testDb.Options);
        var factory = new TestFactory(testDb.Options);
        var settings = new SettingsService(factory);
        await new CatalogService(factory).DeleteMethod(d.BizumId);
        var page = new SalesViewModel(new SaleService(factory, settings), new ClientService(factory),
            new CatalogService(factory), new WorkerService(factory), new TestSoundService(), settings,
            new ExportService(factory), new TestDialogService());

        await page.Load();

        page.Sales.Should().HaveCount(2);
        page.Sales.Should().OnlyContain(r => r.MethodText == Texts.DeletedPaymentMethod);
    }

    [Fact]
    public async Task The_till_page_shows_a_movement_whose_method_was_deleted_as_deleted_method()
    {
        await using var testDb = new TestDatabase();
        var d = await Seed(testDb.Options);
        var factory = new TestFactory(testDb.Options);
        await new CatalogService(factory).DeleteMethod(d.BizumId);
        var page = new TillViewModel(new TillService(factory), new CatalogService(factory),
            new WorkerService(factory), new SettingsService(factory), new TestDialogService());

        await page.Load();

        page.Movements.Should().ContainSingle(r => r.Movement.Id == d.MovementId)
            .Which.MethodText.Should().Be(Texts.DeletedPaymentMethod);
    }

    [Fact]
    public async Task The_sales_csv_names_a_deleted_method_instead_of_leaving_a_blank()
    {
        await using var testDb = new TestDatabase();
        var d = await Seed(testDb.Options);
        var factory = new TestFactory(testDb.Options);
        await new CatalogService(factory).DeleteMethod(d.BizumId);

        await new ExportService(factory).ExportSales(Today, Today, _folder);

        var rows = (await File.ReadAllLinesAsync(Path.Combine(_folder, $"vendes_{Today:yyyyMMdd}-{Today:yyyyMMdd}.csv")))
            .Skip(1).Where(l => l.Length > 0).ToList();
        rows.Should().HaveCount(2);
        rows.Should().OnlyContain(r => r.Contains(Texts.DeletedPaymentMethod));
    }

    // ── New records still need a method ──────────────────────────────────────

    [Fact]
    public async Task A_new_sale_without_a_payment_method_is_refused()
    {
        await using var testDb = new TestDatabase();
        await Seed(testDb.Options);
        var factory = new TestFactory(testDb.Options);
        var sales = new SaleService(factory, new SettingsService(factory));
        var sale = Make.Sale(Today, 0, SaleStatus.Active, Make.Line(1_000));
        sale.PaymentMethodId = null;

        var create = () => sales.Create(sale, [Make.Line(1_000)]);

        await create.Should().ThrowAsync<ArgumentException>();
        await using var check = testDb.Context();
        (await check.Sales.CountAsync()).Should().Be(2, "only the seeded sales");
    }

    [Fact]
    public async Task An_edited_sale_cannot_be_left_without_a_payment_method()
    {
        await using var testDb = new TestDatabase();
        var d = await Seed(testDb.Options);
        var factory = new TestFactory(testDb.Options);
        var sales = new SaleService(factory, new SettingsService(factory));
        var sale = (await sales.GetById(d.SaleIds[0]))!;
        sale.PaymentMethodId = null;
        sale.PaymentMethod = null;

        var update = () => sales.Update(sale, [Make.Line(1_500)]);

        await update.Should().ThrowAsync<ArgumentException>();
        (await sales.GetById(d.SaleIds[0]))!.PaymentMethodId.Should().Be(d.BizumId);
    }

    [Fact]
    public async Task A_new_cash_movement_without_a_payment_method_is_refused()
    {
        await using var testDb = new TestDatabase();
        await Seed(testDb.Options);
        var till = new TillService(new TestFactory(testDb.Options));

        var create = () => till.Create(new CashMovement
        {
            Date = Today, Type = MovementType.In, AmountCents = 1_000, PaymentMethodId = null, Concept = "Canvi"
        });

        await create.Should().ThrowAsync<ArgumentException>();
        await using var check = testDb.Context();
        (await check.CashMovements.CountAsync()).Should().Be(1);
    }
}
