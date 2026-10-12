using EvaGest.Models;

namespace EvaGest.Services;

/// <summary>What a row of the transactions table is: a sale, or a cash movement in or out.</summary>
public enum TransactionKind
{
    Sale,
    CashIn,
    CashOut
}

/// <summary>Which of the three category families a <see cref="TransactionCategory"/> filters on.</summary>
public enum TransactionCategoryKind
{
    /// <summary>A sale with at least one line of this type: service, product or other concept.</summary>
    LineType,
    /// <summary>A sale with at least one product of this free-text product category ("Ceres").</summary>
    ProductCategory,
    /// <summary>A cash movement filed under this expense category ("Lloguer").</summary>
    ExpenseCategory
}

/// <summary>
/// One choice of the single "Category" filter. The three families share one box because
/// to the user they are one question — "what was it for?" — and each family only ever
/// matches one side: line types and product categories only sales, expense categories
/// only cash movements.
/// </summary>
public record TransactionCategory(
    TransactionCategoryKind Kind,
    LineType? LineType = null,
    string? ProductCategory = null,
    int? ExpenseCategoryId = null)
{
    public static TransactionCategory Of(LineType type) => new(TransactionCategoryKind.LineType, LineType: type);
    public static TransactionCategory OfProducts(string category) => new(TransactionCategoryKind.ProductCategory, ProductCategory: category);
    public static TransactionCategory OfExpense(int categoryId) => new(TransactionCategoryKind.ExpenseCategory, ExpenseCategoryId: categoryId);

    /// <summary>True when this category can only ever match a sale.</summary>
    public bool OnlySales => Kind != TransactionCategoryKind.ExpenseCategory;
}

/// <summary>
/// Every filter of the transactions table. The period is required: an open-ended query
/// over the shop's whole history is never what the table is for, and it keeps every
/// query bounded. Null on any other filter means "all".
/// </summary>
public record TransactionFilter(
    DateOnly From, DateOnly To,
    TransactionKind? Kind = null,
    int? ClientId = null,
    int? WorkerId = null,
    int? PaymentMethodId = null,
    TransactionCategory? Category = null,
    string? Text = null,
    bool IncludeVoided = false);

/// <summary>One line of a sale, as the row's detail shows it.</summary>
public record TransactionLine(string Description, int Quantity, int AmountCents, LineType Type);

/// <summary>
/// One row of the transactions table: a sale or a cash movement flattened to the same
/// shape, with no EF entity behind it. Amounts are the values frozen on the record when
/// it was saved — a sale's totals are never recomputed from its lines (canonical rule).
/// </summary>
public record TransactionEntry(
    TransactionKind Kind,
    int Id,
    DateOnly Date,
    TimeOnly? Time,             // null on a cash movement: it has a date only
    string? ClientName,         // null on a cash movement
    string? WorkerName,
    string Concept,
    string? ExpenseCategoryName,
    string? MethodName,         // null once the payment method was deleted (F-04)
    int? BaseCents,             // null on a cash movement whose VAT was not split (RF-13)
    int? VatCents,
    int TotalCents,             // always positive; see SignedTotalCents
    bool Voided,
    string? Notes,
    IReadOnlyList<TransactionLine> Lines)
{
    /// <summary>The total as it moves the balance: money out is negative.</summary>
    public long SignedTotalCents => Kind == TransactionKind.CashOut ? -(long)TotalCents : TotalCents;

    /// <summary>Distinct line types of a sale, in Service, Product, Other order. Empty on a movement.</summary>
    public IEnumerable<LineType> LineTypes => Lines.Select(l => l.Type).Distinct().Order();
}

/// <summary>Everything the filter boxes offer, read in one go.</summary>
public record TransactionFilterOptions(
    List<Client> Clients,
    List<Worker> Workers,
    List<PaymentMethod> Methods,
    List<string> ProductCategories,
    List<ExpenseCategory> ExpenseCategories);

public interface ITransactionsService
{
    /// <summary>Sales and cash movements of the period that pass every filter, newest first.</summary>
    Task<List<TransactionEntry>> Search(TransactionFilter filter);

    /// <summary>Every client, worker, payment method and category, deactivated ones
    /// included: a past transaction may well point at one of them.</summary>
    Task<TransactionFilterOptions> GetFilterOptions();
}
