using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Markup;
using EvaGest.Models;
using EvaGest.Services;
using EvaGest.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using EvaGest.Resources;

namespace EvaGest;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    private static Mutex? _instance;

    /// <summary>Keeps the unhandled-error message from repeating (E-10).</summary>
    private readonly Helpers.ErrorNoticeGate _errorNotices = new(Helpers.ErrorNoticeGate.DefaultQuiet);

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // WPF's Application constructor queues OnStartup on the dispatcher, so it runs in
        // any process that builds an App and pumps messages, not only in EvaGest.exe. The
        // view tests do exactly that to load App.xaml's resources, and used to get the whole
        // startup below: the single-instance mutex (with EvaGest open, a Shutdown that left
        // every view test without an Application), the user's real log file, a migration
        // of the real database and a MainWindow. Only the app's own process starts the app.
        if (System.Reflection.Assembly.GetEntryAssembly() != typeof(App).Assembly)
            return;

        Helpers.SmoothScroll.Activate();

        // Any exception that escapes a command handler would otherwise crash to the
        // default WPF dialog, which shows a raw stack trace (disseny-ui 9: "sense
        // disculpes ni tecnicismes"). Log the real cause, tell the user something plain.
        DispatcherUnhandledException += (_, args) =>
        {
            args.Handled = true;

            // No main window on screen: nothing the user could carry on with. Marking it
            // handled and staying up left an invisible process holding the single-instance
            // mutex (E-10), so the app says so and closes instead.
            if (MainWindow is not { IsLoaded: true })
            {
                Log.Fatal(args.Exception, "Unhandled exception with no main window open; closing");
                MessageBox.Show(Texts.UnexpectedErrorMessage, Texts.UnexpectedErrorTitle,
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                Shutdown();
                return;
            }

            Log.Error(args.Exception, "Unhandled exception");

            // Always logged, but told once: not again while a box is up, nor for the same
            // failure repeating within a minute (a timer or a binding hitting it each time).
            if (!_errorNotices.TryEnter(args.Exception, DateTime.Now)) return;
            try
            {
                MessageBox.Show(Texts.UnexpectedErrorMessage, Texts.UnexpectedErrorTitle,
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            finally
            {
                _errorNotices.Exit();
            }
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
            // Show the copy that is already open rather than vanish without a word. The
            // log is not configured in this process on purpose: the open copy holds the
            // day's log file. The message is in the system language, since this copy never
            // opens the database the chosen language is stored in.
            if (!Helpers.SingleInstance.ActivateExisting())
                MessageBox.Show(Texts.AlreadyRunningMessage, Texts.AlreadyRunningTitle,
                    MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        // Guarded like the steps below (E-02). A missing or broken appsettings.json, or a
        // data folder that cannot be created, used to escape to DispatcherUnhandledException:
        // it was logged to a logger not configured yet (so nowhere), marked handled, and
        // with no window ever opened the process stayed alive holding the mutex, so every
        // later launch said EvaGest was already open.
        StartupFolders.Layout layout;
        try
        {
            // Expand %LOCALAPPDATA% and make sure the folders exist on first run
            layout = StartupFolders.Prepare(AppContext.BaseDirectory);

            // Never pruned. The log is part of the audit trail CU-04 asks for (sale edits and
            // voids, client deletions, cash movements), and the books it backs up have to be
            // kept for years; 30 files used to erase that record a month later. One small
            // text file a day costs next to nothing. The durable copy of each change lives
            // in the audit_entries table, which also travels inside every backup.
            Log.Logger = new LoggerConfiguration()
                .WriteTo.File(Path.Combine(layout.LogsFolder, "log-.txt"),
                              rollingInterval: RollingInterval.Day,
                              retainedFileCountLimit: null)
                .CreateLogger();
        }
        catch (Exception ex)
        {
            StartupFolders.RecordEarlyFailure(ex);
            MessageBox.Show(Texts.StartupFailedMessage, Texts.StartupFailedTitle,
                MessageBoxButton.OK, MessageBoxImage.Warning);
            Shutdown();
            return;
        }

        Log.Information("Application started");

        string dbPath = layout.DbPath;
        string baseFolder = layout.BaseFolder;
        Services = Configure(dbPath, baseFolder);

        // A corrupt or locked database must never surface as a raw exception (RF-18)
        try
        {
            await PrepareDatabase();
        }
        catch (BackupBeforeUpdateFailedException ex)
        {
            // Nothing was migrated: the database is exactly as the previous version left
            // it. Say what actually happened instead of calling the file unreadable.
            Log.Fatal(ex, "Could not back up the database before migrating it; nothing was migrated");
            MessageBox.Show(Texts.BackupBeforeUpdateFailedMessage, Texts.BackupBeforeUpdateFailedTitle,
                MessageBoxButton.OK, MessageBoxImage.Warning);
            Shutdown();
            return;
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Could not open the database");
            MessageBox.Show(Texts.DatabaseUnreadableMessage, Texts.DatabaseUnreadableTitle,
                MessageBoxButton.OK, MessageBoxImage.Warning);
            Shutdown();
            return;
        }

        // Everything from here to the first window is guarded too. An exception in it used
        // to reach DispatcherUnhandledException, which shows its message and marks it
        // handled; but with no window ever opened the process then stayed alive, invisible,
        // holding the single-instance mutex, so every later launch said EvaGest was
        // already open until someone ended it in Task Manager.
        try
        {
            await OpenShell();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Startup failed after the database was opened");
            MessageBox.Show(Texts.StartupFailedMessage, Texts.StartupFailedTitle,
                MessageBoxButton.OK, MessageBoxImage.Warning);
            Shutdown();
        }
    }

    /// <summary>The steps of startup that come after the database is ready: the
    /// interface language, the daily automatic backup and the main window.</summary>
    private static async Task OpenShell()
    {
        // Before the first window is built: every view resolves its texts through
        // {x:Static} as it loads, so the language has to be settled by now (RF-23).
        AppLanguage.Use(AppLanguage.Parse(
            await Services.GetRequiredService<ISettingsService>().Get(ConfigKeys.Language)));

        // Bindings format with FrameworkElement.Language, which is en-US unless set,
        // whatever the thread culture says. Without this a StringFormat like dd/MM/yyyy
        // or HH:mm took its separators from en-US instead of AppLanguage.Culture.
        FrameworkElement.LanguageProperty.OverrideMetadata(typeof(FrameworkElement),
            new FrameworkPropertyMetadata(XmlLanguage.GetLanguage(AppLanguage.Culture.IetfLanguageTag)));

        // The machine may have been off at the configured hour, so this is checked
        // at every startup instead of relying on a running timer (CU-11). A failure
        // (locked/unwritable backup folder, full disk, a copy that fails its check...)
        // does not stop the app from opening: the shell catches it, logs it and keeps a
        // notice up until a good copy is taken.
        var shell = Services.GetRequiredService<MainWindowViewModel>();
        await shell.RunAutomaticBackup();

        new MainWindow { DataContext = shell }.Show();
    }

    /// <summary>The wiring lives in <see cref="AppComposition"/>, where the tests run it too.</summary>
    private static ServiceProvider Configure(string dbPath, string baseFolder)
        => AppComposition.Register(new ServiceCollection(), dbPath, baseFolder).BuildServiceProvider();

    private static Task PrepareDatabase() => AppComposition.PrepareDatabase(Services);

    protected override void OnExit(ExitEventArgs e)
    {
        Log.Information("Application closed");
        Log.CloseAndFlush();
        base.OnExit(e);
    }
}
