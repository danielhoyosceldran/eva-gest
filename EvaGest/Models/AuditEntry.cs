namespace EvaGest.Models;

/// <summary>
/// One change to an accounting record, kept in the database next to the record itself.
///
/// The log file rotates, and an edit used to overwrite the sale in place, so nothing
/// said what a ticket looked like before it was corrected. Each row here keeps the
/// record as it was and as it became, as JSON, so any past state can be read back and
/// the history travels inside every backup. Rows are only ever inserted.
/// </summary>
public class AuditEntry
{
    public int Id { get; set; }

    /// <summary>When the change was saved, in UTC so a DST change never reorders rows.</summary>
    public DateTime AtUtc { get; set; }

    /// <summary>What kind of record changed: "Sale", "CashMovement", "Client".</summary>
    public string Entity { get; set; } = string.Empty;

    public int EntityId { get; set; }

    /// <summary>"Update", "Void", "Delete"...</summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>The record before the change, as JSON; null when there was nothing before.</summary>
    public string? Before { get; set; }

    /// <summary>The record after the change, as JSON; null when nothing is left after.</summary>
    public string? After { get; set; }
}
