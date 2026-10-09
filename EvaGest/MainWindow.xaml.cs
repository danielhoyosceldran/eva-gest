using System;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using EvaGest.Services;
using EvaGest.ViewModels;

namespace EvaGest
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        // Once a minute is plenty for an hour-late threshold and cheap enough to poll
        // for the whole session; the UI-thread timer lives here, not in the ViewModel,
        // same rule as WeekGridView's now-line clock.
        private readonly DispatcherTimer _overdueCheck = new() { Interval = TimeSpan.FromMinutes(1) };

        // Owner mode's idle lock. Checked every 15 s, so it closes at most 15 s after
        // the 5-minute mark; the countdown itself lives in IOwnerAccessService.
        private readonly DispatcherTimer _idleCheck = new() { Interval = TimeSpan.FromSeconds(15) };

        public MainWindow()
        {
            InitializeComponent();
            _overdueCheck.Tick += async (_, _) => await RunOverdueCheck();
            _idleCheck.Tick += (_, _) => (DataContext as MainWindowViewModel)?.LockOwnerIfIdle();

            // PostProcessInput sees input for every window of the app, dialogs included,
            // so typing in a sale dialog still counts as activity for the idle lock.
            InputManager.Current.PostProcessInput += OnAnyInput;

            Loaded += async (_, _) =>
            {
                _overdueCheck.Start();
                _idleCheck.Start();
                await RunOverdueCheck();

                // No PIN yet: ask the owner to create one now that the window is up.
                if (DataContext is MainWindowViewModel vm)
                    await vm.EnsureOwnerPin();
            };
            Closing += OnClosing;
            Closed += (_, _) =>
            {
                _overdueCheck.Stop();
                _idleCheck.Stop();
                InputManager.Current.PostProcessInput -= OnAnyInput;
            };
        }

        /// <summary>
        /// Keys, clicks, wheel and touch restart the idle countdown. Plain mouse moves do
        /// not: WPF raises synthetic ones whenever layout shifts under a still cursor,
        /// which would keep owner mode open with nobody at the computer.
        /// </summary>
        private void OnAnyInput(object sender, ProcessInputEventArgs e)
        {
            if (e.StagingItem.Input is KeyEventArgs or MouseButtonEventArgs or MouseWheelEventArgs or TouchEventArgs
                && DataContext is MainWindowViewModel vm)
                vm.RegisterActivity();
        }

        // Set once the closing backup has run, so the second Close() below goes through.
        private bool _backupDoneOnClose;

        /// <summary>
        /// Holds the first close back until the day's automatic backup has run (F-01): the
        /// close is cancelled, the window is disabled with a wait cursor while the copy is
        /// taken, and then the window closes for real. A second click on the close button
        /// meanwhile is ignored. Skipped when the app is closing itself after a restore.
        /// </summary>
        private async void OnClosing(object? sender, CancelEventArgs e)
        {
            if (_backupDoneOnClose || AppShutdown.Requested || DataContext is not MainWindowViewModel vm)
                return;

            e.Cancel = true;
            if (!IsEnabled) return; // already backing up

            IsEnabled = false;
            Mouse.OverrideCursor = Cursors.Wait;
            try
            {
                await vm.RunBackupOnClose();
            }
            finally
            {
                Mouse.OverrideCursor = null;
                _backupDoneOnClose = true;
            }

            // Queued rather than called directly: when no backup is due, the await above
            // completes synchronously and we are still inside this Closing handler, where
            // WPF refuses a nested Close() (InvalidOperationException). Posting it runs it
            // once the cancelled close has fully unwound. Not awaited on purpose (the
            // discard says so, and keeps the build free of CS4014).
            _ = Dispatcher.BeginInvoke(Close);
        }

        private async Task RunOverdueCheck()
        {
            if (DataContext is MainWindowViewModel vm)
                await vm.CheckOverdueAppointments();
        }
    }
}
