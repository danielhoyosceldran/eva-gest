# Pla de proves automàtiques

## 1. Objectiu

Que després de qualsevol canvi al codi es pugui prémer un botó i saber, en segons, si els números segueixen quadrant. L'èmfasi és en **les operacions del dia a dia** i en **els càlculs de diners**, que són els que trencarien la comptabilitat sense avisar.

No es busca cobertura total. Es busca cobrir el que, si es trenca, la usuària no ho notaria fins que la gestoria li digués que l'IVA no quadra.

---

## 2. Tecnologia

| Peça | Elecció | Motiu |
|---|---|---|
| Marc de proves | **xUnit v3** (`xunit.v3` 3.2.2) | Estàndard de facto a .NET, part de la .NET Foundation, Apache 2.0 |
| Assercions | **AwesomeAssertions** 9.3.0 | Assercions llegibles (`.Should().Be(...)`), Apache 2.0 |
| Base de dades a les proves | **SQLite en memòria** | Motor SQLite real: aplica `CHECK`, claus foranes i índexs únics |
| Execució | Explorador de proves de Visual Studio i `dotnet test` | Sense configuració addicional |

### ⚠️ Per què NO FluentAssertions

FluentAssertions és la llibreria d'assercions més coneguda de .NET, però **a partir de la versió 8 va passar a una llicència comercial** (Xceed Community License). L'ús gratuït es limita a projectes personals i sense ànim de lucre, i exclou explícitament el programari usat *"per una entitat que cobra tarifes o obté ingressos"*.

Aquesta aplicació la faràs tu de franc, però l'usarà un negoci que factura. És una zona ambigua que no val la pena trepitjar per una llibreria d'assercions.

**AwesomeAssertions** és la bifurcació comunitària de FluentAssertions 7, manté Apache 2.0 i té la mateixa API: si algun dia canvies d'idea, només cal canviar el `using`.

### ⚠️ Per què NO el proveïdor InMemory d'EF Core

`Microsoft.EntityFrameworkCore.InMemory` és més ràpid de configurar, però **no és una base de dades relacional**: ignora les restriccions `CHECK`, no aplica les claus foranes i no fa complir els índexs únics.

Amb aquest proveïdor, les proves que verifiquen que no es pot guardar una cita amb client registrat *i* convidat alhora **passarien sempre**, encara que la restricció estigués mal escrita. Fent servir SQLite en memòria s'executa el mateix motor que en producció.

---

## 3. Projecte de proves

Clic dret a la solució → **Add → New Project → xUnit Test Project**, nom `BarberiaApp.Tests`.

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0-windows</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <!-- xUnit v3 test projects must be executables -->
    <OutputType>Exe</OutputType>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="xunit.v3" Version="3.2.2" />
    <PackageReference Include="xunit.runner.visualstudio" Version="3.1.0" />
    <PackageReference Include="AwesomeAssertions" Version="9.3.0" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="18.0.0" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\BarberiaApp\BarberiaApp.csproj" />
  </ItemGroup>

</Project>
```

> `TargetFramework` ha de ser `net10.0-windows` perquè el projecte principal és WPF.

---

## 4. Infraestructura de proves

### 4.1. Base de dades en memòria

```csharp
namespace BarberiaApp.Tests.Infra;

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using BarberiaApp.Data;

/// <summary>
/// A real SQLite engine held in memory. The connection must stay open for the whole
/// test: SQLite drops an in-memory database as soon as the last connection closes.
/// </summary>
public sealed class BaseDadesProva : IAsyncDisposable
{
    private readonly SqliteConnection _connexio;
    public DbContextOptions<BarberiaDbContext> Opcions { get; }

    public BaseDadesProva()
    {
        _connexio = new SqliteConnection("Data Source=:memory:");
        _connexio.Open();

        Opcions = new DbContextOptionsBuilder<BarberiaDbContext>()
            .UseSqlite(_connexio)
            .Options;

        using var db = new BarberiaDbContext(Opcions);
        db.Database.EnsureCreated();

        // Foreign keys are off by default in SQLite and must be enabled per connection
        using var cmd = _connexio.CreateCommand();
        cmd.CommandText = "PRAGMA foreign_keys = ON;";
        cmd.ExecuteNonQuery();
    }

    public BarberiaDbContext Context() => new(Opcions);

    public async ValueTask DisposeAsync()
    {
        await _connexio.DisposeAsync();
    }
}
```

### 4.2. Constructors de dades

Perquè cada prova no hagi de crear mitja base de dades per provar una cosa.

```csharp
namespace BarberiaApp.Tests.Infra;

using BarberiaApp.Models;

/// <summary>Fluent builders so each test states only what it actually cares about.</summary>
public static class Fes
{
    public static Client Client(string nom = "Joan García", string mobil = "612345678")
        => new() { Nom = nom, Mobil = mobil, ClientKey = $"{nom.ToLower()}{mobil}" };

    public static Servei Servei(string nom = "Tall", int preuCents = 1500,
                                int ivaBp = 2100, int? durada = 30)
        => new() { Nom = nom, PreuCents = preuCents, IvaBp = ivaBp,
                   DuradaMin = durada, Actiu = true };

    public static Producte Producte(string nom = "Cera", int preuCents = 900, int ivaBp = 2100)
        => new() { Nom = nom, PreuCents = preuCents, IvaBp = ivaBp, Actiu = true };

    public static Treballadora Treballadora(string nom = "Marta", bool activa = true)
        => new() { Nom = nom, Actiu = activa, Color = "#0F766E" };

    public static VendaLinia Linia(int importCents, int ivaBp = 2100, int quantitat = 1)
        => new() { Descripcio = "Prova", Quantitat = quantitat,
                   PreuUnitariCents = importCents / quantitat,
                   IvaBp = ivaBp, ImportCents = importCents };
}
```

---

## 5. Catàleg de proves

Cada prova té un identificador per poder-la referenciar. **154 proves** repartides en 13 blocs.

---

### Bloc A · Càlcul d'IVA (proves pures, sense base de dades)

El nucli del sistema. Si això falla, tot el que en depèn falla en silenci.

| Id | Prova | Resultat esperat |
|---|---|---|
| A-01 | Una línia de 15,00 € al 21% amb IVA inclòs | base 1240, quota 260, total 1500 |
| A-02 | Una línia de 15,00 € al 21% sense IVA inclòs | base 1500, quota 315, total 1815 |
| A-03 | Tres línies de 10,00 € al 21% inclòs | base 2479, quota 521 (**no** 2478/522) |
| A-04 | Tipus mixtos: 21% i 10% a la mateixa venda | Es calculen per separat i se sumen |
| A-05 | Venda d'import 0 | base 0, quota 0, sense excepció |
| A-06 | Import d'1 cèntim al 21% | base 1, quota 0 |
| A-07 | Tipus d'IVA 0% | base = total, quota 0 |
| A-08 | Recàrrec d'equivalència 5,2% (520 bp) | Es calcula sense pèrdua de precisió |
| A-09 | Import gran (1.000.000 €) | base 82644628, quota 17355372 |
| A-10 | Quantitat > 1 (3 unitats de 9,00 €) | import de línia = 2700 |
| A-11 | **Invariant** `base + iva == total` en 1.000 combinacions aleatòries | Es compleix sempre |
| A-12 | Arrodoniment de 0,5 cèntims | Cap amunt (`AwayFromZero`), mai bancari |
| A-13 | Ordre de les línies no afecta el resultat | Mateix resultat amb les línies barrejades |
| A-14 | `ARegistres` genera una fila per cada tipus present | 2 tipus → 2 files |
| A-15 | Suma dels registres == totals de la venda | Invariant entre taules |

---

### Bloc B · Regla canònica d'agregació

El problema que va aparèixer a l'anàlisi numèrica. Aquestes proves existeixen perquè ningú el reintrodueixi sense adonar-se'n.

| Id | Prova | Resultat esperat |
|---|---|---|
| B-01 | Total d'un període == suma de `Vendes.BaseCents` | Coincidència exacta |
| B-02 | Desglossament per tipus == suma de `VendaDesglossaments` | Coincidència exacta |
| B-03 | **1.000 vendes**: sumar guardats vs recalcular des de línies | Es documenta que divergeixen; el servei ha d'usar els guardats |
| B-04 | Les vendes anul·lades queden excloses del total | No sumen |
| B-05 | Les vendes anul·lades queden excloses del desglossament d'IVA | No sumen |
| B-06 | Un període sense vendes | Tots els totals a 0, sense excepció |
| B-07 | Agregació en `long`, no en `int` | Suma de 30.000.000 cèntims sense desbordament |

---

### Bloc C · Conversió i format de diners

| Id | Prova | Entrada → Sortida |
|---|---|---|
| C-01 | `TryParse("15")` | 1500 |
| C-02 | `TryParse("15,50")` | 1550 |
| C-03 | `TryParse("15.50")` | 1550 |
| C-04 | `TryParse("1.234,56")` | 123456 (el separador de milers va trencar la primera versió) |
| C-05 | `TryParse("  15,00 €  ")` | 1500 |
| C-06 | `TryParse("0,05")` | 5 |
| C-07 | `TryParse("abc")` i `TryParse("")` | `false` |
| C-08 | `Format(1500)` | "15,00 €" amb coma decimal |
| C-09 | `FormatExport(1500)` | "15,00" sense símbol |
| C-10 | Anada i tornada: `TryParse(Format(x)) == x` per a 500 valors | Sempre |
| C-11 | `Percentatges.Format(2100)` i `(520)` | "21 %" i "5,2 %" |

---

### Bloc D · Clau de client i duplicats

| Id | Prova | Resultat esperat |
|---|---|---|
| D-01 | "Joan" + "612345678" i "Joan" + "+34 612 345 678" | Mateixa `ClientKey` |
| D-02 | "joán" i "Joan" | Mateixa clau (accents normalitzats) |
| D-03 | "Joan García" i "Joan Pérez" amb el mateix telèfon | Mateixa clau (només compta el nom de pila) |
| D-04 | Telèfon amb prefix "0034" | Mateixos 9 dígits finals |
| D-05 | Guardar dos clients amb la mateixa clau | La base de dades ho rebutja (índex únic) |
| D-06 | `BuscarPossibleDuplicat` amb un client existent | El torna |
| D-07 | Canviar el telèfon d'un client | La `ClientKey` es recalcula |
| D-08 | Canviar el telèfon **no** trenca l'historial | Les cites i vendes segueixen lligades |

---

### Bloc E · Disponibilitat i solapament

Els 6 casos límit dels casos d'ús, més les vores del càlcul horari.

| Id | Prova | Resultat esperat |
|---|---|---|
| E-01 | 1 treballadora, cap cita a les 10:00 | Sense avís |
| E-02 | 1 treballadora, ja hi ha cita a les 10:00 | Avís de solapament |
| E-03 | 2 treballadores, 1 cita a les 10:00 | Sense avís |
| E-04 | 2 treballadores, 2 cites a les 10:00 | Avís |
| E-05 | 1 de vacances (inactiva), 1 cita | Avís |
| E-06 | Cita cancel·lada a la mateixa hora | Sense avís (no compta) |
| E-07 | Cita no assistida a la mateixa hora | Sense avís |
| E-08 | Cites consecutives sense encavalcar (10:00 30min + 10:30) | Sense avís |
| E-09 | Encavalcament parcial (10:00 45min + 10:30) | Avís |
| E-10 | Cita amb treballadora assignada, l'altra té una cita a la mateixa hora | Sense avís |
| E-11 | Editar una cita no la fa xocar amb ella mateixa | Sense avís |
| E-12 | Hora fora de l'horari de la barberia | Avís de fora d'horari |
| E-13 | Data a `DiesTancats` | Avís de dia tancat amb el motiu |
| E-14 | Cap avís bloqueja el desat | La cita es guarda igualment |
| E-15 | Càlcul del dilluns de la setmana per als 7 dies | Sempre torna el dilluns correcte |
| E-16 | El diumenge pertany a la setmana que comença el dilluns anterior | No salta a la següent |
| E-17 | Repartiment de les cites en 7 columnes | Cada cita cau al seu dia |
| E-18 | Navegació a la setmana anterior i següent | El rang es desplaça 7 dies |
| E-19 | Setmana sense cap cita | 7 columnes buides, cap excepció |
| E-20 | `ObtenirPerRang` inclou els dos extrems | Dilluns i diumenge inclosos |

---

### Bloc F · Cites i estats

| Id | Prova | Resultat esperat |
|---|---|---|
| F-01 | Cita nova neix en estat `Pendent` | Per defecte |
| F-02 | Marcar `Realitzada` | L'estat canvia |
| F-03 | Marcar `Cancellada` | L'estat canvia i no admet venda |
| F-04 | Marcar `NoAssistida` | L'estat canvia i no admet venda |
| F-05 | Intentar associar una venda a una cita cancel·lada | El servei ho rebutja |
| F-06 | Cita realitzada sense venda | És un estat vàlid, no un error |
| F-07 | La durada surt del servei si en té | 30 min per a "Tall" |
| F-08 | La durada surt del valor per defecte si el servei no en té | 30 min de la configuració |
| F-09 | Cita amb client convidat (només nom) | Es guarda |
| F-10 | Cita amb client registrat **i** nom de convidat | La base de dades ho rebutja |
| F-11 | Cita sense client ni nom de convidat | La base de dades ho rebutja |
| F-12 | Una cita no pot tenir dues vendes | Índex únic a `cita_id` |

---

### Bloc G · Vendes

| Id | Prova | Resultat esperat |
|---|---|---|
| G-01 | Venda independent amb un servei | Es guarda amb els totals correctes |
| G-02 | Venda des d'una cita | Client i servei precarregats |
| G-03 | Venda amb línia personalitzada (sense servei ni producte) | `Tipus` == `Altres` |
| G-04 | Modificar el preu d'una línia del catàleg | La venda guarda el preu modificat, el catàleg no canvia |
| G-05 | **Canviar el preu del catàleg després** | Les vendes anteriors mantenen el preu antic |
| G-06 | **Canviar l'IVA del catàleg després** | Les vendes anteriors mantenen l'IVA antic |
| G-07 | Canviar el mode d'IVA global | Les vendes anteriors mantenen el seu `IvaMode` |
| G-08 | Venda sense cap línia | El servei la rebutja |
| G-09 | Venda sense mètode de pagament | La rebutja |
| G-10 | Anul·lar una venda | Passa a `Anullada`, no s'esborra |
| G-11 | Una venda anul·lada segueix visible a l'historial | Hi és |
| G-12 | Editar una venda recalcula els totals | Base, IVA i total actualitzats |
| G-13 | Editar una venda regenera els desglossaments | Files antigues substituïdes |
| G-14 | Venda amb client convidat | Es guarda i compta als totals |
| G-15 | Quantitat 0 o negativa | La base de dades ho rebutja |
| G-16 | Import negatiu de línia | La base de dades ho rebutja |

---

### Bloc H · Caixa i balanç

| Id | Prova | Resultat esperat |
|---|---|---|
| H-01 | `Vendes + Entrades − Sortides = Balanç` | Coincideix |
| H-02 | Període "avui" amb 3 vendes i 1 sortida | Xifres exactes |
| H-03 | Períodes: ahir, setmana, mes, mes anterior | Cada un filtra bé |
| H-04 | Període personalitzat inclou els dos extrems | Dates de límit incloses |
| H-05 | Moviment sense IVA quan l'opció està desactivada | `BaseCents` i `IvaCents` nuls |
| H-06 | Moviment amb IVA quan l'opció està activada | Base i quota calculades i guardades |
| H-07 | Canviar l'opció d'IVA de caixa no altera moviments antics | Valors congelats |
| H-08 | Sortida resta al balanç, entrada suma | Signe correcte |
| H-09 | Desglossament d'IVA per tipus del període | Una fila per tipus present |

---

### Bloc I · Indicadors i informes

| Id | Prova | Resultat esperat |
|---|---|---|
| I-01 | Client amb 0 visites: mitjana per visita | `null` (la UI mostra `—`) |
| I-02 | Client amb 1 visita: freqüència | `null` (calen 2 per tenir interval) |
| I-03 | Client amb 2 visites separades 21 dies | Freqüència 21,0 |
| I-04 | Client amb 3 visites (21 i 21 dies) | Freqüència 21,0 |
| I-05 | Període sense vendes: % de treball | `null` |
| I-06 | Treballadora sense vendes: % de productes | `null` |
| I-07 | % de treball de 2 treballadores | Sumen 100 |
| I-08 | % serveis + % productes + % altres | Sumen 100 |
| I-09 | Les cites cancel·lades no compten com a visita | Excloses |
| I-10 | Les cites no assistides no compten com a visita | Excloses |
| I-11 | Comptadors separats de cancel·lades i no assistides | Cada un pel seu compte |
| I-12 | Clients adormits segueixen als rànquings | Hi apareixen |
| I-13 | Clients convidats **no** apareixen als rànquings | Exclosos |
| I-14 | Clients convidats **sí** compten als totals globals | Inclosos |
| I-15 | Detall per treballadora: serveis fets | Comptatge correcte |
| I-16 | Detall per treballadora: productes venuts | Unitats correctes |
| I-17 | Activitat per dia de la setmana | Agrupació correcta |
| I-18 | Client del mes | El de més despesa del mes en curs |

---

### Bloc J · Clients: adormir i eliminar

| Id | Prova | Resultat esperat |
|---|---|---|
| J-01 | Adormir un client | Desapareix de la cerca |
| J-02 | Un client adormit conserva l'historial | Cites i vendes intactes |
| J-03 | Despertar un client | Torna a la cerca |
| J-04 | Eliminar un client esborra les seves cites | Cascada |
| J-05 | Eliminar un client esborra les seves vendes | Cascada |
| J-06 | Eliminar esborra també les línies i desglossaments | Cascada en dos nivells |
| J-07 | `ComptarHistorial` abans d'eliminar | Xifres exactes per al missatge de confirmació |
| J-08 | Aniversaris d'avui | Només els que fan anys avui |
| J-09 | Aniversari el 29 de febrer en un any no de traspàs | No peta |

---

### Bloc K · Escenaris de dia complet

Aquest és el bloc que demanaves: **simula la jornada real i comprova que al final tot quadra.**

| Id | Escenari | Comprovacions |
|---|---|---|
| K-01 | **Dia normal.** 6 cites, 5 realitzades amb venda, 1 no assistida. Una entrada de 50 € i una sortida de 30 € | Balanç del dia, IVA, comptadors de cites, i que la no assistida no ha generat cap moviment |
| K-02 | **Dia amb correccions.** 4 vendes, s'edita la segona i s'anul·la la quarta | El balanç reflecteix l'edició i exclou l'anul·lada |
| K-03 | **Dia amb client convidat.** 3 vendes, una a un convidat | Compta al total, no genera fitxa ni apareix al rànquing |
| K-04 | **Dia amb dues treballadores.** 8 cites repartides | Els percentatges de cada una sumen 100 |
| K-05 | **Dia amb favors.** 2 vendes amb línia personalitzada a preu reduït | Els imports personalitzats es respecten i compten com a "Altres" |
| K-06 | **Setmana completa.** 5 dies com el K-01 | El total setmanal == suma dels 5 balanços diaris |
| K-07 | **Trimestre.** 60 dies de vendes | La base + IVA del trimestre == suma de les bases i IVA guardats |
| K-08 | **Tancament i reobertura.** Es tanca el context i es torna a obrir | Les xifres són idèntiques (persistència) |
| K-09 | **Dia sense activitat.** Cap cita ni venda | Tot a 0, cap excepció, cap divisió per zero |
| K-10 | **Dia amb tipus d'IVA mixtos.** Serveis al 21% i productes al 10% | El desglossament separa correctament els dos tipus |

Exemple d'implementació de K-01:

```csharp
[Fact]
public async Task Dia_normal_quadra_el_balanc_i_l_IVA()
{
    await using var bd = new BaseDadesProva();
    var dia = new DateOnly(2026, 9, 9);

    // Arrange: catalogue and one worker
    // ... seed with Fes.Servei(), Fes.Treballadora(), Fes.Client()

    // Act: five sales of 15.00 EUR at 21%, one no-show, plus 50 in and 30 out
    // ...

    // Assert
    var resum = await caixa.Resum(dia, dia);

    resum.VendesCents.Should().Be(7500);            // 5 x 1500
    resum.EntradesCents.Should().Be(5000);
    resum.SortidesCents.Should().Be(3000);
    resum.BalancCents.Should().Be(9500);            // 7500 + 5000 - 3000

    // The VAT breakdown must add back up exactly
    (resum.BaseCents + resum.IvaCents).Should().Be(resum.VendesCents);

    // The no-show generated no money movement
    var noAssistides = await cites.ComptarPerEstat(dia, EstatCita.NoAssistida);
    noAssistides.Should().Be(1);
}
```

---

### Bloc M · Pantalla d'Inici

Els comptadors que la usuària mira cada dia. Si es descuadren, s'ho creurà.

| Id | Prova | Resultat esperat |
|---|---|---|
| M-01 | Comptador de cites d'avui | Compta totes les del dia, sigui quin sigui l'estat |
| M-02 | Comptador de vendes d'avui | Només les actives, no les anul·lades |
| M-03 | Cobrat avui == suma dels totals de les vendes actives | Coincideix |
| M-04 | Balanç del dia == vendes + entrades − sortides | Coincideix amb `CaixaService.Resum` del mateix dia |
| M-05 | Dia sense activitat | Tot a 0, cap excepció |
| M-06 | Avís d'aniversari amb un client que fa anys avui | El mostra amb el nom |
| M-07 | Avís d'aniversari sense cap aniversari | No es mostra |
| M-08 | Client adormit que fa anys avui | No apareix a l'avís |

---

### Bloc L · Còpies de seguretat i exportació

| Id | Prova | Resultat esperat |
|---|---|---|
| L-01 | Còpia manual crea un fitxer | Existeix i té mida > 0 |
| L-02 | La còpia és restaurable | Les dades hi tornen a ser |
| L-03 | Retenció: amb 20 còpies i límit 15 | Queden les 15 més noves |
| L-04 | Còpia automàtica pendent des d'ahir | Es fa a l'arrencada |
| L-05 | Còpia automàtica ja feta avui | No se'n fa una altra |
| L-06 | Restaurar fa còpia prèvia de l'estat actual | El fitxer previ existeix |
| L-07 | Exportació: fila per venda, no per línia | Nombre de files == nombre de vendes |
| L-08 | Exportació: la suma de la columna base == base del període | Coincideix amb el que mostra l'app |
| L-09 | Exportació: fitxer d'IVA amb una fila per tipus | Correcte |
| L-10 | Exportació: format decimal amb coma | "15,00" |
| L-11 | Exportació d'un període buit | Fitxer amb capçalera i cap fila, sense error |

---

## 6. Convenció de noms

```csharp
// Pattern: What_Condition_ExpectedResult, written in Catalan to match the domain
[Fact] public void Venda_amb_tres_linies_iguals_agrupa_per_tipus_iva() { }
[Fact] public void Client_amb_una_sola_visita_no_te_frequencia() { }
[Fact] public void Cita_cancellada_no_compta_al_solapament() { }
```

Per a proves amb moltes variants, `[Theory]` amb `[InlineData]`:

```csharp
[Theory]
[InlineData("15", 1500)]
[InlineData("15,50", 1550)]
[InlineData("15.50", 1550)]
[InlineData("1.234,56", 123456)]
public void TryParse_accepta_els_formats_habituals(string entrada, int centimsEsperats)
{
    Diners.TryParse(entrada, out int cents).Should().BeTrue();
    cents.Should().Be(centimsEsperats);
}
```

---

## 7. Proves aleatòries d'invariants

Per als invariants crítics no n'hi ha prou amb casos triats a mà: convé llançar-hi milers de combinacions. Amb una llavor fixa, si una prova falla es pot reproduir exactament.

```csharp
[Fact]
public void Invariant_base_mes_iva_igual_total_es_compleix_sempre()
{
    // Fixed seed: a failure is always reproducible
    var random = new Random(20260909);
    int[] tipus = [2100, 1000, 400, 520, 0];

    for (int i = 0; i < 1000; i++)
    {
        var linies = Enumerable.Range(0, random.Next(1, 6))
            .Select(_ => Fes.Linia(random.Next(1, 50000), tipus[random.Next(tipus.Length)]))
            .ToList();

        foreach (var mode in new[] { IvaMode.Inclos, IvaMode.NoInclos })
        {
            var d = IvaCalculator.Calcular(linies, mode);
            (d.BaseCents + d.IvaCents).Should().Be(d.TotalCents,
                $"iteració {i}, mode {mode}");
        }
    }
}
```

---

## 8. Què NO cal provar

Escriure proves d'aquestes coses costa temps i no evita cap error real:

| No provar | Motiu |
|---|---|
| Vistes XAML i estils | Es verifiquen mirant la pantalla |
| Que EF Core guardi i llegeixi | És provar Microsoft, no el teu codi |
| Propietats sense lògica (`get`/`set`) | No hi ha res que pugui fallar |
| El servei de diàlegs | És una capa fina sobre `MessageBox` |
| Els textos exactes de la interfície | Canviaran sovint i trencarien proves sense motiu |

---

## 9. Ordre d'implementació

Segueix el mateix ordre que el codi de producció, escrivint les proves just després de cada servei:

```
1. Bloc A (IVA)        → abans que res: és la base de tot
2. Bloc C (diners)     → petit i ràpid, dona confiança
3. Bloc D (clients)    → amb el ClientService
4. Bloc E, F (cites)   → amb el DisponibilitatService
5. Bloc G (vendes)     → el nucli
6. Bloc B, H (caixa)   → agregacions
7. Bloc I (informes)
8. Bloc J (clients)
9. Bloc M (inici)      → els comptadors del dia
10. Bloc K (escenaris) → quan la resta funciona
11. Bloc L (backup)    → l'últim
```

**Els blocs A, B i C es poden escriure abans de tenir la base de dades**: són funcions pures i no necessiten res més.

---

## 10. Execució

| Com | Quan |
|---|---|
| Explorador de proves de Visual Studio | Mentre programes |
| `dotnet test` des del terminal | Abans de donar-li una versió nova a la teva amiga |
| `dotnet test --filter "FullyQualifiedName~IvaCalculator"` | Per executar només un bloc |

**Regla pràctica:** si toques qualsevol cosa relacionada amb diners, executa com a mínim els blocs A, B i K abans de donar-ho per bo.

---

## 11. Resum

| Bloc | Proves | Àrea |
|---|---|---|
| A | 15 | Càlcul d'IVA |
| B | 7 | Regla canònica d'agregació |
| C | 11 | Conversió i format de diners |
| D | 8 | Clau de client i duplicats |
| E | 20 | Disponibilitat, solapament i vista setmanal |
| F | 12 | Cites i estats |
| G | 16 | Vendes |
| H | 9 | Caixa i balanç |
| I | 18 | Indicadors i informes |
| J | 9 | Adormir i eliminar clients |
| K | 10 | Escenaris de dia complet |
| L | 11 | Còpies i exportació |
| M | 8 | Pantalla d'Inici |
| **Total** | **154** | |
