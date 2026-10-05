using System.Text.Json;
using AwesomeAssertions;
using EvaGest.Models;
using EvaGest.Services;
using EvaGest.Tests.Infra;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EvaGest.Tests.Services;

/// <summary>
/// Cancelling, marking no-show, reopening or deleting an appointment must leave a row
/// in audit_entries, which travels inside every backup, not only a line in the log on
/// this PC. Each row says which appointment it was and what it looked like before (and,
/// for a status change, after).
/// </summary>
public class AppointmentAuditTests
{
    private static readonly DateOnly Day = new(2026, 10, 12);

    private static async Task<int> Book(AppointmentService appointments, AppointmentStatus status = AppointmentStatus.Pending)
        => await appointments.Create(new Appointment
        {
            Date = Day, Time = new TimeOnly(10, 0), DurationMin = 30,
            GuestName = "Pere Martí", Notes = "Primera visita", Status = status
        });

    /// <summary>Reads one field out of a JSON snapshot without depending on the record
    /// type the service happens to serialise.</summary>
    private static string? Field(string? json, string name)
    {
        json.Should().NotBeNull();
        using var doc = JsonDocument.Parse(json!);
        return doc.RootElement.TryGetProperty(name, out var value) ? value.ToString() : null;
    }

    private static async Task<List<AuditEntry>> AuditOf(TestDatabase testDb, int appointmentId)
    {
        await using var db = testDb.Context();
        return await db.AuditEntries
            .Where(e => e.Entity == "Appointment" && e.EntityId == appointmentId)
            .OrderBy(e => e.Id)
            .ToListAsync();
    }

    [Theory]
    [InlineData(AppointmentStatus.Cancelled)]
    [InlineData(AppointmentStatus.NoShow)]
    public async Task Closing_an_appointment_without_a_sale_leaves_an_audit_row_with_both_states(AppointmentStatus closed)
    {
        await using var testDb = new TestDatabase();
        var appointments = new AppointmentService(new TestFactory(testDb.Options));
        int id = await Book(appointments);

        (await appointments.ChangeStatus(id, closed)).Should().BeTrue();

        var entry = (await AuditOf(testDb, id)).Should().ContainSingle().Subject;
        Field(entry.Before, "Status").Should().Be(nameof(AppointmentStatus.Pending));
        Field(entry.After, "Status").Should().Be(closed.ToString());
        Field(entry.Before, "GuestName").Should().Be("Pere Martí", "the row has to say which visit it was");
    }

    [Fact]
    public async Task Reopening_a_cancelled_appointment_leaves_an_audit_row_with_both_states()
    {
        await using var testDb = new TestDatabase();
        var appointments = new AppointmentService(new TestFactory(testDb.Options));
        int id = await Book(appointments, AppointmentStatus.Cancelled);

        (await appointments.ChangeStatus(id, AppointmentStatus.Pending)).Should().BeTrue();

        var entry = (await AuditOf(testDb, id)).Should().ContainSingle().Subject;
        Field(entry.Before, "Status").Should().Be(nameof(AppointmentStatus.Cancelled));
        Field(entry.After, "Status").Should().Be(nameof(AppointmentStatus.Pending));
    }

    [Fact]
    public async Task Every_status_change_gets_its_own_audit_row()
    {
        await using var testDb = new TestDatabase();
        var appointments = new AppointmentService(new TestFactory(testDb.Options));
        int id = await Book(appointments);

        await appointments.ChangeStatus(id, AppointmentStatus.NoShow);
        await appointments.ChangeStatus(id, AppointmentStatus.Pending);
        await appointments.ChangeStatus(id, AppointmentStatus.Cancelled);

        var entries = await AuditOf(testDb, id);
        entries.Select(e => Field(e.After, "Status")).Should().Equal(
            nameof(AppointmentStatus.NoShow), nameof(AppointmentStatus.Pending), nameof(AppointmentStatus.Cancelled));
    }

    [Fact]
    public async Task Deleting_an_appointment_leaves_an_audit_row_with_what_it_was()
    {
        await using var testDb = new TestDatabase();
        var appointments = new AppointmentService(new TestFactory(testDb.Options));
        int id = await Book(appointments);

        (await appointments.Delete(id)).Should().Be(DeleteResult.Deleted);

        (await appointments.GetById(id)).Should().BeNull();
        var entry = (await AuditOf(testDb, id)).Should().ContainSingle().Subject;
        Field(entry.Before, "GuestName").Should().Be("Pere Martí");
        Field(entry.Before, "Date").Should().Be("2026-10-12");
        Field(entry.Before, "Status").Should().Be(nameof(AppointmentStatus.Pending));
        entry.After.Should().BeNull("nothing is left after a delete");
    }

    [Fact]
    public async Task A_refused_status_change_leaves_no_audit_row()
    {
        // A charged appointment cannot be cancelled (F-05); a row for a change that did
        // not happen would make the audit trail lie.
        await using var testDb = new TestDatabase();
        var factory = new TestFactory(testDb.Options);
        var config = new SettingsService(factory);
        await new SeedService(factory, config).Seed();
        var appointments = new AppointmentService(factory);
        var sales = new SaleService(factory, config);
        int id = await Book(appointments);

        var sale = await sales.PrepareFromAppointment(id);
        await using (var db = testDb.Context())
            sale.PaymentMethodId = (await db.PaymentMethods.FirstAsync()).Id;
        await sales.Create(sale, [Make.Line(1500)]);

        (await appointments.ChangeStatus(id, AppointmentStatus.Cancelled)).Should().BeFalse();

        (await AuditOf(testDb, id)).Should().BeEmpty();
    }
}
