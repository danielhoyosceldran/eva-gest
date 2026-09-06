using System.IO;
using System.Threading;
using System.Windows;
using EvaGest.Data;
using EvaGest.Services;
using EvaGest.ViewModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace EvaGest;

public partial class App : Application
{
    public static IServiceProvider Serveis { get; private set; } = null!;

    private static Mutex? _instancia;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        Helpers.ScrollSuau.Activar();

        // Any exception that escapes a command handler would otherwise crash to the
        // default WPF dialog, which shows a raw stack trace (disseny-ui 9: "sense
        // disculpes ni tecnicismes"). Log the real cause, tell the user something plain.
        DispatcherUnhandledException += (_, args) =>
        {
            Log.Error(args.Exception, "Error inesperat no controlat");
            MessageBox.Show(
                "S'ha produït un error inesperat. L'operació no s'ha pogut completar, "
                + "però les dades ja guardades no s'han vist afectades.",
                "No s'ha pogut completar l'operació",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            args.Handled = true;
        };

        // A named mutex stops a second instance from writing to the same SQLite file
        _instancia = new Mutex(true, "EvaGest.UnicaInstancia", out bool esPrimera);
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
                + "s'hagi mogut de lloc.",
                "No s'han pogut obrir les dades",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            Shutdown();
            return;
        }

        // The machine may have been off at the configured hour, so this is checked
        // at every startup instead of relying on a running timer (CU-11).
        await Serveis.GetRequiredService<IBackupService>().FerCopiaAutomaticaSiCal();

        new MainWindow { DataContext = Serveis.GetRequiredService<MainWindowViewModel>() }.Show();
    }

    private static ServiceProvider Configurar(string rutaBd, string carpetaBase)
    {
        var s = new ServiceCollection();

        // Short-lived contexts per operation: a long-lived one accumulates stale
        // tracked entities in a desktop app.
        s.AddDbContextFactory<BarberiaDbContext>(o => o
            .UseSqlite($"Data Source={rutaBd}")
            .UseSnakeCaseNamingConvention());

        s.AddSingleton(new RutesApp(rutaBd, carpetaBase));
        s.AddSingleton<IBackupService, BackupService>();
        s.AddTransient<IExportService, ExportService>();
        s.AddSingleton<IDialogService, DialogService>();
        s.AddTransient<ICatalegService, CatalegService>();
        s.AddTransient<IClientService, ClientService>();
        s.AddTransient<ICitaService, CitaService>();
        s.AddTransient<IDisponibilitatService, DisponibilitatService>();
        s.AddTransient<ITreballadoraService, TreballadoraService>();
        s.AddTransient<IVendaService, VendaService>();
        s.AddTransient<ICaixaService, CaixaService>();
        s.AddTransient<IInformesService, InformesService>();
        s.AddSingleton<ISoundService, SoundService>();
        // Singleton so the settings table is read once and kept in memory: the agenda
        // grid asks for the slot granularity on every week load.
        s.AddSingleton<IConfiguracioService, ConfiguracioService>();
        s.AddTransient<ISeedService, SeedService>();

        s.AddSingleton<MainWindowViewModel>();
        s.AddTransient<EvaGest.ViewModels.Pagines.IniciViewModel>();
        s.AddTransient<EvaGest.ViewModels.Pagines.CatalegViewModel>();
        s.AddTransient<EvaGest.ViewModels.Pagines.ClientsViewModel>();
        s.AddTransient<EvaGest.ViewModels.Pagines.TreballadoresViewModel>();
        s.AddTransient<EvaGest.ViewModels.Pagines.AgendaViewModel>();
        s.AddTransient<EvaGest.ViewModels.Pagines.VendesViewModel>();
        s.AddTransient<EvaGest.ViewModels.Pagines.CaixaViewModel>();
        s.AddTransient<EvaGest.ViewModels.Pagines.InformesViewModel>();
        s.AddTransient<EvaGest.ViewModels.Pagines.ConfiguracioViewModel>();

        return s.BuildServiceProvider();
    }

    private static async Task PrepararBaseDades()
    {
        var factory = Serveis.GetRequiredService<IDbContextFactory<BarberiaDbContext>>();

        await using var db = await factory.CreateDbContextAsync();
        await db.Database.MigrateAsync();

        // Runs after every migration, not only on a fresh file: later versions add
        // settings keys that an existing database still has to pick up (CU-12, step E).
        await Serveis.GetRequiredService<ISeedService>().Sembrar();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Log.Information("Aplicació tancada");
        Log.CloseAndFlush();
        base.OnExit(e);
    }
}
