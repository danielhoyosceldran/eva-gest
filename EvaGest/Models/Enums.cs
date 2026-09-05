namespace EvaGest.Models;

/// <summary>State of an appointment. Only Realitzada can have an associated sale.</summary>
public enum EstatCita
{
    Pendent,
    Realitzada,
    Cancellada,
    NoAssistida
}

/// <summary>State of a sale. Cancelled sales stay visible but are excluded from all totals.</summary>
public enum EstatVenda
{
    Activa,
    Anullada
}

/// <summary>Whether catalogue prices already include VAT or not.</summary>
public enum IvaMode
{
    Inclos,
    NoInclos
}

/// <summary>Direction of a cash movement that is not a sale.</summary>
public enum TipusMoviment
{
    Entrada,
    Sortida
}

/// <summary>Day of the week, stored as a short text code (Dl, Dt, ...).</summary>
public enum DiaSetmana
{
    Dl, Dt, Dc, Dj, Dv, Ds, Dg
}

/// <summary>Classification of a sale line, used for the per-worker reports (RF-16-D).</summary>
public enum TipusLinia
{
    Servei,
    Producte,
    Altres
}
