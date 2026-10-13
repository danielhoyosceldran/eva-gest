using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using AwesomeAssertions;
using EvaGest.Models;
using EvaGest.Services;
using EvaGest.Tests.Infra;
using EvaGest.ViewModels.Pages;
using EvaGest.Views.Pages;
using Xunit;

namespace EvaGest.Tests.Views;

/// <summary>
/// Manual test C10: answering "no" to the VAT mode change kept the mode, but the picker
/// stayed on "Preus sense IVA" until the page was left. The ViewModel put the mode back
/// while the ComboBox was still applying the user's choice, and the ComboBox ignores a
/// change made in the middle of its own. Checked on the real view, since the ViewModel
/// alone was always right.
/// </summary>
[Collection(WpfCollection.Name)]
public class VatModeSelectorTests(ApplicationWpf app)
{
    [Fact]
    public async Task Saying_no_to_the_vat_mode_change_puts_the_picker_back_where_it_was()
    {
        await using var db = new TestDatabase();
        var f = new TestFactory(db.Options);
        var dialogs = new ModalDialogService();
        var vm = new SettingsViewModel(new BackupService(new AppPaths("live.db", "."), new TestSettings()),
            new ExportService(f), new SettingsService(f), new AvailabilityService(f), dialogs,
            await TestOwner.Unlocked());
        await vm.Load();

        object? shown = null, selected = null;
        app.Runs(() =>
        {
            var view = new SettingsView { DataContext = vm };
            // A real window, far off screen and never activated: the picker's list is a
            // popup, which only exists inside one. The test Application must not get it
            // as its MainWindow (see ResponsiveLayoutTests.UnshownMainWindow).
            var previous = Application.Current.MainWindow;
            var window = new Window
            {
                Content = view, Width = 1200, Height = 900, Left = -20000, Top = -20000,
                WindowStartupLocation = WindowStartupLocation.Manual, ShowActivated = false, ShowInTaskbar = false
            };
            Application.Current.MainWindow = previous;
            window.Show();
            try
            {
            PumpOnce();
            var picker = Descendants<ComboBox>(view).Single(c => ReferenceEquals(c.ItemsSource, vm.ModesVat));

            // The user opens the list and clicks the other mode
            picker.IsDropDownOpen = true;
            for (int i = 0; i < 5 && picker.ItemContainerGenerator.ContainerFromItem(VatMode.NotIncluded) is null; i++)
            {
                PumpOnce();
                picker.UpdateLayout();
            }
            var item = (ComboBoxItem)picker.ItemContainerGenerator.ContainerFromItem(VatMode.NotIncluded);
            item.IsSelected = true;
            picker.IsDropDownOpen = false;
            Pump(() => vm.VatModeChange.IsCompleted);
            view.UpdateLayout();
            // What the closed picker shows, not just what it holds
            shown = picker.SelectionBoxItem;
            selected = picker.SelectedItem;
            }
            finally { window.Close(); Application.Current.MainWindow = previous; }
        });

        dialogs.Questions.Should().Be(1);
        vm.ModeVat.Should().Be(VatMode.Included);
        selected.Should().Be(VatMode.Included);
        shown.Should().Be(VatMode.Included, "the picker must show the mode that is still in force");
    }

    /// <summary>Answers "no" the way a real modal window does: only after running a
    /// message loop of its own, in the middle of whatever raised the question.</summary>
    private sealed class ModalDialogService : TestDialogService, IDialogService
    {
        public int Questions { get; private set; }

        Task<bool> IDialogService.Confirm(string title, string message, string textConfirm, string? textCancel)
        {
            Questions++;
            PumpOnce();
            return Task.FromResult(false);
        }
    }

    /// <summary>Runs the dispatcher until <paramref name="done"/> holds, plus once more
    /// so work queued at the end (the picker's own update) runs too.</summary>
    private static void Pump(Func<bool> done)
    {
        for (int i = 0; i < 200 && !done(); i++) PumpOnce();
        PumpOnce();
    }

    private static void PumpOnce()
    {
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, () => frame.Continue = false);
        Dispatcher.PushFrame(frame);
    }

    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T match) yield return match;
            foreach (var d in Descendants<T>(child)) yield return d;
        }
    }
}
