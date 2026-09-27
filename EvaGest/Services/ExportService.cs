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

    /// <summary>
    /// The one way an amount is written to either CSV. The sales file used to go through
    /// Money.FormatExport, which formats with AppLanguage.Culture and "N2": that both
    /// ignored the fixed culture above and added group separators, so the same 1250,00 EUR
    /// came out as "1.250,00" in vendes_*.csv and "1250,00" in iva_*.csv — one export,
    /// two number formats, from files the assessoria imports side by side.
    ///
    /// "F2" and not "N2": no group separator, so the field never depends on the importer
    /// being told which character to ignore.
    /// </summary>
    private static string Amount(long cents)
        => (cents / 100m).ToString("F2", Culture);

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

        // Cash movements whose VAT was split (RF-13). Their base and quota were stored
        // but never reached any export, so VAT charged on a cash-in, or paid on a
        // cash-out, was invisible to the accountant. Kept in a file of their own: a
        // cash-in is output VAT like a sale, a cash-out is input VAT, and mixing either
        // into iva_*.csv would change what that file has always meant.
        var movementVat = (await db.CashMovements.AsNoTracking()
            .Where(m => m.Status == MovementStatus.Active && m.VatBp != null
                     && m.Date >= from && m.Date <= to)
            .Select(m => new { m.Type, m.VatBp, m.BaseCents, m.VatCents, m.AmountCents })
            .ToListAsync())
            .GroupBy(m => (m.Type, VatBp: m.VatBp!.Value))
            .OrderBy(g => g.Key.Type).ThenBy(g => g.Key.VatBp)
            .Select(g => (g.Key.Type, g.Key.VatBp,
                          Base: g.Sum(m => (long)(m.BaseCents ?? 0)),
                          Vat: g.Sum(m => (long)(m.VatCents ?? 0)),
                          Total: g.Sum(m => (long)m.AmountCents)))
            .ToList();

        // Only written when there is something in it: most shops never switch the
        // split on, and an empty extra file would only raise questions.
        if (movementVat.Count > 0)
            await WriteMovementVat(Path.Combine(destinationFolder, $"iva_caixa_{period}.csv"), movementVat);

        // The file the accountant is given: worth being able to say afterwards which
        // period was exported, when, and how many sales it covered (CU-08).
        Log.Information("Exported {SaleCount} sales and {MovementVatRows} cash-movement VAT rows for {From}..{To} to {Folder}",
            sales.Count, movementVat.Count, from, to, destinationFolder);
    }

    private static async Task WriteMovementVat(string path,
        List<(MovementType type, int vatBp, long baseCents, long vatCents, long totalCents)> rows)
    {
        var sb = new StringBuilder();
        sb.AppendLine(Texts.ExportMovementsVatHeader);

        foreach (var r in rows)
        {
            sb.AppendLine(string.Join(';',
                r.type == MovementType.In ? Texts.CashIn : Texts.CashOut,
                Percentages.Format(r.vatBp, Culture),
                Amount(r.baseCents),
                Amount(r.vatCents),
                Amount(r.totalCents)));
        }

        await File.WriteAllTextAsync(path, sb.ToString(), Encoding.UTF8);
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
                Amount(v.BaseCents),
                Amount(v.VatCents),
                Amount(v.TotalCents)));
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
                Percentages.Format(f.vatBp, Culture),
                Amount(f.baseCents),
                Amount(f.vatCents),
                Amount(f.totalCents)));
        }

        await File.WriteAllTextAsync(path, sb.ToString(), Encoding.UTF8);
    }

    /// <summary>
    /// Makes a free-text field safe to put in the CSV.
    ///
    /// A field starting with = + - @ (or a tab or carriage return) is read by Excel as a
    /// formula, so a client or concept named "=HYPERLINK(...)" ran as one on the
    /// accountant's machine. Such a field gets a leading apostrophe, which spreadsheets
    /// treat as "this is text" and do not show. Amounts never pass through here.
    ///
    /// Then the field is quoted only when it contains the separator, a quote or a line
    /// break, which keeps the common case readable in a plain text editor.
    /// </summary>
    private static string Csv(string value)
    {
        if (value.Length > 0 && value[0] is '=' or '+' or '-' or '@' or '\t' or '\r')
            value = "'" + value;

        return value.Contains(';') || value.Contains('"') || value.Contains('\n') || value.Contains('\r')
            ? $"\"{value.Replace("\"", "\"\"")}\""
            : value;
    }
}
