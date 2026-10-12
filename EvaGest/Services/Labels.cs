using EvaGest.Models;
using EvaGest.Resources;

namespace EvaGest.Services;

/// <summary>Maps enum members to the wording shown to the user.
/// Stored values are ASCII identifiers; these are the translated labels.</summary>
public static class Labels
{
    public static string Text(AppointmentStatus status) => status switch
    {
        AppointmentStatus.Pending   => Texts.Pending,
        AppointmentStatus.Completed => Texts.Completed,
        AppointmentStatus.Cancelled => Texts.Cancelled,
        AppointmentStatus.NoShow    => Texts.NoShow,
        _ => status.ToString()
    };

    public static string Text(Weekday day) => day switch
    {
        Weekday.Mon => Texts.Monday,
        Weekday.Tue => Texts.Tuesday,
        Weekday.Wed => Texts.Wednesday,
        Weekday.Thu => Texts.Thursday,
        Weekday.Fri => Texts.Friday,
        Weekday.Sat => Texts.Saturday,
        Weekday.Sun => Texts.Sunday,
        _ => day.ToString()
    };

    /// <summary>Two-letter day code. The stored value is English (Mon, Tue, …), so the
    /// short form the user reads cannot simply be the enum member name.</summary>
    public static string TextShort(Weekday day) => day switch
    {
        Weekday.Mon => Texts.MondayShort,
        Weekday.Tue => Texts.TuesdayShort,
        Weekday.Wed => Texts.WednesdayShort,
        Weekday.Thu => Texts.ThursdayShort,
        Weekday.Fri => Texts.FridayShort,
        Weekday.Sat => Texts.SaturdayShort,
        Weekday.Sun => Texts.SundayShort,
        _ => day.ToString()
    };

    public static string Text(SaleStatus status) => status switch
    {
        SaleStatus.Active => Texts.Active,
        SaleStatus.Voided => Texts.Voided,
        _ => status.ToString()
    };

    public static string Text(MovementType type) => type switch
    {
        MovementType.In  => Texts.CashIn,
        MovementType.Out => Texts.CashOut,
        _ => type.ToString()
    };

    public static string Text(VatMode mode) => mode switch
    {
        VatMode.Included    => Texts.PricesVatIncluded,
        VatMode.NotIncluded => Texts.PricesVatExcluded,
        _ => mode.ToString()
    };

    public static string Text(Language language) => language switch
    {
        Language.Catalan => Texts.LanguageCatalan,
        Language.Spanish => Texts.LanguageSpanish,
        _ => language.ToString()
    };

    public static string Text(LineType type) => type switch
    {
        LineType.Service => Texts.Service,
        LineType.Product => Texts.Product,
        LineType.Other   => Texts.Other,
        _ => type.ToString()
    };

    public static string Text(TransactionKind kind) => kind switch
    {
        TransactionKind.Sale    => Texts.Sale,
        TransactionKind.CashIn  => Texts.CashIn,
        TransactionKind.CashOut => Texts.CashOut,
        _ => kind.ToString()
    };

    /// <summary>
    /// The category column of the transactions table, and of its CSV: the kinds of line
    /// a sale holds ("Servei + Producte"), or the expense category of a movement. A dash
    /// when there is nothing to say, so the cell is never mistaken for missing data.
    /// </summary>
    public static string Category(TransactionEntry entry)
    {
        if (entry.Kind == TransactionKind.Sale)
        {
            var types = entry.LineTypes.Select(Text).ToList();
            return types.Count > 0 ? string.Join(" + ", types) : "—";
        }

        return entry.ExpenseCategoryName ?? "—";
    }
}
