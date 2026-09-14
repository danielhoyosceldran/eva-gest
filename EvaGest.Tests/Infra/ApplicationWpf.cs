using System.Windows;
using System.Windows.Threading;
using Xunit;

namespace EvaGest.Tests.Infra;

/// <summary>
/// A single WPF Application on a single STA thread, shared by every view test: WPF allows
/// only one Application per AppDomain, and views may only be built on an STA thread.
/// </summary>
public sealed class ApplicationWpf : IDisposable
{
    private readonly Thread _thread;
    private Dispatcher? _dispatcher;

    public ApplicationWpf()
    {
        var ready = new ManualResetEventSlim();

        _thread = new Thread(() =>
        {
            var app = new EvaGest.App();
            app.InitializeComponent();      // the real App.xaml, dictionaries and all

            _dispatcher = Dispatcher.CurrentDispatcher;
            ready.Set();
            Dispatcher.Run();
        }) { IsBackground = true };

        _thread.SetApartmentState(ApartmentState.STA);
        _thread.Start();
        ready.Wait();
    }

    /// <summary>Runs the body on the UI thread and rethrows its exception here.</summary>
    public void Runs(Action body)
    {
        Exception? failure = null;
        _dispatcher!.Invoke(() =>
        {
            try { body(); }
            catch (Exception e) { failure = e; }
        });
        if (failure is not null) throw failure;
    }

    public void Dispose() => _dispatcher?.InvokeShutdown();
}

[CollectionDefinition(Name)]
public class WpfCollection : ICollectionFixture<ApplicationWpf>
{
    public const string Name = "wpf";
}
