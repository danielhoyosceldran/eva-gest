namespace EvaGest.Models;

/// <summary>Key-value store for all user-editable settings (RF-23).</summary>
public class ConfiguracioItem
{
    public string Clau { get; set; } = string.Empty;
    public string Valor { get; set; } = string.Empty;
}

public static class ClausConfig
{
    public const string BarberiaNom          = "barberia_nom";
    public const string BarberiaAdreca       = "barberia_adreca";
    public const string BarberiaTelefon      = "barberia_telefon";
    public const string IvaBpDefecte         = "iva_bp_defecte";
    public const string IvaModeActual        = "iva_mode";
    public const string AplicarIvaCaixa      = "aplicar_iva_caixa";
    public const string DuradaDefecteCitaMin = "durada_defecte_cita_min";
    public const string HoraBackup           = "hora_backup";
    public const string BackupsAConservar    = "backups_a_conservar";
    public const string UltimaCopiaAutomatica = "ultima_copia_automatica";
    public const string MostrarAvisConvidat  = "mostrar_avis_convidat";
    public const string SoConfirmacio        = "so_confirmacio";

    /// <summary>Row granularity of the weekly agenda grid: 15, 30 or 60 minutes.</summary>
    public const string MinutsSlotAgenda     = "minuts_slot_agenda";
}
