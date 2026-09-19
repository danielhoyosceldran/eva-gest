using System.Windows;
using System.Windows.Threading;
using Xunit;

namespace EvaGest.Tests.Infra;

/// <summary>
/// A single WPF Application on a single STA thread, shared by every view test: WPF allows
/// only one Application per AppDomain, and views may only be built on an STA thread.
///
/// Everything here is bounded by a timeout on purpose. This fixture is collection-scoped,
/// so anything that blocks in it blocks the whole run with no output and no failing test —
/// a test run that appears to hang forever rather than fail. Every wait below therefore
/// has a deadline and throws something that names what it was waiting for.
/// </summary>
public sealed class ApplicationWpf : IDisposable
{
    /// <summary>Generous for a dictionary parse (normally milliseconds), short enough that
    /// a broken App.xaml fails the run in seconds instead of hanging it.</summary>
    private static readonly TimeSpan StartupTimeout = TimeSpan.FromSeconds(30);

    private readonly Thread _thread;
    private Dispatcher? _dispatcher;

    public ApplicationWpf()
    {
        var ready = new ManualResetEventSlim();
        Exception? startupFailure = null;

        _thread = new Thread(() =>
        {
            try
            {
                var app = new EvaGest.App();
                app.InitializeComponent();      // the real App.xaml, dictionaries and all
                _dispatcher = Dispatcher.CurrentDispatcher;
            }
            catch (Exception e)
            {
                // Without this the exception died on the background thread, ready was
                // never set, and the constructor blocked forever on a run that could
                // never produce a single result.
                startupFailure = e;
            }
            finally
            {
                ready.Set();
            }

            if (startupFailure is null) Dispatcher.Run();
        }) { IsBackground = true, Name = "EvaGest WPF test UI thread" };

        _thread.SetApartmentState(ApartmentState.STA);
        _thread.Start();

        if (!ready.Wait(StartupTimeout))
            throw new TimeoutException(
                $"The WPF test Application did not start within {StartupTimeout.TotalSeconds:0} s. " +
                "Every view test depends on this fixture, so the run would otherwise hang here.");

        if (startupFailure is not null)
            throw new InvalidOperationException(
                "The WPF test Application failed to start; App.xaml or one of its resource " +
                "dictionaries could not be loaded. Every view test depends on it.", startupFailure);
    }

    /// <summary>
    /// Runs the body on the UI thread and rethrows its exception here.
    ///
    /// Deliberately a blocking Invoke. Replacing it with BeginInvoke + DispatcherOperation
    /// .Wait(timeout) to get a deadline was tried and is worse: the Wait did not honour its
    /// own timeout and wedged the whole run after the other 498 tests had passed. Invoke is
    /// the path WPF supports from a non-dispatcher thread; the deadline that actually
    /// matters is the startup one above, because that is the failure that produces no
    /// output at all.
    /// </summary>
    public void Runs(Action body)
    {
        if (_dispatcher is not { } dispatcher)
            throw new InvalidOperationException("The WPF test Application is not running.");

        Exception? failure = null;
        dispatcher.Invoke(() =>
        {
            try { body(); }
            catch (Exception e) { failure = e; }
        });
        if (failure is not null) throw failure;
    }

    /// <summary>
    /// Shuts the dispatcher down and waits for its thread to actually finish.
    ///
    /// InvokeShutdown on its own only asks. The thread is a background one, so the process
    /// could exit while it was still unwinding Dispatcher.Run and tearing down the visual
    /// trees the view tests built — which showed up once as "Test host process crashed"
    /// after every test had already passed. Joining makes the teardown ordered.
    /// </summary>
    public void Dispose()
    {
        if (_dispatcher is not { } dispatcher) return;

        dispatcher.InvokeShutdown();
        _thread.Join(TimeSpan.FromSeconds(10));
    }
}

[CollectionDefinition(Name)]
public class WpfCollection : ICollectionFixture<ApplicationWpf>
{
    public const string Name = "wpf";
}
