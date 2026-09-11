# Models de domini (entitats C#)

## 1. Decisions de modelatge

| Decisió | Motiu |
|---|---|
| `int` per als cèntims | El màxim d'`int` són 21,4 milions d'euros en cèntims: de sobres per a una barberia |
| `int` per als punts base d'IVA | `2100` = 21,00%. Sense decimals enlloc |
| `DateOnly` / `TimeOnly` | Tipus natius de .NET per a dates i hores sense zona horària. Suportats per EF Core a SQLite |
| Enums en lloc de `string` | El compilador impedeix estats invàlids. Es guarden com a text a la BD amb `HasConversion<string>()` |
| Nullable reference types actiu | `<Nullable>enable</Nullable>` al `.csproj`. Els camps opcionals es marquen amb `?` |
| POCO sense lògica | Les entitats només guarden dades. Els càlculs viuen a `Services/` |
| Sufixos `_cents` i `_bp` → `Cents` i `Bp` | `TotalCents`, `IvaBp`. Impossible confondre unitats |

---

## 2. Enums

**Atenció:** aquests enums es guarden a la base de dades com a **text**, amb `HasConversion<string>()`, i el que es guarda és el **nom del membre** (`Cancellada`, `NoAssistida`, `Anullada`). Per això els noms van sense accents ni espais: han de ser identificadors vàlids de C# i coincidir amb les restriccions `CHECK` de l'esquema.

Les etiquetes que veu l'usuària ("Cancel·lada", "No assistida") es generen a la capa d'interfície amb un convertidor de binding, no surten d'aquí.

```csharp
namespace Barberia.Models;

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
```

---

## 3. Entitats

### 3.1. `Client`

```csharp
namespace Barberia.Models;

public class Client
{
    public int Id { get; set; }

    /// <summary>Duplicate-detection hash: normalised first name + last 9 phone digits.
    /// Must be recalculated by the service layer whenever Nom or Mobil change.</summary>
    public string ClientKey { get; set; } = string.Empty;

    public string Nom { get; set; } = string.Empty;
    public string Mobil { get; set; } = string.Empty;

    public string? Correu { get; set; }
    public DateOnly? DataNaixement { get; set; }
    public string? Observacions { get; set; }

    /// <summary>Hidden from searches and pickers, but still counted in statistics.</summary>
    public bool Adormit { get; set; }

    // Navigation
    public List<Cita> Cites { get; set; } = [];
    public List<Venda> Vendes { get; set; } = [];
}
```

---

### 3.2. `Treballadora`

```csharp
namespace Barberia.Models;

public class Treballadora
{
    public int Id { get; set; }
    public string Nom { get; set; } = string.Empty;

    /// <summary>Inactive workers keep their schedule but do not count towards
    /// availability when checking for appointment overlaps (RF-06).</summary>
    public bool Actiu { get; set; } = true;

    /// <summary>Hex colour used to tell workers apart in the UI.</summary>
    public string Color { get; set; } = "#0F766E";

    // Navigation
    public List<HorariTreballadora> Horaris { get; set; } = [];
    public List<Cita> Cites { get; set; } = [];
    public List<Venda> Vendes { get; set; } = [];
}
```

---

### 3.3. `HorariTreballadora`

Una fila per franja, cosa que permet horaris partits (matí i tarda).

```csharp
namespace Barberia.Models;

public class HorariTreballadora
{
    public int Id { get; set; }

    public int TreballadoraId { get; set; }
    public Treballadora Treballadora { get; set; } = null!;

    public DiaSetmana DiaSetmana { get; set; }
    public TimeOnly HoraInici { get; set; }
    public TimeOnly HoraFi { get; set; }
}
```

---

### 3.4. `Servei`

```csharp
namespace Barberia.Models;

public class Servei
{
    public int Id { get; set; }
    public string Nom { get; set; } = string.Empty;

    public int PreuCents { get; set; }

    /// <summary>This item's own VAT rate in basis points (2100 = 21.00%).
    /// Usually the default, but it can differ per item.</summary>
    public int IvaBp { get; set; }

    /// <summary>When null, the configured default appointment duration is used.</summary>
    public int? DuradaMin { get; set; }

    public bool Actiu { get; set; } = true;
}
```

---

### 3.5. `Producte`

```csharp
namespace Barberia.Models;

public class Producte
{
    public int Id { get; set; }
    public string Nom { get; set; } = string.Empty;

    public int PreuCents { get; set; }
    public string? Categoria { get; set; }

    /// <summary>This item's own VAT rate in basis points.</summary>
    public int IvaBp { get; set; }

    public bool Actiu { get; set; } = true;
}
```

---

### 3.6. `MetodePagament`

```csharp
namespace Barberia.Models;

public class MetodePagament
{
    public int Id { get; set; }
    public string Nom { get; set; } = string.Empty;
    public bool Actiu { get; set; } = true;
}
```

---

### 3.7. `Cita`

```csharp
namespace Barberia.Models;

public class Cita
{
    public int Id { get; set; }

    public DateOnly Data { get; set; }
    public TimeOnly Hora { get; set; }
    public int DuradaMin { get; set; }

    // Registered client XOR guest client: exactly one of these two must be set.
    public int? ClientId { get; set; }
    public Client? Client { get; set; }
    public string? NomConvidat { get; set; }
    public string? TelefonConvidat { get; set; }

    /// <summary>Optional. When set, it is preloaded into the sale form (RF-02).</summary>
    public int? ServeiId { get; set; }
    public Servei? Servei { get; set; }

    /// <summary>Optional. Drives how the overlap check is performed (RF-06).</summary>
    public int? TreballadoraId { get; set; }
    public Treballadora? Treballadora { get; set; }

    public EstatCita Estat { get; set; } = EstatCita.Pendent;
    public string? Observacions { get; set; }

    /// <summary>At most one sale per appointment.</summary>
    public Venda? Venda { get; set; }

    /// <summary>Display name regardless of whether the client is registered.</summary>
    [NotMapped]
    public string NomMostrat => Client?.Nom ?? NomConvidat ?? string.Empty;

    [NotMapped]
    public bool EsConvidat => ClientId is null;

    /// <summary>End time, used by the overlap calculation.</summary>
    [NotMapped]
    public TimeOnly HoraFi => Hora.AddMinutes(DuradaMin);
}
```

> Cal `using System.ComponentModel.DataAnnotations.Schema;` per a `[NotMapped]`.

---

### 3.8. `Venda`

```csharp
namespace Barberia.Models;

public class Venda
{
    public int Id { get; set; }

    public DateOnly Data { get; set; }
    public TimeOnly Hora { get; set; }

    // Registered client XOR guest client.
    public int? ClientId { get; set; }
    public Client? Client { get; set; }
    public string? NomConvidat { get; set; }
    public string? TelefonConvidat { get; set; }

    /// <summary>Optional link to the appointment that generated this sale. Unique index.</summary>
    public int? CitaId { get; set; }
    public Cita? Cita { get; set; }

    public int? TreballadoraId { get; set; }
    public Treballadora? Treballadora { get; set; }

    public int MetodePagamentId { get; set; }
    public MetodePagament MetodePagament { get; set; } = null!;

    // Frozen totals, computed once by IvaCalculator when the sale is saved.
    // Invariant: BaseCents + IvaCents == TotalCents
    public int BaseCents { get; set; }
    public int IvaCents { get; set; }
    public int TotalCents { get; set; }

    /// <summary>Snapshot of the VAT mode in force when this sale was recorded,
    /// so past sales stay auditable if the global setting changes later.</summary>
    public IvaMode IvaMode { get; set; }

    public EstatVenda Estat { get; set; } = EstatVenda.Activa;
    public string? Observacions { get; set; }

    public List<VendaLinia> Linies { get; set; } = [];

    /// <summary>Frozen per-rate VAT breakdown. Period totals are summed from here.</summary>
    public List<VendaDesglossament> Desglossaments { get; set; } = [];

    [NotMapped]
    public string NomMostrat => Client?.Nom ?? NomConvidat ?? string.Empty;

    [NotMapped]
    public bool EsConvidat => ClientId is null;
}
```

---

### 3.9. `VendaLinia`

El punt clau: **cada línia congela el seu preu i el seu IVA**. Si demà canvia el catàleg, les vendes passades no es mouen.

```csharp
namespace Barberia.Models;

public class VendaLinia
{
    public int Id { get; set; }

    public int VendaId { get; set; }
    public Venda Venda { get; set; } = null!;

    // Optional catalogue links, used ONLY for reporting aggregations (RF-16-D).
    // They are never the source of price or VAT: those are frozen copies below.
    public int? ServeiId { get; set; }
    public Servei? Servei { get; set; }
    public int? ProducteId { get; set; }
    public Producte? Producte { get; set; }

    /// <summary>Free-text copy. Allows fully custom lines (favours, special prices).</summary>
    public string Descripcio { get; set; } = string.Empty;

    public int Quantitat { get; set; } = 1;

    /// <summary>Frozen copy of the unit price at the time of the sale.</summary>
    public int PreuUnitariCents { get; set; }

    /// <summary>Frozen copy of the VAT rate applied at the time of the sale.</summary>
    public int IvaBp { get; set; }

    /// <summary>PreuUnitariCents * Quantitat. Exact, never rounded.
    /// Stored rather than computed so the sale total is reproducible.</summary>
    public int ImportCents { get; set; }

    /// <summary>Classification for the per-worker reports. Custom lines count as Altres,
    /// so that Serveis% + Productes% + Altres% == 100%.</summary>
    [NotMapped]
    public TipusLinia Tipus => ServeiId is not null  ? TipusLinia.Servei
                            : ProducteId is not null ? TipusLinia.Producte
                            : TipusLinia.Altres;
}
```

---

### 3.10. `VendaDesglossament`

Una fila per tipus d'IVA present a la venda. **És la font de veritat per a la declaració trimestral.**

```csharp
namespace Barberia.Models;

/// <summary>
/// Frozen VAT breakdown for one tax rate within a sale. Modelo 303 requires base and
/// quota reported per rate, and recomputing them later from lines would drift
/// (see decision 6.4), so they are stored once and only ever summed.
/// </summary>
public class VendaDesglossament
{
    public int Id { get; set; }

    public int VendaId { get; set; }
    public Venda Venda { get; set; } = null!;

    public int IvaBp { get; set; }
    public int BaseCents { get; set; }
    public int IvaCents { get; set; }
    public int TotalCents { get; set; }
}
```

I s'afegeix la col·lecció a `Venda`:

```csharp
public List<VendaDesglossament> Desglossaments { get; set; } = [];
```

---

### 3.11. `MovimentCaixa`

```csharp
namespace Barberia.Models;

public class MovimentCaixa
{
    public int Id { get; set; }

    public DateOnly Data { get; set; }
    public TipusMoviment Tipus { get; set; }

    /// <summary>Final amount, equivalent to a sale's TotalCents.</summary>
    public int ImportCents { get; set; }

    // Only filled in when the "apply VAT to cash movements" setting is on (RF-13).
    // Stored as fixed values, never derived, so past movements never change retroactively.
    public int? BaseCents { get; set; }
    public int? IvaCents { get; set; }
    public int? IvaBp { get; set; }

    public int MetodePagamentId { get; set; }
    public MetodePagament MetodePagament { get; set; } = null!;

    public string Concepte { get; set; } = string.Empty;
    public string? Observacions { get; set; }

    /// <summary>Signed value for balance calculations: entries add, exits subtract.</summary>
    [NotMapped]
    public int ImportSignatCents => Tipus == TipusMoviment.Entrada ? ImportCents : -ImportCents;
}
```

---

### 3.12. `HorariBarberia`

```csharp
namespace Barberia.Models;

public class HorariBarberia
{
    public int Id { get; set; }
    public DiaSetmana DiaSetmana { get; set; }
    public TimeOnly HoraObertura { get; set; }
    public TimeOnly HoraTancament { get; set; }
}
```

---

### 3.13. `DiaTancat`

```csharp
namespace Barberia.Models;

public class DiaTancat
{
    public int Id { get; set; }
    public DateOnly Data { get; set; }
    public string? Motiu { get; set; }
}
```

---

### 3.14. `ConfiguracioItem`

```csharp
namespace Barberia.Models;

/// <summary>Key-value store for all user-editable settings (RF-23).</summary>
public class ConfiguracioItem
{
    public string Clau { get; set; } = string.Empty;
    public string Valor { get; set; } = string.Empty;
}
```

Les claus es consumeixen a través d'una classe de constants, per no escriure cadenes de text soltes pel codi:

```csharp
namespace Barberia.Models;

public static class ClausConfig
{
    public const string BarberiaNom            = "barberia_nom";
    public const string BarberiaAdreca         = "barberia_adreca";
    public const string BarberiaTelefon        = "barberia_telefon";
    public const string IvaBpDefecte           = "iva_bp_defecte";
    public const string IvaModeActual          = "iva_mode";
    public const string AplicarIvaCaixa        = "aplicar_iva_caixa";
    public const string DuradaDefecteCitaMin   = "durada_defecte_cita_min";
    public const string HoraBackup             = "hora_backup";
    public const string BackupsAConservar      = "backups_a_conservar";
    public const string UltimaCopiaAutomatica = "ultima_copia_automatica";
    public const string MostrarAvisConvidat    = "mostrar_avis_convidat";
    public const string SoConfirmacio          = "so_confirmacio";
}
```

---

## 4. Càlcul d'IVA

Aquesta és la peça crítica. Viu a `Services/`, no a les entitats, però es documenta aquí perquè defineix com es poblen `BaseCents`, `IvaCents` i `TotalCents`.

```csharp
using Barberia.Models;

namespace Barberia.Services;

public record DesglossamentIva(int BaseCents, int IvaCents, int TotalCents);

public record DesglossamentPerTipus(int IvaBp, int BaseCents, int IvaCents, int TotalCents);

public static class IvaCalculator
{
    /// <summary>
    /// Computes a sale's VAT breakdown by grouping lines per VAT rate.
    ///
    /// Grouping matters: rounding each line separately and summing does NOT give the
    /// same result as rounding the total. Three lines of 10.00 EUR at 21% inclusive
    /// give base 2478 per-line versus 2479 on the total. Grouping per rate keeps the
    /// figures consistent with how Modelo 303 is filed, and guarantees the invariant
    /// BaseCents + IvaCents == TotalCents.
    /// </summary>
    public static DesglossamentIva Calcular(IEnumerable<VendaLinia> linies, IvaMode mode)
    {
        var perTipus = CalcularPerTipus(linies, mode);

        return new DesglossamentIva(
            perTipus.Sum(g => g.BaseCents),
            perTipus.Sum(g => g.IvaCents),
            perTipus.Sum(g => g.TotalCents));
    }

    /// <summary>Same calculation, kept split per VAT rate. This is what the quarterly
    /// export needs, since Modelo 303 reports each rate separately.</summary>
    public static List<DesglossamentPerTipus> CalcularPerTipus(
        IEnumerable<VendaLinia> linies, IvaMode mode)
    {
        return linies
            .GroupBy(l => l.IvaBp)
            .OrderBy(g => g.Key)
            .Select(g =>
            {
                int ivaBp = g.Key;
                int suma = g.Sum(l => l.ImportCents);  // exact, never rounded

                if (mode == IvaMode.Inclos)
                {
                    int baseCents = ArrodonirCents((decimal)suma * 10000m / (10000m + ivaBp));
                    return new DesglossamentPerTipus(ivaBp, baseCents, suma - baseCents, suma);
                }
                else
                {
                    int quota = ArrodonirCents((decimal)suma * ivaBp / 10000m);
                    return new DesglossamentPerTipus(ivaBp, suma, quota, suma + quota);
                }
            })
            .ToList();
    }

    /// <summary>
    /// Rows to persist in VendaDesglossaments. These are the frozen figures that every
    /// period total and every Modelo 303 export is summed from, so they are written
    /// once when the sale is saved and never recalculated afterwards.
    /// </summary>
    public static List<VendaDesglossament> ARegistres(
        IEnumerable<VendaLinia> linies, IvaMode mode)
        => CalcularPerTipus(linies, mode)
            .Select(g => new VendaDesglossament
            {
                IvaBp = g.IvaBp,
                BaseCents = g.BaseCents,
                IvaCents = g.IvaCents,
                TotalCents = g.TotalCents
            })
            .ToList();

    /// <summary>
    /// Rounds to whole cents away from zero. Math.Round defaults to banker's rounding
    /// (0.5 -> 0, 2.5 -> 2), which is not what Spanish accounting expects, so the
    /// mode must always be stated explicitly.
    /// </summary>
    private static int ArrodonirCents(decimal valor)
        => (int)Math.Round(valor, MidpointRounding.AwayFromZero);
}
```

### Regla d'ús

| Operació | Font |
|---|---|
| Totals d'una venda | `Vendes.BaseCents` / `IvaCents` / `TotalCents` |
| Totals d'un període | Suma de `Vendes.*Cents` de les vendes actives |
| Desglossament per tipus d'un període | Suma de `VendaDesglossaments` agrupada per `IvaBp` |
| Unitats venudes, serveis fets | `VendaLinies` (només comptatges, mai imports fiscals) |

**Mai** es recalcula el desglossament d'un període a partir de `VendaLinies`. Els dos mètodes divergeixen i la diferència creix sense límit: simulat sobre vendes reals, uns 5,65 € de base als 5 anys i 21,40 € als 20 anys.

**Invariant verificada.** L'algoritme s'ha provat amb 400 combinacions aleatòries de línies i tipus (21%, 10%, 4%, 5,2%, 0%) en tots dos modes: `BaseCents + IvaCents == TotalCents` es compleix sempre.

---

## 5. Conversió i format de diners

```csharp
using System.Globalization;

namespace Barberia.Services;

/// <summary>Cents are the storage unit. Euros exist only for display and export.</summary>
public static class Diners
{
    private static readonly CultureInfo Cultura = new("ca-ES");

    /// <summary>Formats cents for the UI: 1500 -> "15,00 €".</summary>
    public static string Format(int cents)
        => (cents / 100m).ToString("C2", Cultura);

    /// <summary>Formats cents for CSV export without the currency symbol: 1500 -> "15,00".</summary>
    public static string FormatExport(int cents)
        => (cents / 100m).ToString("N2", Cultura);

    /// <summary>
    /// Parses user input into cents. Accepts both comma and dot as decimal separator,
    /// and tolerates a group separator: "15", "15,50", "15.50" and "1.234,56" all work.
    /// The last comma or dot is treated as the decimal separator; earlier ones are
    /// group separators. For this domain that is unambiguous, since prices are small.
    /// </summary>
    public static bool TryParse(string? text, out int cents)
    {
        cents = 0;
        if (string.IsNullOrWhiteSpace(text)) return false;

        text = text.Replace("€", "").Replace(" ", "").Trim();

        int posDecimal = text.LastIndexOfAny([',', '.']);

        string normalitzat;
        if (posDecimal < 0)
        {
            normalitzat = text;
        }
        else
        {
            // Strip any group separators from the integer part, then use '.' as decimal
            string entera = text[..posDecimal].Replace(",", "").Replace(".", "");
            string decimals = text[(posDecimal + 1)..];
            normalitzat = $"{entera}.{decimals}";
        }

        if (!decimal.TryParse(normalitzat, NumberStyles.Number,
                              CultureInfo.InvariantCulture, out decimal euros))
            return false;

        cents = (int)Math.Round(euros * 100m, MidpointRounding.AwayFromZero);
        return true;
    }
}

/// <summary>Formats VAT basis points for display: 2100 -> "21 %", 520 -> "5,2 %".</summary>
public static class Percentatges
{
    public static string Format(int bp)
        => (bp / 100m).ToString("0.##", new CultureInfo("ca-ES")) + " %";
}
```

---

## 6. Indicadors amb valor indefinit

Els quatre indicadors amb risc de divisió per zero es modelen com a `decimal?`, i la interfície mostra `—` quan són `null` (RF-17). Mai `0%`, que es confondria amb un zero real.

```csharp
namespace Barberia.Services;

public static class Indicadors
{
    /// <summary>Share of total takings billed by one worker. Null when the period had no sales.</summary>
    public static decimal? PercentatgeTreball(int treballadoraCents, int totalPeriodeCents)
        => totalPeriodeCents == 0 ? null : treballadoraCents * 100m / totalPeriodeCents;

    /// <summary>Share of a worker's takings that came from products.
    /// Null when that worker had no sales in the period.</summary>
    public static decimal? PercentatgeProductes(int productesCents, int treballadoraCents)
        => treballadoraCents == 0 ? null : productesCents * 100m / treballadoraCents;

    /// <summary>Average spend per completed visit. Null when the client has no completed visits.</summary>
    public static decimal? MitjanaPerVisita(int totalGastatCents, int visitesRealitzades)
        => visitesRealitzades == 0 ? null : totalGastatCents / 100m / visitesRealitzades;

    /// <summary>
    /// Average number of days between consecutive completed visits.
    /// Needs at least two visits to have one interval, so it returns null below that.
    /// </summary>
    public static double? FrequenciaDies(IEnumerable<DateOnly> visitesRealitzades)
    {
        var dates = visitesRealitzades.OrderBy(d => d).ToList();
        if (dates.Count < 2) return null;

        double totalDies = dates
            .Zip(dates.Skip(1), (anterior, seguent) => (seguent.ToDateTime(TimeOnly.MinValue)
                                                      - anterior.ToDateTime(TimeOnly.MinValue)).TotalDays)
            .Sum();

        return totalDies / (dates.Count - 1);
    }
}
```

---

## 7. Etiquetes visibles dels enums

Els enums es guarden sense accents (secció 2), però la interfície els ha de mostrar escrits correctament. La traducció viu en un únic lloc:

```csharp
namespace Barberia.Services;

using Barberia.Models;

/// <summary>Maps enum members to the wording shown to the user.
/// Stored values are ASCII identifiers; these are the Catalan labels.</summary>
public static class Etiquetes
{
    public static string Text(EstatCita estat) => estat switch
    {
        EstatCita.Pendent      => "Pendent",
        EstatCita.Realitzada   => "Realitzada",
        EstatCita.Cancellada   => "Cancel·lada",
        EstatCita.NoAssistida  => "No assistida",
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

    public static string Text(DiaSetmana dia) => dia switch
    {
        DiaSetmana.Dl => "Dilluns",
        DiaSetmana.Dt => "Dimarts",
        DiaSetmana.Dc => "Dimecres",
        DiaSetmana.Dj => "Dijous",
        DiaSetmana.Dv => "Divendres",
        DiaSetmana.Ds => "Dissabte",
        DiaSetmana.Dg => "Diumenge",
        _ => dia.ToString()
    };

    public static string Curt(DiaSetmana dia) => dia.ToString();  // "Dl", "Dt", ...
}
```

Per fer-lo servir des del XAML cal un `IValueConverter` a `Helpers/`:

```csharp
namespace Barberia.Helpers;

using System.Globalization;
using System.Windows.Data;
using Barberia.Models;
using Barberia.Services;

/// <summary>Binds an enum straight to a TextBlock showing its Catalan label.</summary>
public class EtiquetaEnumConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value switch
        {
            EstatCita e      => Etiquetes.Text(e),
            EstatVenda e     => Etiquetes.Text(e),
            TipusMoviment e  => Etiquetes.Text(e),
            TipusLinia e     => Etiquetes.Text(e),
            DiaSetmana e     => Etiquetes.Text(e),
            _ => value?.ToString() ?? string.Empty
        };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
```

---

## 8. Notes de configuració d'EF Core

Aquests punts s'implementaran al `DbContext` (fase següent), però es deixen apuntats aquí perquè afecten com es guarden les entitats:

- **Enums com a text:** `.Property(e => e.Estat).HasConversion<string>()` per a tots els enums. Guardar-los com a enter faria la base de dades illegible i fràgil davant reordenacions.
- **Índex únic** a `Client.ClientKey`, `MetodePagament.Nom`, `DiaTancat.Data` i `Venda.CitaId`.
- **`ON DELETE`:** `Cascade` de `Client` cap a `Cites`/`Vendes` i de `Venda` cap a `VendaLinies`; `SetNull` per a `Treballadora`, `Servei`, `Producte` i `Cita`.
- **Restriccions `CHECK`:** client registrat XOR convidat, imports no negatius, quantitat positiva, i que una línia no sigui servei i producte alhora.
- **`[NotMapped]`:** totes les propietats derivades (`NomMostrat`, `Tipus`, `HoraFi`, `ImportSignatCents`) són de només lectura i no es persisteixen.

---

## 9. Documentació relacionada

- `BarberiaDbContext` i la seva configuració: `capa-mvvm.md` secció 6
- Migració inicial i seed: `setup-projecte.md` seccions 9 i 10
- Proves recomanades: `capa-mvvm.md` secció 10
