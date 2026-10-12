namespace EvaGest.Services;

public interface IExportService
{
    /// <summary>
    /// Writes two CSV rows for the period: one row per SALE, and one row per VAT rate.
    ///
    /// Not one row per line: base and quota only exist per sale and per rate. Splitting
    /// a sale's base across its lines requires rounding and the parts do not add back
    /// up, so the export would not match what the app shows (6.8).
    /// </summary>
    Task ExportSales(DateOnly from, DateOnly to, string destinationFolder);
    /// <summary>
    /// Writes the rows of the transactions table exactly as filtered on screen to
    /// transaccions_*.csv in <paramref name="destinationFolder"/>, one row per sale or
    /// movement, and returns the file's path. Money out is written negative, so a
    /// spreadsheet sum of the total column is the period's net; voided rows (only there
    /// when shown) are marked in the status column.
    /// </summary>
    Task<string> ExportTransactions(IReadOnlyList<TransactionEntry> entries,
                                    DateOnly from, DateOnly to, string destinationFolder);
}
