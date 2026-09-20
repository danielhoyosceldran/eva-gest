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

    /// <summary>
    /// The two files go to the same spreadsheet, so an amount has to be written the same
    /// way in both. The sales file used to go through Money.FormatExport — the interface
    /// culture, and "N2" with group separators — while the VAT file used the fixed
    /// culture and "F2": 1250,00 EUR came out as "1.250,00" in one and "1250,00" in the
    /// other.
    /// </summary>
    [Fact]
    public async Task Both_files_write_the_same_amount_the_same_way()
    {
        await using var testDb = new TestDatabase();
        int methodId = await AddsMethod(testDb);
        await using (var db = testDb.Context())
        {
            // Over 1000 EUR on purpose: below that, a group separator never shows up
            // and the two formats look identical.
            db.Sales.Add(Make.Sale(Today, methodId, SaleStatus.Active, Make.Line(125_000, 2100)));
            await db.SaveChangesAsync();
        }

        await new ExportService(new TestFactory(testDb.Options)).ExportSales(Today, Today, _folder);

        string period = $"{Today:yyyyMMdd}-{Today:yyyyMMdd}";
        string sales = await File.ReadAllTextAsync(Path.Combine(_folder, $"vendes_{period}.csv"));
        string vat = await File.ReadAllTextAsync(Path.Combine(_folder, $"iva_{period}.csv"));

        sales.Should().Contain("1250,00").And.NotContain("1.250,00");
        vat.Should().Contain("1250,00").And.NotContain("1.250,00");
    }

    /// <summary>
    /// Both supported languages happen to agree on how a number looks (comma decimal,
    /// dot grouping), so this cannot prove the export pins its own culture — swapping
    /// the fixed culture for AppLanguage.Culture would not move these bytes. What it
    /// does pin is that the figures are written identically in both sessions, including
    /// a fractional VAT rate; the N2-vs-F2 split that actually shipped is caught by
    /// <see cref="Both_files_write_the_same_amount_the_same_way"/>.
    /// </summary>
    [Fact]
    public async Task The_figures_are_written_the_same_way_in_either_language()
    {
        await using var testDb = new TestDatabase();
        int methodId = await AddsMethod(testDb);
        await using (var db = testDb.Context())
        {
            db.Sales.Add(Make.Sale(Today, methodId, SaleStatus.Active, Make.Line(125_000, 520)));
            await db.SaveChangesAsync();
        }

        var export = new ExportService(new TestFactory(testDb.Options));
        string period = $"{Today:yyyyMMdd}-{Today:yyyyMMdd}";

        // The header row is deliberately translated (CLAUDE.md: the CSV headers the
        // accountant gets come from the resx). It is the data rows that must not move.
        static async Task<string> DataRows(string path)
            => string.Join("|", (await File.ReadAllLinesAsync(path)).Skip(1));

        var written = new List<string>();
        foreach (var language in new[] { Language.Catalan, Language.Spanish })
        {
            AppLanguage.Use(language);
            await export.ExportSales(Today, Today, _folder);
            written.Add(await DataRows(Path.Combine(_folder, $"vendes_{period}.csv"))
                      + await DataRows(Path.Combine(_folder, $"iva_{period}.csv")));
        }

        AppLanguage.Use(Language.Catalan);   // this suite shares one process
        written[0].Should().Be(written[1], "the assessoria's figures are not part of the interface");
        written[0].Should().Contain("5,2 %", "including the rate, which is not always whole");
    }
}
