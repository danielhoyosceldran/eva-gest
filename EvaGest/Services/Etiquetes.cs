using EvaGest.Models;

namespace EvaGest.Services;

/// <summary>Maps enum members to the wording shown to the user.
/// Stored values are ASCII identifiers; these are the Catalan labels.</summary>
public static class Etiquetes
{
    public static string Text(EstatCita estat) => estat switch
    {
        EstatCita.Pendent     => "Pendent",
        EstatCita.Realitzada  => "Realitzada",
        EstatCita.Cancellada  => "Cancel·lada",
        EstatCita.NoAssistida => "No assistida",
        _ => estat.ToString()
    };

    public static string Text(EstatVenda estat) => estat switch
    {
        EstatVenda.Activa   => "Activa",
        EstatVenda.Anullada => "Anul·lada",
        _ => estat.ToString()
    };

    public static string Text(TipusMoviment tipus) => tipus switch
    {
        TipusMoviment.Entrada => "Entrada",
        TipusMoviment.Sortida => "Sortida",
        _ => tipus.ToString()
    };

    public static string Text(TipusLinia tipus) => tipus switch
    {
        TipusLinia.Servei   => "Servei",
        TipusLinia.Producte => "Producte",
        TipusLinia.Altres   => "Altres",
        _ => tipus.ToString()
    };
}
