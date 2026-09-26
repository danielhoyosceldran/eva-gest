namespace EvaGest.Models;

/// <summary>Key-value store for all user-editable settings (RF-23).</summary>
public class SettingItem
{
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}

public static class ConfigKeys
{
    public const string ShopName                      = "shop_name";
    public const string ShopAddress                   = "shop_address";
    public const string ShopPhone                     = "shop_phone";
    public const string DefaultVatBp                  = "default_vat_bp";
    public const string CurrentVatMode                = "vat_mode";
    public const string ApplyVatToTill                = "apply_vat_to_till";
    public const string DefaultAppointmentDurationMin = "default_appointment_duration_min";
    public const string BackupTime                    = "backup_time";
    public const string BackupsToKeep                 = "backups_to_keep";
    public const string LastAutomaticBackup           = "last_automatic_backup";
    public const string ShowGuestNotice               = "show_guest_notice";
    public const string ConfirmationSound             = "confirmation_sound";

    /// <summary>Row granularity of the weekly agenda grid: 15, 30 or 60 minutes.</summary>
    public const string AgendaSlotMinutes             = "agenda_slot_minutes";

    /// <summary>Days the agenda grid shows: 3 (from any date) or 7 (Monday to Sunday).
    /// Missing means 3. Shared by the Agenda page and the appointment dialog's picker.</summary>
    public const string AgendaDays                    = "agenda_days";

    /// <summary>Interface language, a <see cref="Models.Language"/> member. Read once at
    /// startup, so a change only shows after restarting.</summary>
    public const string Language                      = "language";

    /// <summary>Salted hash of the owner's PIN (<see cref="Services.OwnerPin"/>). Missing
    /// until the owner creates one; never the PIN itself.</summary>
    public const string OwnerPin                      = "owner_pin";

    /// <summary>Salted hash of the paper recovery code that lets the owner choose a new
    /// PIN after forgetting it.</summary>
    public const string OwnerRecoveryCode             = "owner_recovery_code";
}
