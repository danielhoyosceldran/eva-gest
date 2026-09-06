using System.Windows;
using System.Windows.Threading;
using Xunit;

namespace EvaGest.Tests.Infra;

/// <summary>
/// A single WPF Application on a single STA thread, shared by every view test: WPF allows
/// only one Application per AppDomain, and views may only be built on an STA thread.
/// </summary>
public sealed class AplicacioWpf : IDisposable
{
    private readonly Thread _fil;
    private Dispatcher? _dispatcher;

    public AplicacioWpf()
    {
        var llest = new ManualResetEventSlim();

        _fil = new Thread(() =>
        {
            var app = new EvaGest.App();
            app.InitializeComponent();      // the real App.xaml, dictionaries and all

            _dispatcher = Dispatcher.CurrentDispatcher;
            llest.Set();
            Dispatcher.Run();
        }) { IsBackground = true };

        _fil.SetApartmentState(ApartmentState.STA);
        _fil.Start();
        llest.Wait();
    }

    /// <summary>Runs the body on the UI thread and rethrows its exception here.</summary>
    public void Executa(Action cos)
    {
        Exception? fallada = null;
        _dispatcher!.Invoke(() =>
        {
            try { cos(); }
            catch (Exception e) { fallada = e; }
        });
        if (fallada is not null) throw fallada;
    }

    public void Dispose() => _dispatcher?.InvokeShutdown();
}

[CollectionDefinition(Nom)]
public class ColleccioWpf : ICollectionFixture<AplicacioWpf>
{
    public const string Nom = "wpf";
}
