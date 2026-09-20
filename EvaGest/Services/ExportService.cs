using System.Globalization;
using System.IO;
using System.Text;
using EvaGest.Data;
using EvaGest.Models;
using EvaGest.Resources;
using Microsoft.EntityFrameworkCore;

using Serilog;

namespace EvaGest.Services;

/// <summary>
/// Phase 9. Writes the two CSV rows the assessoria needs for Modelo 303, straight
/// from the frozen Sales / SaleBreakdowns rows (casos-us CU-08).
/// </summary>
public class ExportService(IDbContextFactory<ShopDbContext> factory) : IExportService
{
    /// <summary>The accountant's spreadsheet expects a comma decimal separator, in
    /// either language, so the file format does not move with the interface.</summary>
    private static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("ca-ES");

    public async Task ExportSales(DateOnly from, DateOnly to, string destinationFolder)
    {
        Directory.CreateDirectory(destinationFolder);
        string period = $"{from:yyyyMMdd}-{to:yyyyMMdd}";

        await using var db = await factory.CreateDbContextAsync();

        var sales = await db.Sales.AsNoTracking()
            .Where(v => v.Status == SaleStatus.Active && v.Date >= from && v.Date <= to)
            .Include(v => v.Client)
            .Include(v => v.Worker)
            .Include(v => v.PaymentMethod)
            .Include(v => v.Lines)
            .OrderBy(v => v.Date).ThenBy(v => v.Time)
            .ToListAsync();

        await WriteSales(Path.Combine(destinationFolder, $"vendes_{period}.csv"), sales);

        var breakdowns = await db.SaleBreakdowns.AsNoTracking()
            .Where(d => d.Sale.Status == SaleStatus.Active && d.Sale.Date >= from && d.Sale.Date <= to)
            .GroupBy(d => d.VatBp)
            .Select(g => new
            {
                VatBp = g.Key,
                Base = g.Sum(x => (long)x.BaseCents),
                Vat = g.Sum(x => (long)x.VatCents),
                Total = g.Sum(x => (long)x.TotalCents)
            })
            .OrderBy(g => g.VatBp)
            .ToListAsync();

        await WriteVat(Path.Combine(destinationFolder, $"iva_{period}.csv"),
            breakdowns.Select(d => (d.VatBp, d.Base, d.Vat, d.Total)).ToList());

        // The file the accountant is given: worth being able to say afterwards which
        // period was exported, when, and how many sales it covered (CU-08).
        Log.Information("Exported {SaleCount} sales for {From}..{To} to {Folder}",
            sales.Count, from, to, destinationFolder);
    }

    private static async Task WriteSales(string path, List<Sale> sales)
    {
        var sb = new StringBuilder();
        sb.AppendLine(Texts.ExportSalesHeader);

        foreach (var v in sales)
        {
            string concept = string.Join(" + ", v.Lines.Select(l => l.Description));
            sb.AppendLine(string.Join(';',
                v.Date.ToString("dd/MM/yyyy"),
                v.Time.ToString("HH:mm"),
                Csv(v.DisplayName),
                Csv(v.Worker?.Name ?? ""),
                Csv(concept),
                Csv(v.PaymentMethod.Name),
                Money.FormatExport(v.BaseCents),
                Money.FormatExport(v.VatCents),
                Money.FormatExport(v.TotalCents)));
        }

        await File.WriteAllTextAsync(path, sb.ToString(), Encoding.UTF8);
    }

    private static async Task WriteVat(string path, List<(int vatBp, long baseCents, long vatCents, long totalCents)> rows)
    {
        var sb = new StringBuilder();
        sb.AppendLine(Texts.ExportVatHeader);

        foreach (var f in rows)
        {
            sb.AppendLine(string.Join(';',
                Percentages.Format(f.vatBp),
                (f.baseCents / 100m).ToString("F2", Culture),
                (f.vatCents / 100m).ToString("F2", Culture),
                (f.totalCents / 100m).ToString("F2", Culture)));
        }

        await File.WriteAllTextAsync(path, sb.ToString(), Encoding.UTF8);
    }

    /// <summary>Quotes a field only when it actually contains the separator, a quote
    /// or a newline — keeps the common case readable in a plain text editor.</summary>
    private static string Csv(string value)
        => value.Contains(';') || value.Contains('"') || value.Contains('\n')
            ? $"\"{value.Replace("\"", "\"\"")}\""
            : value;
}
