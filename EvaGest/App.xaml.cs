using System.IO;
using System.Threading;
using System.Windows;
using EvaGest.Data;
using EvaGest.Models;
using EvaGest.Services;
using EvaGest.ViewModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using EvaGest.Resources;

namespace EvaGest;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    private static Mutex? _instance;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        Helpers.SmoothScroll.Activate();

        // Any exception that escapes a command handler would otherwise crash to the
        // default WPF dialog, which shows a raw stack trace (disseny-ui 9: "sense
        // disculpes ni tecnicismes"). Log the real cause, tell the user something plain.
        DispatcherUnhandledException += (_, args) =>
        {
            Log.Error(args.Exception, "Unhandled exception");
            MessageBox.Show(Texts.UnexpectedErrorMessage, Texts.UnexpectedErrorTitle,
                MessageBoxButton.OK, MessageBoxImage.Warning);
            args.Handled = true;
        };

        // Exceptions off the UI thread never reach DispatcherUnhandledException above and
        // would otherwise vanish (CLR terminates the process) or be silently lost (a
        // fire-and-forget Task whose exception nobody awaited). Log both before that happens.
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            Log.Fatal(args.ExceptionObject as Exception, "Unhandled exception (non-UI thread)");
            // The process is terminating right after this: OnExit will not run, so flush here.
            Log.CloseAndFlush();
        };
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            Log.Error(args.Exception, "Unobserved task exception");
            args.SetObserved();
        };

        // A named mutex stops a second instance from writing to the same SQLite file
        _instance = new Mutex(true, "EvaGest.SingleInstance", out bool isFirst);
        if (!isFirst)
        {
            Shutdown();
            return;
        }

        var config = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json")
            .Build();

        // Expand %LOCALAPPDATA% and make sure the folders exist on first run
        string dbPath = Environment.ExpandEnvironmentVariables(
            config["DatabasePath"] ?? throw new InvalidOperationException(
                "appsettings.json is missing DatabasePath"));

        string baseFolder = Path.GetDirectoryName(dbPath)!;
        Directory.CreateDirectory(baseFolder);
        Directory.CreateDirectory(Path.Combine(baseFolder, "Backups"));
        Directory.CreateDirectory(Path.Combine(baseFolder, "Logs"));

        Log.Logger = new LoggerConfiguration()
            .WriteTo.File(Path.Combine(baseFolder, "Logs", "log-.txt"),
                          rollingInterval: RollingInterval.Day,
                          retainedFileCountLimit: 30)
            .CreateLogger();

        Log.Information("Application started");

        Services = Configure(dbPath, baseFolder);

        // A corrupt or locked database must never surface as a raw exception (RF-18)
        try
        {
            await PrepareDatabase();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Could not open the database");
            MessageBox.Show(Texts.DatabaseUnreadableMessage, Texts.DatabaseUnreadableTitle,
                MessageBoxButton.OK, MessageBoxImage.Warning);
            Shutdown();
            return;
        }

        // Before the first window is built: every view resolves its texts through
        // {x:Static} as it loads, so the language has to be settled by now (RF-23).
        AppLanguage.Use(AppLanguage.Parse(
            await Services.GetRequiredService<ISettingsService>().Get(ConfigKeys.Language)));

        // The machine may have been off at the configured hour, so this is checked
        // at every startup instead of relying on a running timer (CU-11). A failure here
        // (locked/unwritable backup folder, full disk...) must not stop the app from
        // opening — the user still needs to work, just without today's automatic copy.
        try
        {
            await Services.GetRequiredService<IBackupService>().RunAutomaticBackupIfDue();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Automatic backup failed");
        }

        new MainWindow { DataContext = Services.GetRequiredService<MainWindowViewModel>() }.Show();
    }

    private static ServiceProvider Configure(string dbPath, string baseFolder)
    {
        var s = new ServiceCollection();

        // Short-lived contexts per operation: a long-lived one accumulates stale
        // tracked entities in a desktop app.
        s.AddDbContextFactory<ShopDbContext>(o => o
            .UseSqlite($"Data Source={dbPath}")
            .UseSnakeCaseNamingConvention());

        s.AddSingleton(new AppPaths(dbPath, baseFolder));
        s.AddSingleton<IBackupService, BackupService>();
        s.AddTransient<IExportService, ExportService>();
        s.AddSingleton<IDialogService, DialogService>();
        s.AddTransient<ICatalogService, CatalogService>();
        s.AddTransient<IClientService, ClientService>();
        s.AddTransient<IAppointmentService, AppointmentService>();
        s.AddTransient<IAvailabilityService, AvailabilityService>();
        s.AddTransient<IWorkerService, WorkerService>();
        s.AddTransient<ISaleService, SaleService>();
        s.AddTransient<ITillService, TillService>();
        s.AddTransient<IReportsService, ReportsService>();
        s.AddSingleton<ISoundService, SoundService>();
        // Singleton so the settings table is read once and kept in memory: the agenda
        // grid asks for the slot granularity on every week load.
        s.AddSingleton<ISettingsService, SettingsService>();
        s.AddTransient<ISeedService, SeedService>();

        s.AddSingleton<MainWindowViewModel>();
        s.AddTransient<EvaGest.ViewModels.Pages.HomeViewModel>();
        s.AddTransient<EvaGest.ViewModels.Pages.CatalogViewModel>();
        s.AddTransient<EvaGest.ViewModels.Pages.ClientsViewModel>();
        s.AddTransient<EvaGest.ViewModels.Pages.WorkersViewModel>();
        s.AddTransient<EvaGest.ViewModels.Pages.AgendaViewModel>();
        s.AddTransient<EvaGest.ViewModels.Pages.SalesViewModel>();
        s.AddTransient<EvaGest.ViewModels.Pages.TillViewModel>();
        s.AddTransient<EvaGest.ViewModels.Pages.ReportsViewModel>();
        s.AddTransient<EvaGest.ViewModels.Pages.SettingsViewModel>();

        return s.BuildServiceProvider();
    }

    private static async Task PrepareDatabase()
    {
        var factory = Services.GetRequiredService<IDbContextFactory<ShopDbContext>>();

        await using var db = await factory.CreateDbContextAsync();
        await db.Database.MigrateAsync();

        // Runs after every migration, not only on a fresh file: later versions add
        // settings keys that an existing database still has to pick up (CU-12, step E).
        await Services.GetRequiredService<ISeedService>().Seed();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Log.Information("Application closed");
        Log.CloseAndFlush();
        base.OnExit(e);
    }
}
