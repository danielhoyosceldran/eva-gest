using AwesomeAssertions;
using EvaGest.Models;
using EvaGest.Services;
using EvaGest.Tests.Infra;
using Xunit;

namespace EvaGest.Tests.Services;

/// <summary>Block L (exportació): CSV rows for the assessoria.</summary>
public class ExportServiceTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "EvaGestExportTests_" + Guid.NewGuid());
    private static readonly DateOnly Today = new(2026, 9, 7);

    public ExportServiceTests() => Directory.CreateDirectory(_folder);
    public void Dispose() => Directory.Delete(_folder, recursive: true);

    private static async Task<int> AddsMethod(TestDatabase testDb)
    {
        await using var db = testDb.Context();
        var m = Make.Method();
        db.PaymentMethods.Add(m);
        await db.SaveChangesAsync();
        return m.Id;
    }

    [Fact] // L-07
    public async Task One_row_per_sale_not_per_line()
    {
        await using var testDb = new TestDatabase();
        int methodId = await AddsMethod(testDb);
        await using (var db = testDb.Context())
        {
            db.Sales.Add(Make.Sale(Today, methodId, SaleStatus.Active, Make.Line(1000, 2100), Make.Line(900, 1000)));
            db.Sales.Add(Make.Sale(Today, methodId, SaleStatus.Active, Make.Line(500, 2100)));
            await db.SaveChangesAsync();
        }

        var export = new ExportService(new TestFactory(testDb.Options));
        await export.ExportSales(Today, Today, _folder);

        string content = await File.ReadAllTextAsync(Path.Combine(_folder, $"vendes_{Today:yyyyMMdd}-{Today:yyyyMMdd}.csv"));
        var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        lines.Should().HaveCount(3); // header + 2 sales, not 3 lines
    }

    [Fact] // L-08
    public async Task The_sum_of_the_base_column_equals_the_base_of_the_period()
    {
        await using var testDb = new TestDatabase();
        int methodId = await AddsMethod(testDb);
        await using (var db = testDb.Context())
        {
            db.Sales.Add(Make.Sale(Today, methodId, SaleStatus.Active, Make.Line(1500, 2100)));
            db.Sales.Add(Make.Sale(Today, methodId, SaleStatus.Active, Make.Line(900, 1000)));
            await db.SaveChangesAsync();
        }

        var till = new TillService(new TestFactory(testDb.Options));
        var summary = await till.Summary(Today, Today);

        var export = new ExportService(new TestFactory(testDb.Options));
        await export.ExportSales(Today, Today, _folder);

        var lines = (await File.ReadAllLinesAsync(
            Path.Combine(_folder, $"vendes_{Today:yyyyMMdd}-{Today:yyyyMMdd}.csv")))
            .Skip(1).Where(l => l.Length > 0);

        decimal sumBase = lines.Sum(l => decimal.Parse(l.Split(';')[6], System.Globalization.CultureInfo.GetCultureInfo("ca-ES")));
        sumBase.Should().Be(summary.BaseCents / 100m);
    }

    [Fact] // L-09
    public async Task The_vat_file_has_one_row_per_rate()
    {
        await using var testDb = new TestDatabase();
        int methodId = await AddsMethod(testDb);
        await using (var db = testDb.Context())
        {
            db.Sales.Add(Make.Sale(Today, methodId, SaleStatus.Active, Make.Line(1500, 2100)));
            db.Sales.Add(Make.Sale(Today, methodId, SaleStatus.Active, Make.Line(900, 1000)));
            await db.SaveChangesAsync();
        }

        var export = new ExportService(new TestFactory(testDb.Options));
        await export.ExportSales(Today, Today, _folder);

        var lines = await File.ReadAllLinesAsync(Path.Combine(_folder, $"iva_{Today:yyyyMMdd}-{Today:yyyyMMdd}.csv"));
        lines.Where(l => l.Length > 0).Should().HaveCount(3); // header + 2 rates
    }

    [Fact] // L-10
    public async Task Format_writes_the_decimal_with_a_comma()
    {
        await using var testDb = new TestDatabase();
        int methodId = await AddsMethod(testDb);
        await using (var db = testDb.Context())
        {
            db.Sales.Add(Make.Sale(Today, methodId, SaleStatus.Active, Make.Line(1500, 2100)));
            await db.SaveChangesAsync();
        }

        var export = new ExportService(new TestFactory(testDb.Options));
        await export.ExportSales(Today, Today, _folder);

        string content = await File.ReadAllTextAsync(Path.Combine(_folder, $"vendes_{Today:yyyyMMdd}-{Today:yyyyMMdd}.csv"));
        content.Should().Contain("15,00");
    }

    [Fact] // L-11
    public async Task Exporting_an_empty_period_does_not_crash()
    {
        await using var testDb = new TestDatabase();
        var export = new ExportService(new TestFactory(testDb.Options));

        await export.ExportSales(Today, Today, _folder);

        var lines = await File.ReadAllLinesAsync(Path.Combine(_folder, $"vendes_{Today:yyyyMMdd}-{Today:yyyyMMdd}.csv"));
        lines.Where(l => l.Length > 0).Should().ContainSingle(); // header only
    }
}
