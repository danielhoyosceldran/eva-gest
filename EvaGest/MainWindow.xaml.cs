using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
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

        private async Task RunOverdueCheck()
        {
            if (DataContext is MainWindowViewModel vm)
                await vm.CheckOverdueAppointments();
        }
    }
}
