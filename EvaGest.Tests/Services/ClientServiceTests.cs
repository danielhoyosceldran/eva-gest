using AwesomeAssertions;
using EvaGest.Models;
using EvaGest.Services;
using EvaGest.Tests.Infra;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EvaGest.Tests.Services;

/// <summary>Block D (client key and duplicates) + Phase 4 smoke tests against a real
/// SQLite engine, so the unique index on ClientKey actually gets exercised.</summary>
public class ClientServiceTests
{
    private static ClientService CreatesService(TestDatabase testDb) => new(new TestFactory(testDb.Options));

    [Fact] // D-01
    public void ClientKey_ignores_the_phone_entirely()
    {
        // The phone used to be half the key. It is not any more: a name identifies a
        // client, so the same name on a new number is the same client (decision 6.1).
        string a = ClientService.ComputeClientKey("Joan García");
        string b = ClientService.ComputeClientKey("Joan García");
        a.Should().Be(b);
    }

    [Fact] // D-02
    public void ClientKey_normalizes_accents()
    {
        string a = ClientService.ComputeClientKey("Joán Garcia");
        string b = ClientService.ComputeClientKey("Joan Garcia");
        a.Should().Be(b);
    }

    [Fact] // D-03
    public void ClientKey_uses_the_whole_name_not_only_the_first()
    {
        // The inverse of what this pinned before. Two people who share a first name are
        // two clients, and the surname is what the user adds to tell them apart.
        string a = ClientService.ComputeClientKey("Joan García");
        string b = ClientService.ComputeClientKey("Joan Pérez");
        a.Should().NotBe(b);
    }

    [Fact] // D-04
    public void ClientKey_ignores_case_and_spacing()
    {
        string a = ClientService.ComputeClientKey("  joan   garcia ");
        string b = ClientService.ComputeClientKey("JoanGarcia");
        a.Should().Be(b);
    }

    [Fact] // D-05
    public async Task Saving_two_clients_with_the_same_name_is_rejected_even_on_another_phone()
    {
        await using var testDb = new TestDatabase();
        var clients = CreatesService(testDb);

        await clients.Create(new Client { Name = "Joan García", Mobile = "612345678" });

        // A different number is not a different client: the index refuses it. The dialog
        // is what stops the user ever reaching this (see ClientDialogViewModelTests).
        var action = async () => await clients.Create(
            new Client { Name = "Joan García", Mobile = "699999999" });
        await action.Should().ThrowAsync<Microsoft.EntityFrameworkCore.DbUpdateException>();
    }

    [Fact] // D-05b
    public async Task Two_clients_sharing_a_phone_but_not_a_name_are_both_saved()
    {
        await using var testDb = new TestDatabase();
        var clients = CreatesService(testDb);

        // A family on one number. This used to be refused, because the key was the first
        // name plus the phone and both of these keyed to "joan612345678".
        await clients.Create(new Client { Name = "Joan García", Mobile = "612345678" });
        await clients.Create(new Client { Name = "Joan Pérez", Mobile = "612345678" });

        (await clients.GetActive()).Should().HaveCount(2);
    }

    [Fact] // D-06
    public async Task FindByName_returns_the_existing_client()
    {
        await using var testDb = new TestDatabase();
        var clients = CreatesService(testDb);

        int id = await clients.Create(new Client { Name = "Joan García", Mobile = "612345678" });

        var existing = await clients.FindByName("joan  garcia");
        existing.Should().NotBeNull();
        existing!.Id.Should().Be(id);
    }

    [Fact] // D-07
    public async Task Changing_the_name_recomputes_the_client_key()
    {
        await using var testDb = new TestDatabase();
        var clients = CreatesService(testDb);

        int id = await clients.Create(new Client { Name = "Joan", Mobile = "612345678" });
        var client = await clients.GetById(id);
        client!.Name = "Joan García";
        await clients.Update(client);

        var updated = await clients.GetById(id);
        updated!.ClientKey.Should().Be(ClientService.ComputeClientKey("Joan García"));
    }

    [Fact]
    public async Task Sleeping_takes_the_client_out_of_the_active_list_without_deleting_them()
    {
        await using var testDb = new TestDatabase();
        var clients = CreatesService(testDb);

        int id = await clients.Create(new Client { Name = "Joan", Mobile = "612345678" });
        await clients.Sleep(id);

        (await clients.GetActive()).Should().BeEmpty();
        (await clients.GetAsleep()).Should().ContainSingle(c => c.Id == id);
    }

    [Fact]
    public async Task Waking_puts_the_client_back_in_the_active_list()
    {
        await using var testDb = new TestDatabase();
        var clients = CreatesService(testDb);

        int id = await clients.Create(new Client { Name = "Joan", Mobile = "612345678" });
        await clients.Sleep(id);
        await clients.Wake(id);

        (await clients.GetActive()).Should().ContainSingle(c => c.Id == id);
    }

    [Fact]
    public async Task Search_by_name_or_mobile()
    {
        await using var testDb = new TestDatabase();
        var clients = CreatesService(testDb);

        await clients.Create(new Client { Name = "Joan García", Mobile = "612345678" });
        await clients.Create(new Client { Name = "Maria Puig", Mobile = "699111222" });

        (await clients.Search("Joan")).Should().ContainSingle();
        (await clients.Search("699111")).Should().ContainSingle();
        (await clients.Search("inexistent")).Should().BeEmpty();
    }

    [Fact]
    public async Task Deleting_a_client_also_deletes_their_appointments_and_sales()
    {
        await using var testDb = new TestDatabase();
        await using (var db = testDb.Context())
        {
            var method = Make.Method();
            db.PaymentMethods.Add(method);
            var client = new Client { Name = "Joan", Mobile = "612345678" };
            db.Clients.Add(client);
            await db.SaveChangesAsync();

            db.Appointments.Add(new Appointment
            {
                ClientId = client.Id, Date = new DateOnly(2026, 1, 1),
                Time = new TimeOnly(10, 0), DurationMin = 30
            });
            db.Sales.Add(new Sale
            {
                ClientId = client.Id, Date = new DateOnly(2026, 1, 1), Time = new TimeOnly(10, 0),
                PaymentMethodId = method.Id, BaseCents = 100, VatCents = 21, TotalCents = 121,
                VatMode = VatMode.Included
            });
            await db.SaveChangesAsync();
        }

        var clients = CreatesService(testDb);
        var active = (await clients.GetActive()).Single();
        await clients.Delete(active.Id);

        await using var check = testDb.Context();
        (await check.Clients.CountAsync()).Should().Be(0);
        (await check.Appointments.CountAsync()).Should().Be(0);
        (await check.Sales.CountAsync()).Should().Be(0);
    }

}
