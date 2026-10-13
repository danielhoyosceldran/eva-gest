using EvaGest.Data;
using EvaGest.Services;
using EvaGest.ViewModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EvaGest;

/// <summary>
/// How the app is put together and how its database is made ready, kept out of
/// App.xaml.cs so the tests can run exactly the same steps against a fresh file. The
/// first start on a new install used to be the one path no test walked: every other test
/// builds its schema with EnsureCreated and wires its services by hand.
/// </summary>
public static class AppComposition
{
    /// <summary>Registers every service, page and the shell over the database at
    /// <paramref name="dbPath"/>. A test registers its own stand-ins afterwards (the last
    /// registration of a service is the one resolved).</summary>
    public static IServiceCollection Register(IServiceCollection s, string dbPath, string baseFolder)
    {
        // Short-lived contexts per operation: a long-lived one accumulates stale
        // tracked entities in a desktop app.
        s.AddDbContextFactory<ShopDbContext>(o => o
            .UseSqlite($"Data Source={dbPath}")
            .UseSnakeCaseNamingConvention());

        s.AddSingleton(new AppPaths(dbPath, baseFolder));
        s.AddSingleton<IBackupService, BackupService>();
        s.AddTransient<IExportService, ExportService>();
        s.AddSingleton<IDialogService, DialogService>();
        s.AddSingleton<IAppShutdown, AppShutdown>();
        s.AddTransient<ICatalogService, CatalogService>();
        s.AddTransient<IClientService, ClientService>();
        s.AddSingleton<IAppointmentChangeNotifier, AppointmentChangeNotifier>();
        s.AddTransient<IAppointmentService, AppointmentService>();
        s.AddTransient<IAvailabilityService, AvailabilityService>();
        s.AddTransient<IWorkerService, WorkerService>();
        s.AddTransient<ISaleService, SaleService>();
        s.AddTransient<ITillService, TillService>();
        s.AddTransient<IReportsService, ReportsService>();
        s.AddTransient<ITransactionsService, TransactionsService>();
        s.AddSingleton<ISoundService, SoundService>();
        // Singleton so the settings table is read once and kept in memory: the agenda
        // grid asks for the slot granularity on every week load.
        s.AddSingleton<ISettingsService, SettingsService>();
        s.AddTransient<ISeedService, SeedService>();
        // Singleton: one owner-mode state for the whole app. Built by hand so the
        // optional clock and iteration count keep their production defaults.
        s.AddSingleton<IOwnerAccessService>(sp =>
            new OwnerAccessService(sp.GetRequiredService<ISettingsService>()));

        s.AddSingleton<MainWindowViewModel>();
        s.AddTransient<ViewModels.Pages.HomeViewModel>();
        s.AddTransient<ViewModels.Pages.CatalogViewModel>();
        s.AddTransient<ViewModels.Pages.ClientsViewModel>();
        s.AddTransient<ViewModels.Pages.WorkersViewModel>();
        s.AddTransient<ViewModels.Pages.AgendaViewModel>();
        s.AddTransient<ViewModels.Pages.SalesViewModel>();
        s.AddTransient<ViewModels.Pages.TillViewModel>();
        s.AddTransient<ViewModels.Pages.ReportsViewModel>();
        s.AddTransient<ViewModels.Pages.SettingsViewModel>();

        return s;
    }

    /// <summary>
    /// Brings the database to the current schema and fills in what a working shop needs
    /// (CU-12): a copy of the old data first when migrations are pending, then the
    /// migrations, then the seed. On a first start there is no file yet, so no copy is
    /// taken and the migrations create it.
    /// </summary>
    /// <exception cref="BackupBeforeUpdateFailedException">The copy before migrating
    /// could not be written; nothing was migrated.</exception>
    public static async Task PrepareDatabase(IServiceProvider services)
    {
        var factory = services.GetRequiredService<IDbContextFactory<ShopDbContext>>();

        await using var db = await factory.CreateDbContextAsync();
        // A copy of the data as the previous version left it, before the schema moves.
        // Throws BackupBeforeUpdateFailedException, and so skips the migration, when the
        // copy cannot be written.
        await DatabaseUpdate.BackupBeforeMigrating(db,
            services.GetRequiredService<AppPaths>().DbPath,
            services.GetRequiredService<IBackupService>());
        await db.Database.MigrateAsync();

        // Runs after every migration, not only on a fresh file: later versions add
        // settings keys that an existing database still has to pick up (CU-12, step E).
        await services.GetRequiredService<ISeedService>().Seed();
    }
}
