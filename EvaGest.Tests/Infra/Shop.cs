using EvaGest.Models;
using EvaGest.Services;
using EvaGest.ViewModels;
using EvaGest.ViewModels.Pages;

namespace EvaGest.Tests.Infra;

/// <summary>
/// The whole app over one in-memory database, for the scenario tests: every service, every
/// page and the shell, wired as the container wires them, with the owner unlocked and a
/// <see cref="ScriptedUser"/> answering the dialogs. Each page is the shell's own instance,
/// so state carried between visits is exercised the way the user meets it.
///
/// The catalogue helpers write through the services rather than the dialogs: they are the
/// set-up of a story, not what it is about.
/// </summary>
public sealed class Shop : IAsyncDisposable
{
    public TestDatabase Db { get; } = new();
    public ScriptedUser User { get; } = new();

    public TestFactory Factory { get; }
    public SettingsService Settings { get; }
    public AppointmentService Appointments { get; }
    public SaleService Sales { get; }
    public TillService Till { get; }
    public ClientService Clients { get; }
    public CatalogService Catalog { get; }
    public WorkerService Workers { get; }
    public ReportsService Reports { get; }
    public AvailabilityService Availability { get; }

    public MainWindowViewModel Shell { get; private set; } = null!;
    public HomeViewModel Home { get; private set; } = null!;
    public AgendaViewModel Agenda { get; private set; } = null!;
    public ClientsViewModel ClientsPage { get; private set; } = null!;
    public WorkersViewModel WorkersPage { get; private set; } = null!;
    public CatalogViewModel CatalogPage { get; private set; } = null!;
    public SalesViewModel SalesPage { get; private set; } = null!;
    public TillViewModel TillPage { get; private set; } = null!;
    public ReportsViewModel ReportsPage { get; private set; } = null!;
    public SettingsViewModel SettingsPage { get; private set; } = null!;

    /// <summary>Shared by the services and the shell, as the container shares it, so a charge
    /// or a status change reaches the overdue notice at once.</summary>
    private readonly AppointmentChangeNotifier _notifier = new();

    public static DateOnly Today => DateOnly.FromDateTime(DateTime.Today);

    private Shop()
    {
        Factory = new TestFactory(Db.Options);
        Settings = new SettingsService(Factory);
        Appointments = new AppointmentService(Factory, _notifier);
        Sales = new SaleService(Factory, Settings, _notifier);
        Till = new TillService(Factory);
        Clients = new ClientService(Factory);
        Catalog = new CatalogService(Factory);
        Workers = new WorkerService(Factory);
        Reports = new ReportsService(Factory);
        Availability = new AvailabilityService(Factory);
    }

    /// <summary>A seeded, empty shop with the owner's PIN made and owner mode open.</summary>
    public static async Task<Shop> Open()
    {
        var shop = new Shop();
        await new SeedService(shop.Factory, shop.Settings).Seed();

        var owner = await TestOwner.Unlocked();
        var sound = new TestSoundService();
        var export = new ExportService(shop.Factory);
        var user = shop.User;

        shop.Home = new HomeViewModel(shop.Appointments, shop.Sales, shop.Till, shop.Clients,
            shop.Availability, shop.Catalog, shop.Workers, shop.Settings, sound, user);
        shop.Agenda = new AgendaViewModel(shop.Appointments, shop.Sales, shop.Availability, shop.Clients,
            shop.Catalog, shop.Workers, shop.Settings, sound, user);
        shop.ClientsPage = new ClientsViewModel(shop.Clients, shop.Reports, user, owner);
        shop.WorkersPage = new WorkersViewModel(shop.Workers, user);
        shop.CatalogPage = new CatalogViewModel(shop.Catalog, shop.Settings, user);
        shop.SalesPage = new SalesViewModel(shop.Sales, shop.Clients, shop.Catalog, shop.Workers,
            sound, shop.Settings, export, user);
        shop.TillPage = new TillViewModel(shop.Till, shop.Catalog, shop.Workers, shop.Settings, user);
        shop.ReportsPage = new ReportsViewModel(shop.Reports);
        shop.SettingsPage = new SettingsViewModel(new NoBackups(), export, shop.Settings,
            shop.Availability, user, owner);

        shop.Shell = new MainWindowViewModel(shop.Home, shop.CatalogPage, shop.ClientsPage, shop.WorkersPage,
            shop.Agenda, shop.SalesPage, shop.TillPage, shop.ReportsPage, shop.SettingsPage,
            user, shop.Appointments, shop._notifier, owner);

        return shop;
    }

    // â”€â”€ Set-up helpers â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    public async Task<Service> AddService(string name, int priceCents, int vatBp = 2100, int? durationMin = 30)
    {
        var service = Make.Service(name, priceCents, vatBp, durationMin);
        service.Id = await Catalog.CreateService(service);
        return service;
    }

    public async Task<Product> AddProduct(string name, int priceCents, int vatBp = 2100)
    {
        var product = Make.Product(name, priceCents, vatBp);
        product.Id = await Catalog.CreateProduct(product);
        return product;
    }

    public async Task<Worker> AddWorker(string name)
        => await Workers.Create(Make.Worker(name), new Dictionary<Weekday, List<(TimeOnly, TimeOnly)>>
        {
            [Weekday.Mon] = [(new TimeOnly(9, 0), new TimeOnly(20, 0))]
        });

    public async Task<Client> AddClient(string name, string mobile = "612345678")
    {
        var client = Make.Client(name, mobile);
        client.Id = await Clients.Create(client);
        return client;
    }

    /// <summary>Books straight through the service; the stories that are about booking
    /// go through the dialog instead.</summary>
    public async Task<Appointment> Book(DateOnly date, TimeOnly time, Client? client = null,
        string? guest = null, Service? service = null, Worker? worker = null, int durationMin = 30)
    {
        int id = await Appointments.Create(new Appointment
        {
            Date = date, Time = time, DurationMin = durationMin,
            ClientId = client?.Id, GuestName = client is null ? guest ?? "Passavolant" : null,
            ServiceId = service?.Id, WorkerId = worker?.Id
        });
        return (await Appointments.GetById(id))!;
    }

    public async Task<int> MethodId(string name)
        => (await Catalog.GetMethods()).Single(m => m.Name == name).Id;

    public async ValueTask DisposeAsync() => await Db.DisposeAsync();

    /// <summary>The settings page needs a backup service only to show the last copy; the
    /// stories never take one.</summary>
    private sealed class NoBackups : IBackupService
    {
        public event Action<BackupInfo>? BackupTaken { add { } remove { } }
        public Task<List<BackupInfo>> ListAll() => Task.FromResult(new List<BackupInfo>());
        public Task<BackupInfo> MakeManualBackup() => throw new NotSupportedException();
        public Task<BackupInfo> MakeBackupBeforeUpdate() => throw new NotSupportedException();
        public Task<BackupInfo?> RunAutomaticBackupIfDue() => Task.FromResult<BackupInfo?>(null);
        public Task<BackupInfo?> RunBackupOnCloseIfDue() => Task.FromResult<BackupInfo?>(null);
        public Task DeleteOldBackups() => Task.CompletedTask;
        public Task Restore(string backupPath) => throw new NotSupportedException();
    }
}
