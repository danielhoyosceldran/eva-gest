using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using AwesomeAssertions;
using EvaGest.Helpers;
using EvaGest.Models;
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
///   E 1920x1032 (1920x1080 at 100 %)
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
        ("A", 1024, 720), ("B", 1093, 566), ("C", 1280, 672), ("D", 1366, 720), ("E", 1920, 1032)
    ];

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
        ["Service"] = _ => Task.FromResult(new Built(new ServiceDialogViewModel(), () => new ServiceDialogView())),
        ["Product"] = _ => Task.FromResult(new Built(new ProductDialogViewModel(), () => new ProductDialogView())),
        ["Category"] = _ => Task.FromResult(new Built(new CategoryDialogViewModel(), () => new CategoryDialogView())),
        ["Payment method"] = _ => Task.FromResult(new Built(new PaymentMethodDialogViewModel(), () => new PaymentMethodDialogView())),
        ["Owner unlock"] = s => Task.FromResult(new Built(
            new OwnerUnlockDialogViewModel(new OwnerAccessService(new SettingsService(s.Factory))),
            () => new OwnerUnlockDialogView())),
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
                Layout(host, cap, sizeToContent: true);

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

    // ───────────────────────────── pages ─────────────────────────────

    private static readonly Dictionary<string, Func<WorstCaseShop, Task<Built>>> PageBuilders = new()
    {
        ["Home, owner mode"] = async s =>
        {
            var f = s.Factory;
            var settings = new SettingsService(f);
            var vm = new HomeViewModel(new AppointmentService(f), new SaleService(f, settings), new TillService(f),
                new ClientService(f), new AvailabilityService(f), new CatalogService(f), new WorkerService(f),
                settings, new TestSoundService(), new TestDialogService(), new UnlockedOwner());
            await vm.Load();
            return new(vm, () => new HomeView());
        },
        ["Agenda"] = async s => new(await Agenda(s, openDay: false), () => new AgendaView()),
        ["Agenda, week and day panel"] = async s => new(await Agenda(s, openDay: true), () => new AgendaView()),
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

    /// <summary>The agenda in the week view (its widest), optionally with today's
    /// detail panel open beside it — the narrowest the week grid ever gets.</summary>
    private static async Task<AgendaViewModel> Agenda(WorstCaseShop s, bool openDay)
    {
        var f = s.Factory;
        var settings = new SettingsService(f);
        var vm = new AgendaViewModel(new AppointmentService(f), new SaleService(f, settings), new AvailabilityService(f),
            new ClientService(f), new CatalogService(f), new WorkerService(f), settings, new TestSoundService(),
            new TestDialogService());
        await vm.Load();
        if (vm.Grid.IsThreeDayView) await vm.Grid.ToggleViewCommand.ExecuteAsync(null);
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
            foreach (var screen in Screens)
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
    private static void Layout(FrameworkElement element, Size size, bool sizeToContent)
    {
        for (int i = 0; i < 3; i++)
        {
            element.Measure(size);
            var final = sizeToContent
                ? new Size(Math.Min(element.DesiredSize.Width, size.Width), Math.Min(element.DesiredSize.Height, size.Height))
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
    /// - a flexible table column squeezed below its minimum width.
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

            bool checkable = element is TextBlock or ButtonBase or ComboBox or TextBox or DatePicker or PasswordBox;
            if (!checkable || element.TemplatedParent is TextBox or ComboBox or DatePicker or DatePickerTextBox) continue;

            var clip = LayoutInformation.GetLayoutClip(element);
            if (clip is not null && !element.ClipToBounds && !clip.Bounds.IsEmpty
                && (clip.Bounds.Width + Tolerance < element.ActualWidth || clip.Bounds.Height + Tolerance < element.ActualHeight))
                found.Add($"{Describe(element)} cut to {clip.Bounds.Width:0}x{clip.Bounds.Height:0} of {element.ActualWidth:0}x{element.ActualHeight:0}");

            if (!InsideScrollingArea(element, root))
            {
                var bounds = element.TransformToAncestor(root)
                    .TransformBounds(new Rect(0, 0, element.ActualWidth, element.ActualHeight));
                if (bounds.Right > root.ActualWidth + Tolerance || bounds.Bottom > root.ActualHeight + Tolerance)
                    found.Add($"{Describe(element)} pushed off screen to {bounds.Right:0},{bounds.Bottom:0} " +
                              $"(room {root.ActualWidth:0}x{root.ActualHeight:0})");
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
