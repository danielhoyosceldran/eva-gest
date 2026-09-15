using System;
using System.Threading.Tasks;
using System.Windows;
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

        public MainWindow()
        {
            InitializeComponent();
            _overdueCheck.Tick += async (_, _) => await RunOverdueCheck();

            Loaded += async (_, _) =>
            {
                _overdueCheck.Start();
                await RunOverdueCheck();
            };
            Closed += (_, _) => _overdueCheck.Stop();
        }

        private async Task RunOverdueCheck()
        {
            if (DataContext is MainWindowViewModel vm)
                await vm.CheckOverdueAppointments();
        }
    }
}
