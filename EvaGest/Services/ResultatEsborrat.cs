namespace EvaGest.Services;

/// <summary>
/// What a delete request actually did. Anything the history already points at is
/// deactivated instead of removed, so past appointments and sales keep reading the
/// way they were recorded (RF-10). The ViewModels report which of the two happened,
/// because "Eliminar" that silently deactivates would look like a bug.
/// </summary>
public enum ResultatEsborrat
{
    /// <summary>Physically removed: nothing in the history referenced it.</summary>
    Eliminat,

    /// <summary>Kept but deactivated, because the history references it. It stops
    /// appearing in the pickers for new appointments and sales.</summary>
    Desactivat,

    /// <summary>Nothing done: removing it would leave the app unable to work.</summary>
    Bloquejat
}
