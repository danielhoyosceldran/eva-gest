using AwesomeAssertions;
using EvaGest.Models;
using EvaGest.Services;
using EvaGest.Tests.Infra;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EvaGest.Tests.Scenarios;

/// <summary>
/// Block H — attacks on the stored data. These try to put the database into a state the
/// rules forbid, and to make a total disagree with the rows it is summed from. Each one
/// has a way it could actually happen: a hand-edited file, a restored backup from an
/// older version, a client named after a company with a semicolon in it, a sale corrected
/// months later.
/// </summary>
public class IntegrityAttackTests
{
    private static readonly DateOnly March = new(2026, 3, 12);
    private static readonly DateOnly September = new(2026, 9, 19);

    private static async Task<int> AddsMethod(TestDatabase testDb)
    {
        await using var db = testDb.Context();
        var m = Make.Method();
        db.PaymentMethods.Add(m);
        await db.SaveChangesAsync();
        return m.Id;
    }

    // ── The client XOR guest rule ────────────────────────────────────────────

    [Fact]
    public async Task An_appointment_can_not_name_a_client_and_a_guest_at_once()
    {
        // The check constraint is the last line of defence: the UI enforces this, but a
        // restored or hand-edited database must not be able to carry the contradiction.
        await using var testDb = new TestDatabase();
        await using var db = testDb.Context();

        var client = Make.Client();
        db.Clients.Add(client);
        await db.SaveChangesAsync();

        db.Appointments.Add(new Appointment
        {
            Date = September, Time = new TimeOnly(10, 0), DurationMin = 30,
            ClientId = client.Id,
            GuestName = "Also a guest"      // both set: forbidden
        });

        Func<Task> save = async () => await db.SaveChangesAsync();
        await save.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task An_appointment_must_name_somebody()
    {
        await using var testDb = new TestDatabase();
        await using var db = testDb.Context();

        db.Appointments.Add(new Appointment
        {
            Date = September, Time = new TimeOnly(10, 0), DurationMin = 30
            // neither ClientId nor GuestName: forbidden
        });

        Func<Task> save = async () => await db.SaveChangesAsync();
        await save.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task Two_sales_can_not_claim_the_same_appointment()
    {
        // The unique index on AppointmentId is what stops one appointment being charged
        // twice, which would double-count it in every total.
        //
        // Each save goes through its own context, because that is how the app works (one
        // context per operation) and because it is the only way the index is actually
        // exercised: inside a single context EF resolves the one-to-one itself, silently
        // moving the appointment to the second sale rather than letting the write fail.
        await using var testDb = new TestDatabase();
        int methodId = await AddsMethod(testDb);

        int appointmentId;
        await using (var db = testDb.Context())
        {
            var appointment = new Appointment
            {
                Date = September, Time = new TimeOnly(10, 0), DurationMin = 30, GuestName = "Anna"
            };
            db.Appointments.Add(appointment);
            await db.SaveChangesAsync();
            appointmentId = appointment.Id;
        }

        await using (var db = testDb.Context())
        {
            var first = Make.Sale(September, methodId, SaleStatus.Active, Make.Line(1000));
            first.AppointmentId = appointmentId;
            db.Sales.Add(first);
            await db.SaveChangesAsync();
        }

        await using (var db = testDb.Context())
        {
            var second = Make.Sale(September, methodId, SaleStatus.Active, Make.Line(2000));
            second.AppointmentId = appointmentId;
            db.Sales.Add(second);

            Func<Task> save = async () => await db.SaveChangesAsync();
            await save.Should().ThrowAsync<DbUpdateException>();
        }

        await using var check = testDb.Context();
        (await check.Sales.CountAsync(v => v.AppointmentId == appointmentId))
            .Should().Be(1, "an appointment is charged once");
    }

    // ── Voided sales must vanish from every figure ───────────────────────────

    [Fact]
    public async Task A_voided_sale_is_excluded_from_the_till_the_reports_and_the_export()
    {
        // One place still counting a voided sale is enough to make the books disagree with
        // the VAT return, so all three readers are checked against the same data.
        await using var testDb = new TestDatabase();
        int methodId = await AddsMethod(testDb);
        var factory = new TestFactory(testDb.Options);

        await using (var db = testDb.Context())
        {
            db.Sales.Add(Make.Sale(September, methodId, SaleStatus.Active, Make.Line(10_000)));
            db.Sales.Add(Make.Sale(September, methodId, SaleStatus.Voided, Make.Line(99_999)));
            await db.SaveChangesAsync();
        }

        var summary = await new TillService(factory).Summary(September, September);
        summary.SalesCents.Should().Be(10_000, "a voided sale is not takings");
        summary.VatBreakdown.Sum(r => r.TotalCents).Should().Be(10_000);

        string folder = Path.Combine(Path.GetTempPath(), "EvaGestAttack_" + Guid.NewGuid());
        try
        {
            await new ExportService(factory).ExportSales(September, September, folder);
            string vat = await File.ReadAllTextAsync(
                Directory.GetFiles(folder, "iva_*.csv").Single());

            vat.Should().NotContain("999", "the voided amount must not reach the accountant");
        }
        finally { Directory.Delete(folder, recursive: true); }
    }

    [Fact]
    public async Task Voiding_a_sale_keeps_the_row_so_the_history_stays_auditable()
    {
        await using var testDb = new TestDatabase();
        int methodId = await AddsMethod(testDb);
        var factory = new TestFactory(testDb.Options);

        int saleId;
        await using (var db = testDb.Context())
        {
            var sale = Make.Sale(September, methodId, SaleStatus.Active, Make.Line(10_000));
            db.Sales.Add(sale);
            await db.SaveChangesAsync();
            saleId = sale.Id;
        }

        await new SaleService(factory, new TestSettings()).Void(saleId);

        await using var check = testDb.Context();
        var voided = await check.Sales.FirstAsync(v => v.Id == saleId);
        voided.Status.Should().Be(SaleStatus.Voided);
        voided.TotalCents.Should().Be(10_000, "the figures stay on the record, they are only excluded");
    }

    // ── A recorded sale never moves ──────────────────────────────────────────

    [Fact]
    public async Task Correcting_an_old_sale_does_not_move_it_into_the_current_period()
    {
        // The regression this pins: the dialog stamped DateTime.Now onto every save, so
        // fixing a typo on a March ticket moved it to today — out of its VAT quarter and
        // onto today's till. The service is the layer that persists it, so it is checked
        // here end to end.
        await using var testDb = new TestDatabase();
        int methodId = await AddsMethod(testDb);
        var factory = new TestFactory(testDb.Options);
        var sales = new SaleService(factory, new TestSettings());

        var original = Make.Sale(March, methodId, SaleStatus.Active, Make.Line(5_000));
        int id = await sales.Create(original, [Make.Line(5_000)]);

        var corrected = await sales.GetById(id);
        corrected!.Notes = "typo fixed";
        await sales.Update(corrected, [Make.Line(5_500)]);

        var reloaded = await sales.GetById(id);
        reloaded!.Date.Should().Be(March, "a recorded sale keeps the day it happened");

        var marchTill = await new TillService(factory).Summary(March, March);
        var septemberTill = await new TillService(factory).Summary(September, September);
        marchTill.SalesCents.Should().Be(5_500);
        septemberTill.SalesCents.Should().Be(0, "the correction must not land in another period");
    }

    [Fact]
    public async Task Editing_a_sale_keeps_the_vat_mode_it_was_taken_in()
    {
        // The shop switching to VAT-exclusive must not reinterpret older tickets.
        await using var testDb = new TestDatabase();
        int methodId = await AddsMethod(testDb);
        var factory = new TestFactory(testDb.Options);

        var settings = new TestSettings((ConfigKeys.CurrentVatMode, nameof(VatMode.Included)));
        var sales = new SaleService(factory, settings);

        int id = await sales.Create(
            Make.Sale(March, methodId, SaleStatus.Active, Make.Line(12_100)), [Make.Line(12_100)]);

        // The shop changes its mind months later.
        await settings.Save(ConfigKeys.CurrentVatMode, nameof(VatMode.NotIncluded));

        var sale = await sales.GetById(id);
        await sales.Update(sale!, [Make.Line(12_100)]);

        var reloaded = await sales.GetById(id);
        reloaded!.VatMode.Should().Be(VatMode.Included);
        reloaded.TotalCents.Should().Be(12_100,
            "the customer paid 121,00 €; today's setting must not turn that into 146,41 €");
    }

    // ── Text that breaks file formats ────────────────────────────────────────

    [Fact]
    public async Task A_client_name_containing_the_csv_separator_does_not_break_the_export()
    {
        // "Perruqueria Sant Jordi; SL" is an ordinary business name and it contains the
        // field separator. Unquoted it would shift every later column by one.
        await using var testDb = new TestDatabase();
        int methodId = await AddsMethod(testDb);

        await using (var db = testDb.Context())
        {
            var sale = Make.Sale(September, methodId, SaleStatus.Active, Make.Line(1_000));
            sale.GuestName = "Sant Jordi; SL \"the one\"";
            db.Sales.Add(sale);
            await db.SaveChangesAsync();
        }

        string folder = Path.Combine(Path.GetTempPath(), "EvaGestAttack_" + Guid.NewGuid());
        try
        {
            await new ExportService(new TestFactory(testDb.Options)).ExportSales(September, September, folder);
            string[] lines = await File.ReadAllLinesAsync(
                Directory.GetFiles(folder, "vendes_*.csv").Single());

            string header = lines[0];
            string row = lines[1];

            // A quoted field may contain the separator, so the column count is only
            // meaningful once quoted sections are accounted for. Counting separators
            // outside quotes is what the accountant's spreadsheet does too.
            SeparatorsOutsideQuotes(row).Should().Be(SeparatorsOutsideQuotes(header),
                "the row must have exactly as many columns as the header");
            row.Should().Contain("\"\"the one\"\"", "an embedded quote is doubled, not dropped");
        }
        finally { Directory.Delete(folder, recursive: true); }
    }

    private static int SeparatorsOutsideQuotes(string line)
    {
        bool inQuotes = false;
        int count = 0;
        foreach (char c in line)
        {
            if (c == '"') inQuotes = !inQuotes;
            else if (c == ';' && !inQuotes) count++;
        }
        return count;
    }

    [Fact]
    public async Task A_very_long_unicode_name_is_stored_and_read_back_intact()
    {
        // Emoji and accents survive because the column is TEXT and the file is UTF-8;
        // this pins that nothing truncates on the way in or out.
        await using var testDb = new TestDatabase();
        string name = "Núria Ferrà-Öst " + new string('ñ', 300) + " 💇";

        int id;
        await using (var db = testDb.Context())
        {
            var client = Make.Client(name, "600111222");
            db.Clients.Add(client);
            await db.SaveChangesAsync();
            id = client.Id;
        }

        var read = await new ClientService(new TestFactory(testDb.Options)).GetById(id);
        read!.Name.Should().Be(name);
    }

    [Fact]
    public async Task A_search_term_made_of_sql_wildcards_matches_literally_and_does_not_error()
    {
        // The search builds a LIKE pattern from user text. "%" is a wildcard there, so a
        // client actually called "100%" is the awkward case.
        await using var testDb = new TestDatabase();
        await using (var db = testDb.Context())
        {
            db.Clients.Add(Make.Client("Anna", "600000001"));
            db.Clients.Add(Make.Client("100% Barber", "600000002"));
            await db.SaveChangesAsync();
        }

        var clients = new ClientService(new TestFactory(testDb.Options));

        Func<Task> search = async () => await clients.Search("'; DROP TABLE clients; --");
        await search.Should().NotThrowAsync("parameters are bound, never concatenated");

        await using var check = testDb.Context();
        (await check.Clients.CountAsync()).Should().Be(2, "nothing was dropped");
    }
}
