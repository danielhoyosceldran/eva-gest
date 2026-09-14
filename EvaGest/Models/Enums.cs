namespace EvaGest.Models;

/// <summary>State of an appointment. Only Completed can have an associated sale.</summary>
public enum AppointmentStatus
{
    Pending,
    Completed,
    Cancelled,
    NoShow
}

/// <summary>State of a sale. Cancelled sales stay visible but are excluded from all totals.</summary>
public enum SaleStatus
{
    Active,
    Voided
}

/// <summary>Whether catalogue prices already include VAT or not.</summary>
public enum VatMode
{
    Included,
    NotIncluded
}

/// <summary>Direction of a cash movement that is not a sale.</summary>
public enum MovementType
{
    In,
    Out
}

/// <summary>Day of the week, stored as a short text code (Mon, Tue, ...).</summary>
public enum Weekday
{
    Mon, Tue, Wed, Thu, Fri, Sat, Sun
}

/// <summary>Language of the interface. Stored as text, read once at startup: the app
/// loads one set of texts and one culture and keeps them for the whole session.</summary>
public enum Language
{
    Catalan,
    Spanish
}

/// <summary>Classification of a sale line, used for the per-worker reports (RF-16-D).</summary>
public enum LineType
{
    Service,
    Product,
    Other
}
