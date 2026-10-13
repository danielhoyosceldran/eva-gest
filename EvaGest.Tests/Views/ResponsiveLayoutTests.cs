using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using AwesomeAssertions;
using EvaGest.Helpers;
using EvaGest.Models;
using EvaGest.Resources;
using EvaGest.Services;
using EvaGest.Tests.Infra;
using EvaGest.ViewModels.Dialogs;
using EvaGest.ViewModels.Pages;
using EvaGest.Views.Dialogs;
using EvaGest.Views.Elements;
using EvaGest.Views.Pages;
using Xunit;

namespace EvaGest.Tests.Views;

/// <summary>
/// Block R — every dialog and page laid out at the screen sizes the shop's computer may
/// realistically have, with the worst data the shop can produce, in both languages.
///
/// Sizes are the work area in DIPs, after Windows display scaling and without the
/// taskbar:
///   A 1024x720  (1280x800 at 125 %)      B 1093x566  (1366x768 at 125 %, the worst)
///   C 1280x672  (1920x1080 at 150 %)     D 1366x720  (1366x768 at 100 %)
///   E 1920x1032 (1920x1080 at 100 %)   F 1536x816 (1920x1080 at 125 %, the shop's own)
///
/// A dialog passes if, capped to the screen as DialogWindow caps itself, its buttons
/// are fully on screen and nothing is cut off or pushed past an edge. A page passes if,
/// in the maximised main window, nothing is cut off, no table column collapses, and no
/// text is shortened without a tooltip to read it in full. Scrolling is fine: it is
/// clipping that hides things for good.
/// </summary>
[Collection(WpfCollection.Name)]
public class ResponsiveLayoutTests(ApplicationWpf app, WorstCaseShop shop) : IClassFixture<WorstCaseShop>
{
    private static readonly (string name, double width, double height)[] Screens =
    [
        ("A", 1024, 720), ("B", 1093, 566), ("C", 1280, 672), ("D", 1366, 720), ("E", 1920, 1032),
        ("F", 1536, 816)
    ];

    /// <summary>The shop's own screen (F). Scrolling is allowed everywhere else, but on
    /// this one an everyday sale must fit whole.</summary>
    private static readonly (string name, double width, double height) ShopScreen = ("F", 1536, 816);

    /// <summary>Non-client frame of a fixed-size dialog (caption and borders), and the
    /// caption of the maximised main window. Approximate, and on the generous side.</summary>
    private const double DialogFrameWidth = 16, DialogFrameHeight = 39, MainCaption = 31;

    /// <summary>Less than this much one-pixel rounding is not a clipped control.</summary>
    private const double Tolerance = 1.5;

    /// <summary>A dialog's scrolling form must keep at least this much room above the
    /// pinned buttons; less and it is technically scrollable but useless.</summary>
    private const double MinBodyHeight = 120;

    public static TheoryData<string, string> Dialogs => Cross(DialogBuilders.Keys);
    public static TheoryData<string, string> Pages => Cross(PageBuilders.Keys);
    public static TheoryData<string> Languages => new("ca-ES", "es-ES");

    private static TheoryData<string, string> Cross(IEnumerable<string> names)
    {
        var data = new TheoryData<string, string>();
        foreach (var name in names)
            foreach (var language in new[] { "ca-ES", "es-ES" })
                data.Add(name, language);
        return data;
    }

    // ───────────────────────────── dialogs ─────────────────────────────

    private record Built(object ViewModel, Func<FrameworkElement> View);

    private static readonly Dictionary<string, Func<WorstCaseShop, Task<Built>>> DialogBuilders = new()
    {
        ["Appointment"] = async s =>
        {
            var f = s.Factory;
            var vm = new AppointmentDialogViewModel(new AppointmentService(f), new AvailabilityService(f),
                new ClientService(f), new CatalogService(f), new WorkerService(f), new SettingsService(f),
                new TestDialogService(), WorstCaseShop.Today);
            await vm.Initialization;
            vm.ErrorValidation = "Cal indicar el nom del client per guardar la cita, i l'hora ha de ser vàlida.";
            return new(vm, () => new AppointmentDialogView());
        },
        ["Sale with 20 lines"] = async s =>
        {
            var f = s.Factory;
            var settings = new SettingsService(f);
            var vm = await SaleDialogViewModel.New(new SaleService(f, settings), new ClientService(f),
                new CatalogService(f), new WorkerService(f), new TestSoundService(), settings, new TestDialogService());
            for (int i = 0; i < 20; i++) vm.AddCustomConceptCommand.Execute(null);
            vm.ErrorValidation = "Cal indicar el preu de cada línia per poder cobrar la venda.";
            return new(vm, () => new SaleDialogView());
        },
        ["Worker, full week"] = async s =>
        {
            var worker = (await new WorkerService(s.Factory).GetAll()).First();
            var week = Enum.GetValues<Weekday>().ToDictionary(d => d,
                _ => new List<(TimeOnly, TimeOnly)> { (new(9, 0), new(14, 0)), (new(16, 0), new(20, 0)) });
            return new(new WorkerDialogViewModel(worker, week), () => new WorkerDialogView());
        },
        ["Client record"] = async s =>
        {
            var clients = new ClientService(s.Factory);
            var vm = new ClientRecordViewModel(clients, new ReportsService(s.Factory), new TestDialogService(),
                (await clients.GetActive()).First(), showAmounts: true);
            await vm.Load();
            return new(vm, () => new ClientRecordView());
        },
        ["Client browser"] = async s => new(
            new ClientBrowserDialogViewModel(await new ClientService(s.Factory).GetActive()),
            () => new ClientBrowserDialogView()),
        ["Option browser"] = async s => new(
            new OptionBrowserDialogViewModel("Triar treballadora",
                (await new WorkerService(s.Factory).GetAll()).Select(w => new PickerOption(w, w.Name))),
            () => new OptionBrowserDialogView()),
        ["Client"] = s => Task.FromResult(new Built(
            new ClientDialogViewModel(new ClientService(s.Factory))
            {
                ErrorValidation = "Ja hi ha un client amb aquest nom. Escriu un nom diferent per guardar-lo."
            },
            () => new ClientDialogView())),
        ["Help"] = _ => Task.FromResult(new Built(new HelpViewModel(), () => new HelpView())),
        ["Restore with 25 backups"] = s =>
        {
            var vm = new RestoreDialogViewModel(new BackupService(new AppPaths("live.db", "."), new TestSettings()),
                new TestDialogService());
            for (int i = 0; i < 25; i++)
                vm.Backups.Add(new BackupInfo($"copy{i}", DateTime.Now.AddDays(-i), i % 2 == 0, 123_456_789));
            return Task.FromResult(new Built(vm, () => new RestoreDialogView()));
        },
        ["Export"] = s => Task.FromResult(new Built(
            new ExportDialogViewModel(new ExportService(s.Factory), new TestDialogService()),
            () => new ExportDialogView())),
        ["Cash out with VAT"] = async s =>
        {
            var f = s.Factory;
            var vm = await MovementDialogViewModel.New(MovementType.Out, WorstCaseShop.Today,
                new CatalogService(f), new WorkerService(f), new SettingsService(f));
            vm.SplitVat = true;
            return new(vm, () => new MovementDialogView());
        },
        ["Transactions"] = async s =>
        {
            var f = s.Factory;
            var vm = new TransactionsDialogViewModel(new TransactionsService(f), new ExportService(f),
                new TestDialogService(), WorstCaseShop.Today);
            await vm.Load();
            await vm.SelectThisYearCommand.ExecuteAsync(null);
            vm.IncludeVoided = true;
            await vm.Search();
            vm.ErrorValidation = "No s'han pogut carregar les transaccions. Canvia algun filtre o torna-ho a provar d'aquí a un moment.";
            return new(vm, () => new TransactionsDialogView());
        },
        ["Service"] = _ => Task.FromResult(new Built(new ServiceDialogViewModel(), () => new ServiceDialogView())),
        ["Product"] = _ => Task.FromResult(new Built(new ProductDialogViewModel(), () => new ProductDialogView())),
        ["Category"] = s => Task.FromResult(new Built(new CategoryDialogViewModel(new CatalogService(s.Factory)), () => new CategoryDialogView())),
        ["Payment method"] = s => Task.FromResult(new Built(new PaymentMethodDialogViewModel(new CatalogService(s.Factory)), () => new PaymentMethodDialogView())),
        ["Owner unlock"] = s => Task.FromResult(new Built(
            new OwnerUnlockDialogViewModel(new OwnerAccessService(new SettingsService(s.Factory))),
            () => new OwnerUnlockDialogView())),
        ["Confirm"] = _ => Task.FromResult(new Built(
            new ConfirmDialogViewModel(Texts.DiscardChangesTitle,
                $"La venda de {WorstCaseShop.LongName} ja té un «Tall de cabell i arranjament de barba complet 0».",
                Texts.DiscardChanges, Texts.KeepEditing, destructive: true),
            () => new ConfirmDialogView())),
        ["Owner PIN confirm"] = s => Task.FromResult(new Built(
            new OwnerPinConfirmDialogViewModel(new OwnerAccessService(new SettingsService(s.Factory)),
                "Eliminar la venda",
                $"S'eliminarà la venda de {WorstCaseShop.LongName} del dia d'avui per un import de 133,44 €.",
                "Eliminar"),
            () => new OwnerPinConfirmDialogView())),
        ["Owner PIN change"] = s => Task.FromResult(new Built(
            new OwnerPinDialogViewModel(new OwnerAccessService(new SettingsService(s.Factory)), OwnerPinMode.Change),
            () => new OwnerPinDialogView())),
        ["Recovery code"] = _ => Task.FromResult(new Built(
            new RecoveryCodeDialogViewModel("ABCD-EFGH-IJKL"), () => new RecoveryCodeDialogView())),
    };

    [Theory, MemberData(nameof(Dialogs))]
    public async Task Every_dialog_fits_the_screen_with_its_buttons_in_view(string dialog, string language)
    {
        using var _ = new Language(language);
        var built = await DialogBuilders[dialog](shop);
        var problems = new List<string>();

        app.Runs(() =>
        {
            using var __ = new Language(language);
            foreach (var screen in Screens)
            {
                // What DialogWindow does: cap the window to the work area, then size to
                // the content inside that cap. The host mirrors DialogWindow.xaml.
                var (maxWidth, maxHeight) = LayoutFit.MaxWindowSize(screen.width, screen.height,
                    (double)Application.Current.FindResource("DialogScreenMargin"));
                var view = built.View();
                view.DataContext = built.ViewModel;
                var host = new ContentControl
                {
                    Content = view, MinWidth = 380,
                    Margin = (Thickness)Application.Current.FindResource("PadCard")
                };
                var cap = new Size(maxWidth - DialogFrameWidth, maxHeight - DialogFrameHeight);
                // A dialog that fills the screen is not sized to its content: DialogWindow
                // gives it the whole cap, so it is laid out at exactly that size.
                bool fills = built.ViewModel is DialogViewModelBase { FillsScreen: true };
                bool fillsHeight = built.ViewModel is DialogViewModelBase { FillsHeight: true };
                Layout(host, cap, sizeToContent: !fills, fillsHeight);

                foreach (var p in Problems(host)) problems.Add($"{screen.name}: {p}");

                // The form must keep some room above the pinned buttons
                var body = Descendants<ScrollViewer>(host).FirstOrDefault(sv => sv.TemplatedParent is DialogShell);
                if (body is null)
                    problems.Add($"{screen.name}: not wrapped in a DialogShell, so nothing scrolls and nothing is pinned");
                else if (body.ViewportHeight + Tolerance < Math.Min(body.ExtentHeight, MinBodyHeight))
                    problems.Add($"{screen.name}: the form has only {body.ViewportHeight:0} DIP above the buttons");
            }
        });

        problems.Should().BeEmpty("the {0} dialog must fit every screen in {1}:{2}",
            dialog, language, Lines(problems));
    }

    [Theory, MemberData(nameof(Languages))]
    public async Task An_everyday_sale_fits_the_shops_screen_without_scrolling(string language)
    {
        // Four lines, a registered client, a worker and a payment method: what the shop
        // charges all day. On its 1920x1080 screen at 125 % the whole dialog, totals and
        // Cobrar included, must be in view without touching the scroll bar.
        using var _ = new Language(language);
        var f = shop.Factory;
        var settings = new SettingsService(f);
        var clients = new ClientService(f);
        var vm = await SaleDialogViewModel.New(new SaleService(f, settings), clients, new CatalogService(f),
            new WorkerService(f), new TestSoundService(), settings, new TestDialogService());
        vm.SelectedClient = (await clients.GetActive()).First();
        vm.Worker = vm.ActiveWorkers.First();
        vm.PaymentMethod = vm.ActiveMethods.First();
        foreach (var quick in vm.QuickServices.Take(4)) vm.AddQuickServiceCommand.Execute(quick);
        vm.Lines.Should().HaveCount(4);

        string? problem = null;
        app.Runs(() =>
        {
            using var __ = new Language(language);
            var (maxWidth, maxHeight) = LayoutFit.MaxWindowSize(ShopScreen.width, ShopScreen.height,
                (double)Application.Current.FindResource("DialogScreenMargin"));
            var host = new ContentControl
            {
                Content = new SaleDialogView(), DataContext = vm, MinWidth = 380,
                Margin = (Thickness)Application.Current.FindResource("PadCard")
            };
            ((FrameworkElement)host.Content).DataContext = vm;
            Layout(host, new Size(maxWidth - DialogFrameWidth, maxHeight - DialogFrameHeight), sizeToContent: true,
                fillsHeight: vm.FillsHeight);

            var body = Descendants<ScrollViewer>(host).First(sv => sv.TemplatedParent is DialogShell);
            var lines = Descendants<ScrollViewer>(host).First(sv => sv.Name == "LinesScroll");
            if (body.ExtentHeight > body.ViewportHeight + Tolerance)
                problem = $"the form needs {body.ExtentHeight:0} DIP but has {body.ViewportHeight:0}";
            // The lines list scrolls by itself, but four lines should not need to
            else if (lines.ExtentHeight > lines.ViewportHeight + Tolerance)
                problem = $"the lines need {lines.ExtentHeight:0} DIP but have {lines.ViewportHeight:0}";
        });

        problem.Should().BeNull("an everyday sale must fit the shop's screen in {0}", language);
    }

    /// <summary>
    /// The heaviest everyday case for the top of the form: charged from an appointment
    /// (the "Des de la cita…" line), for someone not registered (the guest notice with its
    /// button), with the quick-service buttons showing. Found in manual test B7: the
    /// payment methods ended up half under the pinned buttons and the form had to scroll.
    /// </summary>
    [Theory, MemberData(nameof(Languages))]
    public async Task A_guest_sale_from_an_appointment_fits_the_shops_screen_without_scrolling(string language)
    {
        using var _ = new Language(language);
        await using var db = new TestDatabase();
        var f = new TestFactory(db.Options);
        var settings = new SettingsService(f);
        await new SeedService(f, settings).Seed();
        Appointment appointment;
        await using (var ctx = db.Context())
        {
            ctx.Services.AddRange(Make.Service("Tall", 1500), Make.Service("Barba", 800));
            var worker = Make.Worker("Laia");
            ctx.Workers.Add(worker);
            await ctx.SaveChangesAsync();
            appointment = new Appointment
            {
                Date = WorstCaseShop.Today, Time = new TimeOnly(11, 0), DurationMin = 15,
                GuestName = "Pere Vidal", ServiceId = ctx.Services.First(s => s.Name == "Barba").Id,
                WorkerId = worker.Id
            };
            ctx.Appointments.Add(appointment);
            await ctx.SaveChangesAsync();
        }
        var vm = await SaleDialogViewModel.FromAppointment(new SaleService(f, settings), new ClientService(f),
            new CatalogService(f), new WorkerService(f), new TestSoundService(), settings, new TestDialogService(),
            appointment);
        vm.UnregisteredClientNotice.Should().BeTrue("this case is about the guest notice");
        vm.QuickServices.Should().NotBeEmpty();

        string? problem = null;
        app.Runs(() =>
        {
            using var __ = new Language(language);
            var (maxWidth, maxHeight) = LayoutFit.MaxWindowSize(ShopScreen.width, ShopScreen.height,
                (double)Application.Current.FindResource("DialogScreenMargin"));
            var host = new ContentControl
            {
                Content = new SaleDialogView(), DataContext = vm, MinWidth = 380,
                Margin = (Thickness)Application.Current.FindResource("PadCard")
            };
            ((FrameworkElement)host.Content).DataContext = vm;
            Layout(host, new Size(maxWidth - DialogFrameWidth, maxHeight - DialogFrameHeight), sizeToContent: true,
                fillsHeight: vm.FillsHeight);

            var body = Descendants<ScrollViewer>(host).First(sv => sv.TemplatedParent is DialogShell);
            if (body.ExtentHeight > body.ViewportHeight + Tolerance)
                problem = $"the form needs {body.ExtentHeight:0} DIP but has {body.ViewportHeight:0}";
        });

        problem.Should().BeNull("a guest sale from an appointment must fit the shop's screen in {0}", language);
    }

    [Fact]
    public async Task A_long_sale_fills_the_screen_and_scrolls_only_its_lines()
    {
        // Twenty lines on the shop's screen: the dialog takes the whole height, the form
        // around the lines stays still, and only the lines list scrolls.
        var f = shop.Factory;
        var settings = new SettingsService(f);
        var vm = await SaleDialogViewModel.New(new SaleService(f, settings), new ClientService(f),
            new CatalogService(f), new WorkerService(f), new TestSoundService(), settings, new TestDialogService());
        for (int i = 0; i < 20; i++) vm.AddCustomConceptCommand.Execute(null);

        // Read on the WPF thread, asserted after it
        double hostHeight = 0, capHeight = 0, minLines = 0;
        (double extent, double viewport) body = default, lines = default;
        app.Runs(() =>
        {
            var (maxWidth, maxHeight) = LayoutFit.MaxWindowSize(ShopScreen.width, ShopScreen.height,
                (double)Application.Current.FindResource("DialogScreenMargin"));
            var host = new ContentControl
            {
                Content = new SaleDialogView(), DataContext = vm, MinWidth = 380,
                Margin = (Thickness)Application.Current.FindResource("PadCard")
            };
            capHeight = maxHeight - DialogFrameHeight;
            Layout(host, new Size(maxWidth - DialogFrameWidth, capHeight), sizeToContent: true, fillsHeight: true);
            // RenderSize leaves out the host's own margin (the window's padding)
            hostHeight = host.RenderSize.Height + host.Margin.Top + host.Margin.Bottom;
            minLines = (double)Application.Current.FindResource("SaleLinesMinHeight");
            var bodyScroll = Descendants<ScrollViewer>(host).First(sv => sv.TemplatedParent is DialogShell);
            var linesScroll = Descendants<ScrollViewer>(host).First(sv => sv.Name == "LinesScroll");
            body = (bodyScroll.ExtentHeight, bodyScroll.ViewportHeight);
            lines = (linesScroll.ExtentHeight, linesScroll.ViewportHeight);
        });

        vm.FillsHeight.Should().BeTrue();
        hostHeight.Should().BeApproximately(capHeight, Tolerance);
        body.extent.Should().BeLessThanOrEqualTo(body.viewport + Tolerance, "the form itself must not scroll");
        lines.extent.Should().BeGreaterThan(lines.viewport, "twenty lines scroll inside their own list");
        lines.viewport.Should().BeGreaterThanOrEqualTo(minLines);
    }

    // ───────────────────────────── pages ─────────────────────────────

    private static readonly Dictionary<string, Func<WorstCaseShop, Task<Built>>> PageBuilders = new()
    {
        ["Home, owner mode"] = async s =>
        {
            var f = s.Factory;
            var settings = new SettingsService(f);
            var vm = new HomeViewModel(new AppointmentService(f), new SaleService(f, settings), new TillService(f),
                new ClientService(f), new AvailabilityService(f), new CatalogService(f), new WorkerService(f),
                settings, new TestSoundService(), new TestDialogService());
            await vm.Load();
            return new(vm, () => new HomeView());
        },
        ["Agenda"] = async s => new(await Agenda(s, openDay: false), () => new AgendaView()),
        ["Agenda, week and day panel"] = async s => new(await Agenda(s, openDay: true), () => new AgendaView()),
        // The app's default view, whose toolbar is the widest: « and » show only here
        ["Agenda, three days"] = async s => new(await Agenda(s, openDay: false, threeDays: true), () => new AgendaView()),
        ["Clients"] = async s =>
        {
            var vm = new ClientsViewModel(new ClientService(s.Factory), new ReportsService(s.Factory),
                new TestDialogService(), new UnlockedOwner());
            await vm.Load();
            return new(vm, () => new ClientsView());
        },
        ["Workers"] = async s =>
        {
            var vm = new WorkersViewModel(new WorkerService(s.Factory), new TestDialogService());
            await vm.Load();
            return new(vm, () => new WorkersView());
        },
        ["Catalog"] = async s =>
        {
            var vm = new CatalogViewModel(new CatalogService(s.Factory), new SettingsService(s.Factory), new TestDialogService());
            await vm.Load();
            return new(vm, () => new CatalogView());
        },
        ["Sales"] = async s =>
        {
            var f = s.Factory;
            var settings = new SettingsService(f);
            var vm = new SalesViewModel(new SaleService(f, settings), new ClientService(f), new CatalogService(f),
                new WorkerService(f), new TestSoundService(), settings, new ExportService(f), new TestDialogService());
            await vm.Load();
            return new(vm, () => new SalesView());
        },
        ["Till"] = async s =>
        {
            var f = s.Factory;
            var vm = new TillViewModel(new TillService(f), new CatalogService(f), new WorkerService(f),
                new SettingsService(f), new TestDialogService());
            await vm.Load();
            return new(vm, () => new TillView());
        },
        ["Reports"] = async s =>
        {
            var vm = new ReportsViewModel(new ReportsService(s.Factory));
            await vm.Load();
            return new(vm, () => new ReportsView());
        },
        ["Settings"] = async s =>
        {
            var f = s.Factory;
            var vm = new SettingsViewModel(new BackupService(new AppPaths("live.db", "."), new TestSettings()),
                new ExportService(f), new SettingsService(f), new AvailabilityService(f), new TestDialogService(),
                new UnlockedOwner());
            await vm.Load();
            return new(vm, () => new SettingsView());
        },
    };

    /// <summary>The agenda in the week view (its widest grid), optionally with today's
    /// detail panel open beside it — the narrowest the week grid ever gets — or in the
    /// three-day view, whose toolbar is the widest.</summary>
    private static async Task<AgendaViewModel> Agenda(WorstCaseShop s, bool openDay, bool threeDays = false)
    {
        var f = s.Factory;
        var settings = new SettingsService(f);
        var vm = new AgendaViewModel(new AppointmentService(f), new SaleService(f, settings), new AvailabilityService(f),
            new ClientService(f), new CatalogService(f), new WorkerService(f), settings, new TestSoundService(),
            new TestDialogService());
        await vm.Load();
        if (vm.Grid.IsThreeDayView != threeDays) await vm.Grid.ToggleViewCommand.ExecuteAsync(null);
        if (openDay)
        {
            vm.Grid.HeaderClickCommand.Execute(vm.Grid.Days.First(d => d.Date == WorstCaseShop.Today));
            for (int i = 0; i < 250 && (!vm.HasSelectedDay || vm.DayAppointments.Count == 0); i++)
                await Task.Delay(20, TestContext.Current.CancellationToken);
            vm.DayAppointments.Should().NotBeEmpty("the day panel must be open for this case");
        }
        return vm;
    }

    [Theory, MemberData(nameof(Pages))]
    public async Task Every_page_fits_the_main_window_without_clipping(string page, string language)
    {
        using var _ = new Language(language);
        var built = await PageBuilders[page](shop);
        var problems = new List<string>();

        app.Runs(() =>
        {
            using var __ = new Language(language);
            foreach (var screen in PageScreens())
            {
                // The maximised main window, minus the navigation column
                var size = new Size(screen.width - (double)Application.Current.FindResource("NavWidth"),
                                    screen.height - MainCaption);
                var view = built.View();
                view.DataContext = built.ViewModel;
                Layout(view, size, sizeToContent: false);

                foreach (var p in Problems(view)) problems.Add($"{screen.name}: {p}");
            }
        });

        problems.Should().BeEmpty("the {0} page must fit every screen in {1}:{2}",
            page, language, Lines(problems));
    }

    /// <summary>
    /// The screens plus "M": the main window restored and shrunk to its minimum size
    /// (MainMinWidth x MainMinHeight, frame included), the narrowest a page ever gets.
    /// Written like a screen so the page loop takes off the navigation column and the
    /// caption the same way. Manual test B found the Agenda and Caixa toolbars cut off
    /// at that size, which no screen here was narrow enough to show. Call on the UI thread.
    /// </summary>
    private static IEnumerable<(string name, double width, double height)> PageScreens()
        => Screens.Append(("M",
            (double)Application.Current.FindResource("MainMinWidth") - DialogFrameWidth,
            (double)Application.Current.FindResource("MainMinHeight") - DialogFrameHeight + MainCaption));

    /// <summary>
    /// The page is first shown empty (a new install) and the first worker arrives while it
    /// is on screen. The table's row-actions column is sized "Auto": it used to be measured
    /// while the table was hidden behind the empty state, came out almost zero wide, and
    /// stayed that way — Editar hidden, the status button cut to "…car inactiva".
    /// </summary>
    [Theory, MemberData(nameof(Languages))]
    public async Task The_workers_table_shows_its_row_actions_when_the_first_worker_arrives_on_screen(string language)
    {
        using var _ = new Language(language);
        await using var db = new TestDatabase();
        var vm = new WorkersViewModel(new WorkerService(new TestFactory(db.Options)), new TestDialogService());
        await vm.Load();
        var problems = new List<string>();

        app.Runs(() =>
        {
            using var __ = new Language(language);
            var size = new Size(ShopScreen.width - (double)Application.Current.FindResource("NavWidth"),
                                ShopScreen.height - MainCaption);
            var view = new WorkersView { DataContext = vm };
            Layout(view, size, sizeToContent: false);

            using (var ctx = db.Context())
            {
                ctx.Workers.Add(new Worker
                {
                    Name = "Laia", Color = "#0F766E",
                    Schedules = [new WorkerSchedule { Weekday = Weekday.Mon, StartTime = new(9, 0), EndTime = new(13, 0) }]
                });
                ctx.SaveChanges();
            }
            // Reloaded on the UI thread, as the page does when a dialog saves
            Await(vm.Load());
            Layout(view, size, sizeToContent: false);

            problems.AddRange(Problems(view));
        });

        problems.Should().BeEmpty("the row actions must be fully visible in {0}:{1}", language, Lines(problems));
    }

    /// <summary>
    /// A new install: only what the first start seeds. No page may show a table that is
    /// just its headers — each says instead what to do (disseny-ui 9), and the client
    /// rankings in Informes say why they are empty instead of showing blank cards.
    /// </summary>
    [Theory, MemberData(nameof(Languages))]
    public async Task No_page_shows_a_bare_table_on_a_fresh_install(string language)
    {
        using var _ = new Language(language);
        // A WorstCaseShop never initialised is an empty database; seed it as the first
        // start does, and nothing more.
        await using var fresh = new WorstCaseShop();
        await new SeedService(fresh.Factory, new SettingsService(fresh.Factory)).Seed();

        var built = new Dictionary<string, Built>();
        // The day-panel case needs appointments to open the panel at all
        foreach (var (page, build) in PageBuilders.Where(p => p.Key != "Agenda, week and day panel"))
            built[page] = await build(fresh);
        var problems = new List<string>();

        app.Runs(() =>
        {
            using var __ = new Language(language);
            var size = new Size(ShopScreen.width - (double)Application.Current.FindResource("NavWidth"),
                                ShopScreen.height - MainCaption);
            foreach (var (page, b) in built)
            {
                // The asleep clients' list starts folded; open it, as it is a table too (B-27)
                if (b.ViewModel is ClientsViewModel clients) clients.ShowAsleep = true;
                var view = b.View();
                view.DataContext = b.ViewModel;
                Layout(view, size, sizeToContent: false);

                foreach (var grid in Descendants<DataGrid>(view).Where(g => Shown(g, view) && g.Items.Count == 0))
                    problems.Add($"{page}: a table with only its headers ({string.Join(", ", grid.Columns.Select(c => c.Header))})");

                if (page == "Reports")
                {
                    int shown = Descendants<TextBlock>(view).Count(t => Shown(t, view) && t.Text == Texts.NoClientRankingYet);
                    if (shown != 3) problems.Add($"Reports: {shown} of the 3 client rankings say why they are empty");
                }
            }
        });

        problems.Should().BeEmpty("every empty list must say what to do in {0}:{1}", language, Lines(problems));
    }

    /// <summary>Waits for a task started on the UI thread by pumping the dispatcher, so its
    /// continuations (which come back to this thread) can run.</summary>
    private static void Await(Task task)
    {
        var frame = new System.Windows.Threading.DispatcherFrame();
        task.ContinueWith(_ => frame.Continue = false, TaskScheduler.Default);
        System.Windows.Threading.Dispatcher.PushFrame(frame);
        task.GetAwaiter().GetResult();
    }

    // ───────────────────────────── shell ─────────────────────────────

    [Theory, MemberData(nameof(Languages))]
    public void Every_navigation_entry_stays_reachable_in_owner_mode(string language)
    {
        var problems = new List<string>();

        app.Runs(() =>
        {
            using var _ = new Language(language);
            // The real MainWindow's sidebar, detached so it can be laid out alone
            var window = UnshownMainWindow();
            var root = (Grid)window.Content;
            window.Content = null;
            var sidebar = (FrameworkElement)root.Children[0];
            root.Children.Remove(sidebar);
            sidebar.DataContext = new OwnerModeShell();

            foreach (var screen in Screens)
            {
                Layout(sidebar, new Size((double)Application.Current.FindResource("NavWidth"),
                                         screen.height - MainCaption), sizeToContent: false);
                foreach (var p in Problems(sidebar)) problems.Add($"{screen.name}: {p}");
            }
        });

        problems.Should().BeEmpty("every entry must be reachable in {0}:{1}",
            language, Lines(problems));
    }

    [Fact]
    public void The_main_window_cannot_shrink_below_the_smallest_supported_screen()
    {
        app.Runs(() =>
        {
            // A restored window must still fit screen B's work area, caption included
            var window = UnshownMainWindow();
            window.MinWidth.Should().BeGreaterThan(0).And.BeLessThanOrEqualTo(Screens.Min(s => s.width));
            window.MinHeight.Should().BeGreaterThan(0).And.BeLessThanOrEqualTo(Screens.Min(s => s.height));
        });
    }

    /// <summary>
    /// A MainWindow that is never shown. WPF makes the first window created the
    /// application's MainWindow, which the test Application must not have (XamlLoadTests),
    /// so that is put back. Closing it instead could shut the test Application down.
    /// </summary>
    private static EvaGest.MainWindow UnshownMainWindow()
    {
        var previous = Application.Current.MainWindow;
        var window = new EvaGest.MainWindow();
        Application.Current.MainWindow = previous;
        return window;
    }

    // ───────────────────────────── measuring ─────────────────────────────

    /// <summary>
    /// Lays the element out at <paramref name="size"/>. A SizeToContent dialog ends up
    /// as big as its content but no bigger than the cap; a page fills its slot. Run
    /// three times with the dispatcher pumped in between, so ElementName bindings to
    /// ActualHeight and the SizeChanged handlers in the code-behind settle.
    /// </summary>
    /// <param name="fillsHeight">Mirrors DialogWindow for a FillsHeight dialog: sized to
    /// its content across, but always the full height on offer.</param>
    private static void Layout(FrameworkElement element, Size size, bool sizeToContent, bool fillsHeight = false)
    {
        for (int i = 0; i < 3; i++)
        {
            element.Measure(size);
            var final = sizeToContent
                ? new Size(Math.Min(element.DesiredSize.Width, size.Width),
                    fillsHeight ? size.Height : Math.Min(element.DesiredSize.Height, size.Height))
                : size;
            element.Arrange(new Rect(final));
            element.UpdateLayout();
            Pump();
        }
    }

    private static void Pump()
    {
        var frame = new System.Windows.Threading.DispatcherFrame();
        System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(
            System.Windows.Threading.DispatcherPriority.ApplicationIdle, () => frame.Continue = false);
        System.Windows.Threading.Dispatcher.PushFrame(frame);
    }

    /// <summary>
    /// Everything wrong with an arranged tree, one line each:
    /// - a control or text cut off by layout (arranged smaller than it needs);
    /// - a control pushed past the right or bottom edge, unless it sits in a scrolling
    ///   area that can bring it into view;
    /// - text shortened with "…" and no tooltip to read it in full;
    /// - a flexible table column squeezed below its minimum width;
    /// - a button in a table cell wider than its column.
    /// The text in table cells and everything in the week grid are left out: both clip
    /// by design and carry a tooltip with the full text. Buttons in table cells are not.
    /// </summary>
    private static List<string> Problems(FrameworkElement root)
    {
        var found = new List<string>();

        foreach (var element in Descendants<FrameworkElement>(root))
        {
            if (!Shown(element, root) || InsideWeekGrid(element, root)) continue;
            // The DataGrid's filler header is a background strip behind the real headers:
            // it always spans the whole row and is clipped by design.
            if (element is DataGridColumnHeader { Name: "PART_FillerColumnHeader" }) continue;
            if (element is TextBlock && InsideTableCell(element, root)) continue;

            if (element is DataGrid grid)
                foreach (var column in grid.Columns.Where(c => c.Visibility == Visibility.Visible && c.Width.IsStar))
                    if (column.ActualWidth + Tolerance < Math.Max(column.MinWidth, 20))
                        found.Add($"table column '{column.Header}' squeezed to {column.ActualWidth:0} DIP");

            bool checkable = element is TextBlock or ButtonBase or ComboBox or TextBox or PasswordBox;
            if (!checkable || element.TemplatedParent is TextBox or ComboBox) continue;

            var clip = LayoutInformation.GetLayoutClip(element);
            if (clip is not null && !element.ClipToBounds && !clip.Bounds.IsEmpty
                && (clip.Bounds.Width + Tolerance < element.ActualWidth || clip.Bounds.Height + Tolerance < element.ActualHeight))
                found.Add($"{Describe(element)} cut to {clip.Bounds.Width:0}x{clip.Bounds.Height:0} of {element.ActualWidth:0}x{element.ActualHeight:0}");

            // A button in a table cell is arranged at its full size even when the column is
            // too narrow for it: the cell clips it, not the layout, so check it against the cell.
            if (element is ButtonBase && Ancestors(element, root).OfType<DataGridCell>().FirstOrDefault() is { } cell)
            {
                var inCell = element.TransformToAncestor(cell)
                    .TransformBounds(new Rect(0, 0, element.ActualWidth, element.ActualHeight));
                if (inCell.Left < -Tolerance || inCell.Right > cell.ActualWidth + Tolerance)
                    found.Add($"{Describe(element)} cut by its table cell ({cell.ActualWidth:0} DIP wide)");
            }

            if (!InsideScrollingArea(element, root))
            {
                var bounds = element.TransformToAncestor(root)
                    .TransformBounds(new Rect(0, 0, element.ActualWidth, element.ActualHeight));
                if (bounds.Right > root.ActualWidth + Tolerance || bounds.Bottom > root.ActualHeight + Tolerance)
                    found.Add($"{Describe(element)} pushed off screen to {bounds.Right:0},{bounds.Bottom:0} " +
                              $"(room {root.ActualWidth:0}x{root.ActualHeight:0})");
            }
            // A scrolling page only scrolls up and down: what runs off its right edge is
            // cut off for good (the Caixa toolbar's + Sortida at the window's minimum width).
            // Text built to shorten itself with "…" is left to the trimming check below.
            // Only content counts: a scroll bar sits beside the viewport by design, and a
            // table scrolls sideways on its own.
            else if (element is not TextBlock { TextTrimming: not TextTrimming.None }
                     && !Ancestors(element, root).Any(a => a is DataGrid)
                     && Ancestors(element, root).FirstOrDefault(a => a is ScrollContentPresenter or ScrollBar)
                         is ScrollContentPresenter { ScrollOwner: { HorizontalScrollBarVisibility: ScrollBarVisibility.Disabled } page })
            {
                var bounds = element.TransformToAncestor(page)
                    .TransformBounds(new Rect(0, 0, element.ActualWidth, element.ActualHeight));
                if (bounds.Right > page.ViewportWidth + Tolerance)
                    found.Add($"{Describe(element)} cut off on the right at {bounds.Right:0} " +
                              $"(room {page.ViewportWidth:0}, and it cannot scroll sideways)");
            }

            // A control running past the right edge of its card is cut by the card, which no
            // layout clip above reports (the shop's opening hours at the window's minimum
            // width, B-25). Tables scroll sideways on their own and are left out.
            if (element is not TextBlock && !Ancestors(element, root).Any(a => a is DataGrid)
                && Ancestors(element, root).OfType<Border>().FirstOrDefault(IsCard) is { } card)
            {
                var inCard = element.TransformToAncestor(card)
                    .TransformBounds(new Rect(0, 0, element.ActualWidth, element.ActualHeight));
                // The card's padding is where its content stops: past it, the content is cut
                double room = card.ActualWidth - card.Padding.Right - card.BorderThickness.Right;
                if (inCard.Right > room + Tolerance)
                    found.Add($"{Describe(element)} runs past its card to {inCard.Right:0} (room {room:0})");
            }

            if (element is TextBlock text && IsTrimmed(text) && !HasToolTip(text, root))
                found.Add($"{Describe(text)} shortened with no tooltip");
        }

        return found.Distinct().ToList();
    }

    private static bool IsTrimmed(TextBlock text)
    {
        if (text.TextTrimming == TextTrimming.None || text.TextWrapping != TextWrapping.NoWrap
            || string.IsNullOrEmpty(text.Text) || text.ActualWidth <= 0) return false;
        var formatted = new FormattedText(text.Text, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
            new Typeface(text.FontFamily, text.FontStyle, text.FontWeight, text.FontStretch), text.FontSize,
            Brushes.Black, VisualTreeHelper.GetDpi(text).PixelsPerDip);
        return formatted.WidthIncludingTrailingWhitespace > text.ActualWidth - text.Padding.Left - text.Padding.Right + Tolerance;
    }

    /// <summary>Visible all the way up. IsVisible itself is false outside a window.</summary>
    private static bool Shown(FrameworkElement element, FrameworkElement root)
    {
        for (DependencyObject? d = element; d is not null; d = VisualTreeHelper.GetParent(d))
        {
            if (d is UIElement u && u.Visibility != Visibility.Visible) return false;
            if (d == root) return true;
        }
        return true;
    }

    private static bool InsideWeekGrid(DependencyObject element, FrameworkElement root)
        => Ancestors(element, root).Any(a => a is WeekGridView);

    private static bool IsCard(Border border)
        => border.Style is not null && ReferenceEquals(border.Style, border.TryFindResource("DataCard"));

    private static bool InsideTableCell(DependencyObject element, FrameworkElement root)
        => Ancestors(element, root).Any(a => a is DataGridCell);

    private static bool InsideScrollingArea(DependencyObject element, FrameworkElement root)
        => Ancestors(element, root).Any(a => a is ScrollContentPresenter);

    private static bool HasToolTip(FrameworkElement element, FrameworkElement root)
        => element.ToolTip is not null
           || Ancestors(element, root).OfType<FrameworkElement>().Any(a => a.ToolTip is not null);

    private static IEnumerable<DependencyObject> Ancestors(DependencyObject element, FrameworkElement root)
    {
        for (var d = VisualTreeHelper.GetParent(element); d is not null && d != root; d = VisualTreeHelper.GetParent(d))
            yield return d;
    }

    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        int n = VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < n; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T found) yield return found;
            foreach (var deeper in Descendants<T>(child)) yield return deeper;
        }
    }

    private static string Describe(FrameworkElement element) => element switch
    {
        TextBlock t => $"text \"{Short(t.Text)}\"",
        ContentControl { Content: string s } c => $"{c.GetType().Name} \"{Short(s)}\"",
        _ when !string.IsNullOrEmpty(element.Name) => $"{element.GetType().Name} {element.Name}",
        _ => element.GetType().Name
    };

    private static string Short(string s) => s.Length > 40 ? s[..40] + "…" : s;

    /// <summary>Every problem on its own line, so a failure lists all of them rather
    /// than only the first.</summary>
    private static string Lines(IEnumerable<string> problems)
        => string.Concat(problems.Select(p => Environment.NewLine + "  " + p));

    /// <summary>
    /// Switches this test, and the UI thread while it builds views, to one language —
    /// only the current thread and async flow, never the process default, which the
    /// tests running in parallel share.
    /// </summary>
    private sealed class Language : IDisposable
    {
        private readonly CultureInfo _culture = CultureInfo.CurrentCulture;
        private readonly CultureInfo _uiCulture = CultureInfo.CurrentUICulture;

        public Language(string name)
            => CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(name);

        public void Dispose()
        {
            CultureInfo.CurrentCulture = _culture;
            CultureInfo.CurrentUICulture = _uiCulture;
        }
    }

    /// <summary>The shell's ViewModel as the sidebar sees it, with owner mode open so
    /// every private page's entry shows.</summary>
    public sealed class OwnerModeShell
    {
        public bool IsOwnerUnlocked => true;
        public object? CurrentPage => null;
    }

    /// <summary>Owner mode open, so the pages show every money card and column.</summary>
    private sealed class UnlockedOwner : IOwnerAccessService
    {
        public bool IsUnlocked => true;
        public event Action? Changed { add { } remove { } }
        public TimeSpan LockoutRemaining => TimeSpan.Zero;
        public Task<bool> HasPin() => Task.FromResult(true);
        public Task<AccessResult> Unlock(string pin) => Task.FromResult(AccessResult.Accepted);
        public Task<AccessResult> VerifyPin(string pin) => Task.FromResult(AccessResult.Accepted);
        public Task<AccessResult> CheckRecoveryCode(string code) => Task.FromResult(AccessResult.Accepted);
        public Task<string> CreatePin(string pin) => Task.FromResult("ABCD-EFGH-IJKL");
        public Task<AccessResult> ChangePin(string currentPin, string newPin) => Task.FromResult(AccessResult.Accepted);
        public void Lock() { }
        public void RegisterActivity() { }
        public void LockIfIdle() { }
    }
}
