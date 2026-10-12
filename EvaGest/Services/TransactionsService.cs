using System.Globalization;
using EvaGest.Data;
using EvaGest.Models;
using Microsoft.EntityFrameworkCore;

namespace EvaGest.Services;

/// <summary>
/// RF-29. The transactions table of the Reports page: sales and cash movements side by side,
/// filtered together. Read-only — every correction still goes through the Sales and
/// Till pages. Amounts come straight from the frozen values on each Sale and
/// CashMovement; lines are read only to name the concept and classify the category.
/// </summary>
public class TransactionsService(IDbContextFactory<ShopDbContext> factory) : ITransactionsService
{
    public async Task<List<TransactionEntry>> Search(TransactionFilter filter)
    {
        await using var db = await factory.CreateDbContextAsync();

        var entries = new List<TransactionEntry>();
        if (WantsSales(filter)) entries.AddRange(await SearchSales(db, filter));
        if (WantsMovements(filter)) entries.AddRange(await SearchMovements(db, filter));

        // Free text is matched here rather than in SQL: SQLite's LIKE ignores case only
        // for ASCII, so "àlex" would not find "Àlex", and accents are what Catalan names
        // are made of. The period bounds the list, so this stays small.
        if (!string.IsNullOrWhiteSpace(filter.Text))
        {
            string text = filter.Text.Trim();
            entries = entries.Where(e => Matches(e, text)).ToList();
        }

        // Newest first. A movement has no time of its own, so on a shared day it sorts
        // after the sales; the id keeps the order stable between reloads.
        return entries
            .OrderByDescending(e => e.Date)
            .ThenByDescending(e => e.Time ?? TimeOnly.MinValue)
            .ThenByDescending(e => e.Id)
            .ToList();
    }

    public async Task<TransactionFilterOptions> GetFilterOptions()
    {
        await using var db = await factory.CreateDbContextAsync();

        var clients = await db.Clients.AsNoTracking().OrderBy(c => c.Name).ToListAsync();
        var workers = await db.Workers.AsNoTracking().OrderBy(w => w.Name).ToListAsync();
        var methods = await db.PaymentMethods.AsNoTracking().OrderBy(m => m.Name).ToListAsync();
        var expenseCategories = await db.ExpenseCategories.AsNoTracking().OrderBy(c => c.Name).ToListAsync();

        // Product categories are free text on the product, so the list is whatever has
        // been typed, blanks left out. Distinct is done here, trimmed and ignoring case,
        // so "Ceres" and "ceres " are one entry.
        var productCategories = (await db.Products.AsNoTracking()
                .Where(p => p.Category != null && p.Category != "")
                .Select(p => p.Category!)
                .ToListAsync())
            .Select(c => c.Trim())
            .Where(c => c.Length > 0)
            .Distinct(StringComparer.CurrentCultureIgnoreCase)
            .Order(StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        return new TransactionFilterOptions(clients, workers, methods, productCategories, expenseCategories);
    }

    // ── Which side each filter allows ────────────────────────────────────────

    /// <summary>Sales are wanted unless the type says movements or the category is an expense one.</summary>
    private static bool WantsSales(TransactionFilter f)
        => f.Kind is null or TransactionKind.Sale
           && f.Category is null or { OnlySales: true };

    /// <summary>Movements are wanted unless the type says sales, a client is chosen (a
    /// movement has none) or the category is one only a sale can have.</summary>
    private static bool WantsMovements(TransactionFilter f)
        => f.Kind is null or TransactionKind.CashIn or TransactionKind.CashOut
           && f.ClientId is null
           && f.Category is null or { OnlySales: false };

    // ── Sales ────────────────────────────────────────────────────────────────

    private static async Task<List<TransactionEntry>> SearchSales(ShopDbContext db, TransactionFilter f)
    {
        var query = db.Sales.AsNoTracking()
            .Include(s => s.Client)
            .Include(s => s.Worker)
            .Include(s => s.PaymentMethod)
            .Include(s => s.Lines)
            .Where(s => s.Date >= f.From && s.Date <= f.To);

        if (!f.IncludeVoided) query = query.Where(s => s.Status == SaleStatus.Active);
        if (f.ClientId is int clientId) query = query.Where(s => s.ClientId == clientId);
        if (f.WorkerId is int workerId) query = query.Where(s => s.WorkerId == workerId);
        if (f.PaymentMethodId is int methodId) query = query.Where(s => s.PaymentMethodId == methodId);

        // A sale matches a category when ANY of its lines does: a haircut sold with a wax
        // is both a service sale and a product sale. The row still shows the whole sale
        // and its frozen total, never a share of it (see the class comment).
        if (f.Category is { Kind: TransactionCategoryKind.LineType, LineType: LineType type })
        {
            query = type switch
            {
                LineType.Service => query.Where(s => s.Lines.Any(l => l.ServiceId != null)),
                LineType.Product => query.Where(s => s.Lines.Any(l => l.ProductId != null)),
                _ => query.Where(s => s.Lines.Any(l => l.ServiceId == null && l.ProductId == null))
            };
        }
        else if (f.Category is { Kind: TransactionCategoryKind.ProductCategory, ProductCategory: string category })
        {
            // The products in that category are picked in memory, with the same trimmed,
            // case-insensitive comparison the option list was built with: SQLite's
            // lower() is ASCII-only, so "Àrees" and "àrees" would not compare equal there.
            var productIds = await ProductsInCategory(db, category);
            query = query.Where(s => s.Lines.Any(l => l.ProductId != null && productIds.Contains(l.ProductId.Value)));
        }

        var sales = await query.ToListAsync();

        return sales.Select(s => new TransactionEntry(
            Kind: TransactionKind.Sale,
            Id: s.Id,
            Date: s.Date,
            Time: s.Time,
            ClientName: s.DisplayName,
            WorkerName: s.Worker?.Name,
            Concept: string.Join(" + ", s.Lines.Select(l => l.Description)),
            ExpenseCategoryName: null,
            MethodName: s.PaymentMethod?.Name,
            BaseCents: s.BaseCents,
            VatCents: s.VatCents,
            TotalCents: s.TotalCents,
            Voided: s.Status == SaleStatus.Voided,
            Notes: s.Notes,
            Lines: s.Lines.OrderBy(l => l.Id)
                .Select(l => new TransactionLine(l.Description, l.Quantity, l.AmountCents, l.Type))
                .ToList())).ToList();
    }

    /// <summary>Ids of every product whose free-text category is <paramref name="category"/>,
    /// ignoring case and surrounding blanks.</summary>
    private static async Task<List<int>> ProductsInCategory(ShopDbContext db, string category)
    {
        string wanted = category.Trim();
        var products = await db.Products.AsNoTracking()
            .Where(p => p.Category != null)
            .Select(p => new { p.Id, p.Category })
            .ToListAsync();

        return products
            .Where(p => string.Equals(p.Category!.Trim(), wanted, StringComparison.CurrentCultureIgnoreCase))
            .Select(p => p.Id)
            .ToList();
    }

    // ── Cash movements ───────────────────────────────────────────────────────

    private static async Task<List<TransactionEntry>> SearchMovements(ShopDbContext db, TransactionFilter f)
    {
        var query = db.CashMovements.AsNoTracking()
            .Include(m => m.PaymentMethod)
            .Include(m => m.Category)
            .Include(m => m.Worker)
            .Where(m => m.Date >= f.From && m.Date <= f.To);

        if (!f.IncludeVoided) query = query.Where(m => m.Status == MovementStatus.Active);
        if (f.Kind == TransactionKind.CashIn) query = query.Where(m => m.Type == MovementType.In);
        if (f.Kind == TransactionKind.CashOut) query = query.Where(m => m.Type == MovementType.Out);
        if (f.WorkerId is int workerId) query = query.Where(m => m.WorkerId == workerId);
        if (f.PaymentMethodId is int methodId) query = query.Where(m => m.PaymentMethodId == methodId);
        if (f.Category is { ExpenseCategoryId: int categoryId }) query = query.Where(m => m.CategoryId == categoryId);

        var movements = await query.ToListAsync();

        return movements.Select(m => new TransactionEntry(
            Kind: m.Type == MovementType.In ? TransactionKind.CashIn : TransactionKind.CashOut,
            Id: m.Id,
            Date: m.Date,
            Time: null,
            ClientName: null,
            WorkerName: m.Worker?.Name,
            Concept: m.Concept,
            ExpenseCategoryName: m.Category?.Name,
            MethodName: m.PaymentMethod?.Name,
            BaseCents: m.BaseCents,
            VatCents: m.VatCents,
            TotalCents: m.AmountCents,
            Voided: m.Status == MovementStatus.Voided,
            Notes: m.Notes,
            Lines: [])).ToList();
    }

    // ── Free text ────────────────────────────────────────────────────────────

    /// <summary>
    /// True when <paramref name="text"/> appears in the client, worker, concept, any line
    /// or the notes of the entry, ignoring case and accents ("jose" finds "José").
    /// </summary>
    public static bool Matches(TransactionEntry entry, string text)
    {
        var compare = CultureInfo.InvariantCulture.CompareInfo;
        const CompareOptions options = CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace;

        bool Has(string? field) => field is not null && compare.IndexOf(field, text, options) >= 0;

        return Has(entry.ClientName) || Has(entry.WorkerName) || Has(entry.Concept)
               || Has(entry.ExpenseCategoryName) || Has(entry.Notes)
               || entry.Lines.Any(l => Has(l.Description));
    }
}

/// <summary>
/// The footer of the transactions table. Voided rows are listed but never counted,
/// like everywhere else in the app. Base and VAT are those of the sales only, as on the
/// Till page: a cash movement carries them only when its VAT was split (RF-13), and
/// mixing those in would make the figure mean something different from one period to
/// the next.
/// </summary>
public record TransactionTotals(
    int Count,
    long SalesCents,
    long CashInCents,
    long CashOutCents,
    long SalesBaseCents,
    long SalesVatCents)
{
    /// <summary>Money in: sales plus cash-ins.</summary>
    public long IncomeCents => SalesCents + CashInCents;

    /// <summary>Money in minus money out, the same balance the Till page shows.</summary>
    public long NetCents => SalesCents + CashInCents - CashOutCents;

    public static TransactionTotals From(IEnumerable<TransactionEntry> entries)
    {
        var active = entries.Where(e => !e.Voided).ToList();
        var sales = active.Where(e => e.Kind == TransactionKind.Sale).ToList();

        // Summed as long: a year of a busy shop is far from int.MaxValue cents, but the
        // sum must not depend on that (and Enumerable.Sum over int throws on overflow).
        return new TransactionTotals(
            Count: active.Count,
            SalesCents: sales.Sum(e => (long)e.TotalCents),
            CashInCents: active.Where(e => e.Kind == TransactionKind.CashIn).Sum(e => (long)e.TotalCents),
            CashOutCents: active.Where(e => e.Kind == TransactionKind.CashOut).Sum(e => (long)e.TotalCents),
            SalesBaseCents: sales.Sum(e => (long)(e.BaseCents ?? 0)),
            SalesVatCents: sales.Sum(e => (long)(e.VatCents ?? 0)));
    }
}
