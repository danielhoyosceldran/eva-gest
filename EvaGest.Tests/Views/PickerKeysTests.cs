using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using AwesomeAssertions;
using EvaGest.Models;
using EvaGest.Tests.Infra;
using EvaGest.ViewModels.Elements;
using EvaGest.Views.Elements;
using Xunit;

namespace EvaGest.Tests.Views;

/// <summary>
/// B-32: once Esc had closed a search box's list, a second Esc no longer closed the
/// dialog. The box took Esc and Enter through KeyBindings whose commands are disabled
/// while the list is closed, meaning to let the keys through; but a KeyBinding marks the
/// key handled whenever it matches, enabled or not, so the dialog never saw them. The key
/// is sent as the keyboard would, through the box's real event route.
/// </summary>
[Collection(WpfCollection.Name)]
public class PickerKeysTests(ApplicationWpf app)
{
    /// <summary>Shows <paramref name="content"/> in a real window far off screen (a key
    /// event needs a PresentationSource) and runs <paramref name="test"/> on it.</summary>
    private static void InWindow(FrameworkElement content, Action test)
    {
        var previous = Application.Current.MainWindow;
        var window = new Window
        {
            Content = content, Width = 500, Height = 300, Left = -20000, Top = -20000,
            WindowStartupLocation = WindowStartupLocation.Manual, ShowActivated = false, ShowInTaskbar = false
        };
        Application.Current.MainWindow = previous;
        window.Show();
        try { test(); }
        finally { window.Close(); Application.Current.MainWindow = previous; }
    }

    /// <summary>Sends <paramref name="key"/> to <paramref name="box"/> and says whether
    /// anything on the way took it, which keeps it from the dialog.</summary>
    private static bool Press(TextBox box, Key key)
    {
        var source = PresentationSource.FromVisual(box)!;
        var preview = new KeyEventArgs(Keyboard.PrimaryDevice, source, 0, key) { RoutedEvent = Keyboard.PreviewKeyDownEvent };
        box.RaiseEvent(preview);
        if (preview.Handled) return true;
        var down = new KeyEventArgs(Keyboard.PrimaryDevice, source, 0, key) { RoutedEvent = Keyboard.KeyDownEvent };
        box.RaiseEvent(down);
        return down.Handled;
    }

    private static TextBox Box(DependencyObject view)
        => (TextBox)((FrameworkElement)view).FindName("Box");

    [Fact]
    public void A_search_box_lets_esc_and_enter_through_to_the_dialog_once_its_list_is_closed()
        => app.Runs(() =>
        {
            var workers = new List<Worker> { new() { Id = 1, Name = "Marta Puig" }, new() { Id = 2, Name = "Maria Soler" } };
            var picker = new SearchPickerViewModel<Worker>(workers, w => w.Name, _ => { }, null, "Triar treballadora");
            var view = new SearchPickerView { DataContext = picker };

            InWindow(view, () =>
            {
                picker.Text = "Ma";
                picker.IsDropdownOpen.Should().BeTrue("precondition: typing opened the list");

                Press(Box(view), Key.Escape).Should().BeTrue("the first Esc closes the list");
                picker.IsDropdownOpen.Should().BeFalse();

                Press(Box(view), Key.Escape).Should().BeFalse("the second Esc is the dialog's");
                Press(Box(view), Key.Enter).Should().BeFalse("Enter is the dialog's while the list is closed");
            });
        });

    [Fact]
    public void The_client_box_lets_esc_and_enter_through_to_the_dialog_once_its_list_is_closed()
        => app.Runs(() =>
        {
            var picker = new ClientPickerViewModel();
            picker.SetClients([new Client { Id = 1, Name = "Joan García", Mobile = "612345678" },
                               new Client { Id = 2, Name = "Joana Vidal", Mobile = "622345678" }]);
            var view = new ClientPickerView { DataContext = picker };

            InWindow(view, () =>
            {
                picker.Text = "Joa";
                picker.IsDropdownOpen.Should().BeTrue("precondition: typing opened the list");

                Press(Box(view), Key.Down).Should().BeTrue("the arrows move along the list");
                Press(Box(view), Key.Escape).Should().BeTrue("the first Esc closes the list");
                picker.IsDropdownOpen.Should().BeFalse();

                Press(Box(view), Key.Escape).Should().BeFalse("the second Esc is the dialog's");
                Press(Box(view), Key.Enter).Should().BeFalse("Enter is the dialog's while the list is closed");
            });
        });
}
