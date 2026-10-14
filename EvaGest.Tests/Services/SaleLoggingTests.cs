using System.Collections.Concurrent;
using AwesomeAssertions;
using EvaGest.Models;
using EvaGest.Services;
using EvaGest.Tests.Infra;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Xunit;

namespace EvaGest.Tests.Services;

/// <summary>
/// B-42: a sale charged without an appointment (a counter sale) left no line in the log;
/// only a sale raised from an appointment did, as "Appointment N completed by sale M".
/// The log is part of the audit trail CU-04 asks for, and its edit and void lines name
/// sales it had never mentioned.
///
/// The app logs through the static Serilog logger, so the test swaps in its own for the
/// duration. It runs in the WPF collection, which holds the one test that checks the
/// static logger is left silent, so the two never overlap.
/// </summary>
[Collection(WpfCollection.Name)]
public class SaleLoggingTests
{
    private sealed class Captured : ILogEventSink
    {
        public ConcurrentQueue<LogEvent> Events { get; } = new();
        public void Emit(LogEvent logEvent) => Events.Enqueue(logEvent);
    }

    [Fact]
    public async Task A_counter_sale_is_logged_with_its_id_and_total()
    {
        await using var testDb = new TestDatabase();
        await using (var db = testDb.Context())
        {
            db.PaymentMethods.Add(Make.Method());
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
        int methodId;
        await using (var db = testDb.Context()) methodId = db.PaymentMethods.Single().Id;

        // An amount no other test charges, so a sale logged by a test running alongside
        // in another collection cannot be mistaken for this one.
        const int total = 98_765;
        var sale = new Sale
        {
            Date = new DateOnly(2026, 9, 7), Time = new TimeOnly(10, 0), GuestName = "Client de prova",
            PaymentMethodId = methodId
        };
        var line = new SaleLine
        {
            Description = "Prova", Quantity = 1, UnitPriceCents = total, VatBp = 2100, AmountCents = total
        };

        var sink = new Captured();
        var previous = Log.Logger;
        Log.Logger = new LoggerConfiguration().WriteTo.Sink(sink).CreateLogger();
        int id;
        try
        {
            id = await new SaleService(new TestFactory(testDb.Options), new TestSettings()).Create(sale, [line]);
        }
        finally
        {
            Log.Logger = previous;
        }

        sink.Events.Should().ContainSingle(e =>
            e.Level == LogEventLevel.Information
            && e.Properties.ContainsKey("SaleId") && e.Properties["SaleId"].ToString() == id.ToString()
            && e.Properties.ContainsKey("TotalCents") && e.Properties["TotalCents"].ToString() == total.ToString());
    }
}
