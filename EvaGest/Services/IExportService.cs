namespace EvaGest.Services;

public interface IExportService
{
    /// <summary>
    /// Writes two CSV files for the period: one row per SALE, and one row per VAT rate.
    ///
    /// Not one row per line: base and quota only exist per sale and per rate. Splitting
    /// a sale's base across its lines requires rounding and the parts do not add back
    /// up, so the export would not match what the app shows (6.8).
    /// </summary>
    Task ExportarVendes(DateOnly des, DateOnly fins, string carpetaDesti);
}
