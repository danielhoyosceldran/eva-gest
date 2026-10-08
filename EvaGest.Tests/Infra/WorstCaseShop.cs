using EvaGest.Models;
using EvaGest.Services;
using Xunit;

namespace EvaGest.Tests.Infra;

/// <summary>
/// A shop filled with the data that is hardest to lay out: long client, worker, service
/// and product names, thirty clients and sales, twenty appointments today, four-digit
/// amounts on the summary cards. Seeded once and shared by the layout tests, which only
/// read from it.
/// </summary>
public sealed class WorstCaseShop : IAsyncLifetime
{
    public const string LongName = "Maria del Carme Fernández-Villaverde i Puigdollers";

    public static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.Today);

    private readonly TestDatabase _db = new();

    public TestFactory Factory { get; }

    public WorstCaseShop() => Factory = new TestFactory(_db.Options);

    public async ValueTask InitializeAsync()
    {
        var settings = new SettingsService(Factory);
        await new SeedService(Factory, settings).Seed();

        await using var db = _db.Context();
        for (int d = 0; d < 7; d++)
            db.ShopSchedule.Add(new ShopSchedule
            {
                Weekday = (Weekday)d, OpeningTime = new TimeOnly(9, 0), ClosingTime = new TimeOnly(21, 0)
            });
        for (int i = 0; i < 8; i++)
            db.Workers.Add(new Worker { Name = $"Treballadora amb nom llarg {i}", Color = "#0F766E" });
        for (int i = 0; i < 6; i++)
            db.Services.Add(Make.Service($"Tall de cabell i arranjament de barba complet {i}"));
        for (int i = 0; i < 6; i++)
            db.Products.Add(Make.Product($"Cera fixadora mat d'alta resistència {i}"));

        var clients = Enumerable.Range(0, 30)
            .Select(i => Make.Client($"{LongName} {i}", $"6{i:00}345678")).ToList();
        db.Clients.AddRange(clients);
        await db.SaveChangesAsync();

        int methodId = db.PaymentMethods.First().Id;
        int serviceId = db.Services.First().Id;
        int workerId = db.Workers.First().Id;

        // Two appointments today for each of ten clients
        foreach (var client in clients.Take(10))
            for (int k = 0; k < 2; k++)
                db.Appointments.Add(new Appointment
                {
                    Date = Today, Time = new TimeOnly(9 + k * 6, 0), DurationMin = 45,
                    ClientId = client.Id, ServiceId = serviceId, WorkerId = workerId
                });

        // Thirty sales today: the day's totals run into four digits
        foreach (var client in clients)
        {
            var sale = Make.Sale(Today, methodId, SaleStatus.Active, Make.Line(12345, 2100), Make.Line(999, 1000));
            sale.GuestName = null;
            sale.ClientId = client.Id;
            db.Sales.Add(sale);
        }

        for (int i = 0; i < 15; i++)
            db.CashMovements.Add(new CashMovement
            {
                Date = Today, Type = i % 2 == 0 ? MovementType.In : MovementType.Out, AmountCents = 123456,
                Concept = "Compra de material de neteja i tovalloles " + i, PaymentMethodId = methodId
            });

        await db.SaveChangesAsync();
    }

    public ValueTask DisposeAsync() => _db.DisposeAsync();
}
