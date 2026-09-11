# Crear el projecte a Visual Studio

## 1. Requisits previs

| Necessari | Notes |
|---|---|
| Visual Studio 2026 | Amb la càrrega de treball **Desenvolupament d'escriptori .NET** |
| SDK de .NET 10 | Ve amb la càrrega de treball anterior |
| Eina `dotnet-ef` | S'instal·la al pas 4 |

Comprova que el SDK hi és, des d'un terminal:

```powershell
dotnet --list-sdks
```

Ha d'aparèixer una línia que comenci per `10.`.

---

## 2. Crear el projecte

1. Visual Studio → **Create a new project**
2. Cerca `WPF` i tria **WPF Application**

> ⚠️ **No** triïs "WPF App (**.NET Framework**)". És la plantilla antiga i no et deixarà seleccionar .NET 10.
>
> La correcta té les etiquetes `C#` · `Windows` · `Desktop` i **no** porta "(.NET Framework)" al nom.

3. Nom del projecte: `BarberiaApp`
4. Framework: **.NET 10.0 (Long Term Support)**
5. Crear

---

## 3. Configurar el `.csproj`

Obre el fitxer del projecte (doble clic sobre `BarberiaApp` a l'explorador de solucions) i deixa'l així:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net10.0-windows</TargetFramework>
    <UseWPF>true</UseWPF>

    <!-- Nullable reference types catch a whole class of bugs at compile time -->
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>

    <!-- Spanish/Catalan number formatting is used throughout -->
    <NeutralLanguage>ca-ES</NeutralLanguage>

    <ApplicationIcon>Resources\barberia.ico</ApplicationIcon>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.EntityFrameworkCore.Sqlite" Version="10.0.11" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.Design" Version="10.0.11">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers</IncludeAssets>
    </PackageReference>
    <PackageReference Include="Microsoft.Extensions.DependencyInjection" Version="10.0.11" />
    <PackageReference Include="Microsoft.Extensions.Configuration.Json" Version="10.0.11" />
    <PackageReference Include="CommunityToolkit.Mvvm" Version="8.4.2" />
    <PackageReference Include="Serilog" Version="4.3.1" />
    <PackageReference Include="Serilog.Sinks.File" Version="7.0.0" />
  </ItemGroup>

  <ItemGroup>
    <None Update="appsettings.json">
      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    </None>
  </ItemGroup>

</Project>
```

**Sobre les versions:** són les vigents al setembre de 2026. Els paquets de pegat es mouen sovint; si NuGet t'ofereix una `10.0.x` superior, agafa-la. El que no has de canviar és el **major**: tot ha de ser `10.x` per coincidir amb .NET 10.

Després de guardar, **Build → Rebuild Solution** perquè es restaurin els paquets.

---

## 4. Instal·lar l'eina de migracions

Un sol cop a la màquina:

```powershell
dotnet tool install --global dotnet-ef
```

Si ja la tenies d'abans, actualitza-la, perquè ha de ser de la mateixa generació que EF Core:

```powershell
dotnet tool update --global dotnet-ef
```

Comprova-ho:

```powershell
dotnet ef --version
```

---

## 5. Crear l'estructura de carpetes

A l'explorador de solucions, clic dret sobre el projecte → **Add → New Folder**, i crea:

```
BarberiaApp/
├── Models/
├── ViewModels/
│   ├── Pagines/
│   ├── Dialegs/
│   └── Elements/
├── Views/
│   ├── Pagines/
│   └── Dialegs/
├── Services/
├── Data/
├── Helpers/
└── Resources/
    └── Sons/
```

Després copia-hi el codi dels documents ja escrits:

| Des de | Cap a |
|---|---|
| `models-domini.md` seccions 2 i 3 | `Models/` — un fitxer per entitat, més `Enums.cs` |
| `models-domini.md` seccions 4, 5 i 6 | `Services/` — `IvaCalculator.cs`, `Diners.cs`, `Indicadors.cs` |
| `capa-mvvm.md` secció 6.1 | `Data/BarberiaDbContext.cs` |
| `disseny-ui.md` secció 4 | `Resources/` — els quatre `.xaml` |

---

## 6. `appsettings.json`

Clic dret al projecte → **Add → New Item → JSON File**, nom `appsettings.json`:

```json
{
  "RutaBaseDades": "%LOCALAPPDATA%\\BarberiaApp\\barberia.db"
}
```

Aquest és **l'únic** valor que viu fora de la base de dades (RF-23). La resta de configuració es guarda a la taula `Configuracio`.

Les carpetes de còpies i de registres es deriven d'aquesta ruta, així que no cal configurar-les:

```
%LOCALAPPDATA%\BarberiaApp\
├── barberia.db
├── Backups\
└── Logs\
```

---

## 7. Fàbrica de context per a temps de disseny

**Aquest pas evita un error que et bloquejaria una bona estona.** Quan executis `dotnet ef migrations add`, l'eina intenta construir el `DbContext` sense executar l'aplicació. Com que la nostra cadena de connexió surt de `appsettings.json` en temps d'execució, l'eina no la troba i falla amb un missatge poc clar (`Unable to create a DbContext...`).

La solució és una classe que l'eina troba automàticament. Crea `Data/DesignTimeDbContextFactory.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace BarberiaApp.Data;

/// <summary>
/// Used only by the `dotnet ef` tools at design time. The running application builds
/// its DbContext through dependency injection instead (see App.xaml.cs).
/// Without this class, `dotnet ef migrations add` cannot construct the context.
/// </summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<BarberiaDbContext>
{
    public BarberiaDbContext CreateDbContext(string[] args)
    {
        var opcions = new DbContextOptionsBuilder<BarberiaDbContext>()
            .UseSqlite("Data Source=disseny.db")   // never actually written to
            .Options;

        return new BarberiaDbContext(opcions);
    }
}
```

Perquè això funcioni, el `DbContext` necessita un constructor que accepti opcions. Afegeix-lo a `BarberiaDbContext`:

```csharp
public BarberiaDbContext(DbContextOptions<BarberiaDbContext> opcions) : base(opcions) { }
```

---

## 8. Arrencada de l'aplicació

Substitueix el contingut de `App.xaml.cs`. Això implementa el cas d'ús CU-12.

```csharp
using System.IO;
using System.Windows;
using BarberiaApp.Data;
using BarberiaApp.Services;
using BarberiaApp.ViewModels.Pagines;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace BarberiaApp;

public partial class App : Application
{
    public static IServiceProvider Serveis { get; private set; } = null!;

    private static Mutex? _instancia;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // A named mutex stops a second instance from writing to the same SQLite file
        _instancia = new Mutex(true, "BarberiaApp.UnicaInstancia", out bool esPrimera);
        if (!esPrimera)
        {
            Shutdown();
            return;
        }

        var config = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json")
            .Build();

        // Expand %LOCALAPPDATA% and make sure the folders exist on first run
        string rutaBd = Environment.ExpandEnvironmentVariables(
            config["RutaBaseDades"] ?? throw new InvalidOperationException(
                "Falta RutaBaseDades a appsettings.json"));

        string carpetaBase = Path.GetDirectoryName(rutaBd)!;
        Directory.CreateDirectory(carpetaBase);
        Directory.CreateDirectory(Path.Combine(carpetaBase, "Backups"));
        Directory.CreateDirectory(Path.Combine(carpetaBase, "Logs"));

        Log.Logger = new LoggerConfiguration()
            .WriteTo.File(Path.Combine(carpetaBase, "Logs", "log-.txt"),
                          rollingInterval: RollingInterval.Day,
                          retainedFileCountLimit: 30)
            .CreateLogger();

        Log.Information("Aplicació iniciada");

        Serveis = Configurar(rutaBd, carpetaBase);

        // A corrupt or locked database must never surface as a raw exception (RF-18)
        try
        {
            await PrepararBaseDades();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "No s'ha pogut obrir la base de dades");
            MessageBox.Show(
                "El fitxer de dades no es pot llegir. Pot ser que estigui malmès o que "
                + "s'hagi mogut de lloc.\n\nTens còpies de seguretat disponibles i pots "
                + "recuperar-ne una des de la carpeta:\n" + Path.Combine(carpetaBase, "Backups"),
                "No s'han pogut obrir les dades",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            Shutdown();
            return;
        }

        // Daily backup may be overdue if the machine was off at the configured hour
        await Serveis.GetRequiredService<IBackupService>().FerCopiaAutomaticaSiCal();

        new MainWindow { DataContext = Serveis.GetRequiredService<MainWindowViewModel>() }.Show();
    }

    private static ServiceProvider Configurar(string rutaBd, string carpetaBase)
    {
        var s = new ServiceCollection();

        // Short-lived contexts per operation: a long-lived one accumulates stale
        // tracked entities in a desktop app (see capa-mvvm section 6.2)
        s.AddDbContextFactory<BarberiaDbContext>(o => o.UseSqlite($"Data Source={rutaBd}"));

        s.AddSingleton(new RutesApp(rutaBd, carpetaBase));
        s.AddSingleton<IConfiguracioService, ConfiguracioService>();
        s.AddSingleton<IDialogService, DialogService>();
        s.AddSingleton<IBackupService, BackupService>();

        s.AddTransient<IClientService, ClientService>();
        s.AddTransient<ICitaService, CitaService>();
        s.AddTransient<IDisponibilitatService, DisponibilitatService>();
        s.AddTransient<IVendaService, VendaService>();
        s.AddTransient<ICaixaService, CaixaService>();
        s.AddTransient<ICatalegService, CatalegService>();
        s.AddTransient<ITreballadoraService, TreballadoraService>();
        s.AddTransient<IInformesService, InformesService>();
        s.AddTransient<IExportService, ExportService>();

        s.AddSingleton<MainWindowViewModel>();
        s.AddTransient<IniciViewModel>();
        // ... one registration per page and dialog ViewModel

        return s.BuildServiceProvider();
    }

    /// <summary>
    /// Applies pending migrations, backing up first so a failed migration is recoverable
    /// (decision 6.5). Seeds defaults on a brand-new database.
    /// </summary>
    private static async Task PrepararBaseDades()
    {
        var factory = Serveis.GetRequiredService<
            Microsoft.EntityFrameworkCore.IDbContextFactory<BarberiaDbContext>>();

        await using var db = await factory.CreateDbContextAsync();

        var pendents = (await db.Database.GetPendingMigrationsAsync()).ToList();
        bool esNova = !(await db.Database.CanConnectAsync());

        if (pendents.Count > 0 && !esNova)
        {
            Log.Information("Migracions pendents: {N}. Fent còpia preventiva.", pendents.Count);
            await Serveis.GetRequiredService<IBackupService>().FerCopiaManual();
        }

        await db.Database.MigrateAsync();

        var cfg = Serveis.GetRequiredService<IConfiguracioService>();
        await cfg.SeedInicial();
        await cfg.Carregar();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Log.Information("Aplicació tancada");
        Log.CloseAndFlush();
        base.OnExit(e);
    }
}

/// <summary>Resolved paths, injected so services never compute them again.</summary>
public record RutesApp(string BaseDades, string CarpetaBase)
{
    public string CarpetaBackups => Path.Combine(CarpetaBase, "Backups");
    public string CarpetaLogs => Path.Combine(CarpetaBase, "Logs");
}
```

I treu l'arrencada automàtica de la finestra de `App.xaml`, perquè ara la creem nosaltres:

```xml
<Application x:Class="BarberiaApp.App"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
    <!-- StartupUri removed on purpose: OnStartup creates the window after the
         database is ready, so the UI never binds to an unmigrated database -->
    <Application.Resources>
        <ResourceDictionary>
            <ResourceDictionary.MergedDictionaries>
                <ResourceDictionary Source="Resources/Colors.xaml" />
                <ResourceDictionary Source="Resources/Typography.xaml" />
                <ResourceDictionary Source="Resources/Metrics.xaml" />
                <ResourceDictionary Source="Resources/Controls.xaml" />
            </ResourceDictionary.MergedDictionaries>
        </ResourceDictionary>
    </Application.Resources>
</Application>
```

---

## 9. Generar la migració inicial

Un cop tinguis les entitats a `Models/` i el `BarberiaDbContext` compilant, des de la carpeta del projecte:

```powershell
dotnet ef migrations add InitialCreate
```

Això crea `Data/Migrations/` amb tres fitxers. **Obre el que acaba en `_InitialCreate.cs` i revisa'l** abans de continuar. Comprova concretament:

| Comprovació | Què has de veure |
|---|---|
| Imports com a enters | `preu_cents`, `total_cents`… han de ser `INTEGER`, mai `TEXT` |
| Percentatges com a enters | `iva_bp` ha de ser `INTEGER` |
| Enums com a text | `estat`, `iva_mode`, `tipus` han de ser `TEXT` |
| Valors dels enums | Si afegeixes restriccions `CHECK`, els valors han de ser els **noms dels membres** de l'enum (`Cancellada`, `NoAssistida`, `Anullada`), no les etiquetes amb accent que veu l'usuària |
| Índexs únics | A `client_key`, `nom` de mètodes, `data` de dies tancats i `cita_id` |
| Cascades | `Cascade` des de `Clients` i des de `Vendes`; `SetNull` per a catàleg i treballadores |

Si alguna cosa no quadra, corregeix el `DbContext` i regenera:

```powershell
dotnet ef migrations remove
dotnet ef migrations add InitialCreate
```

Les restriccions `CHECK` de l'esquema (secció 5) EF Core no les genera soles. S'afegeixen a mà dins del mètode `Up()` de la migració:

```csharp
migrationBuilder.Sql(@"
    -- Registered client XOR guest client
    CREATE TRIGGER IF NOT EXISTS trg_cites_client_xor
    BEFORE INSERT ON Cites
    WHEN NOT ((NEW.client_id IS NOT NULL AND NEW.nom_convidat IS NULL)
           OR (NEW.client_id IS NULL AND NEW.nom_convidat IS NOT NULL))
    BEGIN
        SELECT RAISE(ABORT, 'Cal un client registrat o un nom de convidat, no els dos');
    END;
");
```

> **Per què un trigger i no un `CHECK`:** SQLite no permet afegir una restricció `CHECK` a una taula ja creada amb `ALTER TABLE`. Amb EF Core, o es declara amb `HasCheckConstraint` dins de `OnModelCreating` (que sí funciona en crear la taula), o s'usa un trigger. Amb `HasCheckConstraint` és més net; el trigger és l'alternativa si cal afegir-ho més tard.

Versió preferida, al `DbContext`:

```csharp
b.Entity<Cita>().ToTable(t => t.HasCheckConstraint("ck_cites_client_xor",
    "(client_id IS NOT NULL AND nom_convidat IS NULL) OR " +
    "(client_id IS NULL AND nom_convidat IS NOT NULL)"));
```

---

## 10. Primera execució

No cal executar `dotnet ef database update`: el codi del pas 8 crida `MigrateAsync()` a l'arrencada, així que la base de dades es crea sola.

Prem **F5**. Si tot va bé:

1. Es crea `%LOCALAPPDATA%\BarberiaApp\` amb la base de dades i les dues subcarpetes
2. El registre `Logs\log-<data>.txt` conté "Aplicació iniciada"
3. S'obre la finestra principal

Per inspeccionar la base de dades, obre el fitxer `.db` amb **DB Browser for SQLite** (gratuït). Serveix per comprovar que les taules i els tipus són els esperats.

---

## 11. Crear el projecte de proves

Clic dret a la **solució** → **Add → New Project → xUnit Test Project**, nom `BarberiaApp.Tests`.

Deixa el seu `.csproj` així:

```xml
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
```

Dos punts que et faran perdre temps si no els saps:

- **`OutputType` ha de ser `Exe`.** xUnit v3 ho exigeix; amb el valor per defecte les proves no s'executen.
- **`TargetFramework` ha de ser `net10.0-windows`**, no `net10.0`, perquè el projecte referenciat és WPF.

El catàleg complet de proves a implementar és a `pla-proves.md`.

---

## 12. Ordre recomanat per començar a programar

No intentis muntar-ho tot alhora. Aquest ordre et deixa una cosa executable a cada pas:

```
1. Models + DbContext + migració         → la BD existeix
2. ConfiguracioService + seed             → l'app arrenca amb valors
3. Estils de Resources/                   → la finestra ja té l'aspecte definitiu
4. MainWindow + navegació lateral         → pots moure't entre pàgines buides
5. Proves dels blocs A, B i C             → funcions pures, sense BD
6. CatalegService + pantalla de catàleg   → CRUD senzill, per agafar el patró
7. ClientService + pantalla de clients    → afegeix cerca i ClientKey
8. CitaService + DisponibilitatService    → la part de lògica més densa
9. VendaService + diàleg de venda         → el nucli del negoci
10. CaixaService + pantalla de caixa
11. InformesService + informes
12. BackupService + ExportService
13. Ajuda i so
```

El pas 6 és a propòsit un CRUD avorrit: serveix per fixar el patró Vista → ViewModel → Servei una vegada, i després repetir-lo.

---

## 13. Errors freqüents

| Error | Causa i solució |
|---|---|
| `Unable to create a DbContext` en fer `migrations add` | Falta la `DesignTimeDbContextFactory` del pas 7 |
| `dotnet ef` no es reconeix | L'eina no està instal·lada, o cal reobrir el terminal després d'instal·lar-la |
| `The type or namespace 'ObservableObject' could not be found` | Falta `using CommunityToolkit.Mvvm.ComponentModel;` |
| `[ObservableProperty]` no genera res | La classe ha de ser `partial` |
| `NotMapped` no es reconeix | Falta `using System.ComponentModel.DataAnnotations.Schema;` |
| `sys:Double` no es resol al XAML | L'espai de noms a .NET 10 és `clr-namespace:System;assembly=System.Runtime`, no `mscorlib` |
| Els imports es guarden com a text a la BD | Alguna propietat és `decimal` en lloc de `int`. Han de ser tots enters en cèntims |
| La finestra surt en blanc | Has tret `StartupUri` però no crides `.Show()`, o el `DataContext` és nul |

---

## 14. Nota sobre `[ObservableProperty]`

`CommunityToolkit.Mvvm` 8.4.1 i posteriors funcionen amb C# 14, cosa que permet declarar-ho sobre **propietats parcials** en lloc de camps:

```csharp
// Newer style, available with .NET 10 / C# 14
public partial class ClientDialogViewModel : ObservableObject
{
    [ObservableProperty]
    public partial string Nom { get; set; }
}
```

```csharp
// Field style, used in capa-mvvm.md. Also works, generates a "Nom" property.
public partial class ClientDialogViewModel : ObservableObject
{
    [ObservableProperty]
    private string _nom = string.Empty;
}
```

Les dues compilen. Tria una i sigues-hi consistent a tot el projecte; els documents fan servir l'estil de camp.

---

## 15. Control de versions

Encara que el projecte sigui personal, val la pena un repositori Git local: et deixa tornar enrere quan una prova surt malament. Fitxer `.gitignore` a l'arrel:

```gitignore
bin/
obj/
.vs/
*.user
*.db
*.db-shm
*.db-wal
```

La base de dades queda fora del repositori a propòsit: conté dades reals de clients.
