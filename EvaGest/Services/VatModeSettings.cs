using EvaGest.Models;

namespace EvaGest.Services;

/// <summary>
/// The VAT mode in force right now, read from the settings table.
///
/// One home for it, because two callers need the same answer and must never disagree:
/// <see cref="SaleService"/> reads it at save time to freeze it onto the sale, and
/// SaleDialogViewModel reads it to compute the live footer the user is looking at while
/// they type. They had a copy each; if the fallback in one had ever been changed, the
/// total on screen and the total written to the database would have parted company.
/// </summary>
public static class VatModeSettings
{
    /// <summary>Used only if the setting is missing or unreadable; matches the seeded
    /// default (esquema-bbdd 2.14).</summary>
    public const VatMode Fallback = VatMode.Included;

    public static async Task<VatMode> CurrentVatMode(this ISettingsService settings)
        => Enum.TryParse<VatMode>(await settings.Get(ConfigKeys.CurrentVatMode), out var mode)
            ? mode
            : Fallback;
}
