using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using AwesomeAssertions;
using EvaGest.Services;
using EvaGest.Tests.Infra;
using EvaGest.ViewModels.Dialogs;
using EvaGest.Views.Dialogs;
using EvaGest.Views.Elements;
using Xunit;

namespace EvaGest.Tests.Views;

/// <summary>
/// With one or two workers, the sale offers one button per worker instead of the search
/// box and its list: there is nothing to search. A third worker brings the search box
/// back. Only one button is chosen at a time, clicking it again leaves the sale without
/// a worker (it is optional), and a name never gets cut: buttons that do not fit side by
/// side go one under the other, and a name too long for the column wraps.
/// </summary>
[Collection(WpfCollection.Name)]
public class SaleWorkerButtonsTests(ApplicationWpf app)
{
    private static async Task<SaleDialogViewModel> ASale(TestDatabase testDb, params string[] workers)
    {
        var f = new TestFactory(testDb.Options);
        var settings = new SettingsService(f);
        await using (var db = testDb.Context())
        {
            db.PaymentMethods.Add(Make.Method());
            foreach (var name in workers) db.Workers.Add(Make.Worker(name));
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
        return await SaleDialogViewModel.New(new SaleService(f, settings), new ClientService(f),
            new CatalogService(f), new WorkerService(f), new TestSoundService(), settings, new TestDialogService());
    }

    [Fact]
    public async Task One_or_two_workers_are_buttons_and_a_third_brings_the_search_back()
    {
        await using (var one = new TestDatabase())
            (await ASale(one, "Marta")).ShowsWorkerButtons.Should().BeTrue();
        await using (var two = new TestDatabase())
            (await ASale(two, "Marta", "Laia")).ShowsWorkerButtons.Should().BeTrue();
        await using (var three = new TestDatabase())
            (await ASale(three, "Marta", "Laia", "Pau")).ShowsWorkerButtons.Should().BeFalse();
        await using (var none = new TestDatabase())
            (await ASale(none)).ShowsWorkerButtons.Should().BeFalse("with no worker there is no button to show");
    }

    /// <summary>The sale dialog laid out at its own width, its bindings resolved.</summary>
    private static SaleDialogView Laid(SaleDialogViewModel vm)
    {
        var view = new SaleDialogView { DataContext = vm };
        view.Measure(new Size(800, 1000));
        view.Arrange(new Rect(0, 0, 800, 1000));
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, () => frame.Continue = false);
        Dispatcher.PushFrame(frame);
        view.UpdateLayout();
        return view;
    }

    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T match) yield return match;
            foreach (var deeper in Descendants<T>(child)) yield return deeper;
        }
    }

    private static ListBoxItem Button(ListBox list, int index)
        => (ListBoxItem)list.ItemContainerGenerator.ContainerFromIndex(index);

    /// <summary>The button of the worker with that name (the list comes sorted by name).</summary>
    private static ListBoxItem Button(ListBox list, string name)
        => (ListBoxItem)list.ItemContainerGenerator.ContainerFromItem(
            list.Items.Cast<EvaGest.Models.Worker>().Single(w => w.Name == name));

    /// <summary>
    /// The press of a click on a button's name, as the mouse delivers it: a tunnelling
    /// PreviewMouseDown, which each element on the way turns into its own
    /// PreviewMouseLeftButtonDown. Only the press: that is where clicking the chosen
    /// worker again is caught, and a ListBoxItem outside a real window does not select
    /// itself on a synthetic one.
    /// </summary>
    private static bool PressOn(ListBoxItem item)
    {
        var press = new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left)
        {
            RoutedEvent = Mouse.PreviewMouseDownEvent
        };
        Descendants<TextBlock>(item).First().RaiseEvent(press);
        return press.Handled;
    }

    [Fact]
    public async Task Clicking_a_worker_chooses_them_and_clicking_them_again_leaves_the_sale_without_one()
    {
        await using var testDb = new TestDatabase();
        var vm = await ASale(testDb, "Marta", "Laia");

        app.Runs(() =>
        {
            var view = Laid(vm);
            var list = (ListBox)view.FindName("WorkerButtons");
            list.Visibility.Should().Be(Visibility.Visible);
            var picker = Descendants<SearchPickerView>(view).First(p => p.DataContext == vm.WorkerPicker);
            ((UIElement)VisualTreeHelper.GetParent(picker)).Visibility
                .Should().Be(Visibility.Collapsed, "the search box gives way to the buttons");

            PressOn(Button(list, "Laia")).Should().BeFalse("a worker not chosen yet is left to select itself");
            Button(list, "Laia").IsSelected = true;
            vm.Worker!.Name.Should().Be("Laia");

            Button(list, "Marta").IsSelected = true;
            vm.Worker!.Name.Should().Be("Marta", "only one is chosen at a time");
            Button(list, "Laia").IsSelected.Should().BeFalse();

            PressOn(Button(list, "Marta")).Should().BeTrue();
            vm.Worker.Should().BeNull("clicking the chosen worker again clears the choice");
            list.SelectedItem.Should().BeNull();
        });
    }

    [Fact]
    public async Task A_long_name_is_never_cut_the_buttons_go_one_under_the_other_instead()
    {
        await using var testDb = new TestDatabase();
        var vm = await ASale(testDb, "Maria Dolors Fernández-Puigdollers", "Laia");

        app.Runs(() =>
        {
            var view = Laid(vm);
            var list = (ListBox)view.FindName("WorkerButtons");
            var first = Button(list, 0);
            var second = Button(list, 1);

            foreach (var text in Descendants<TextBlock>(list))
            {
                // Laid out at a width that fits it whole: nothing hidden or trimmed
                text.TextTrimming.Should().Be(TextTrimming.None);
                text.DesiredSize.Width.Should().BeLessThanOrEqualTo(list.ActualWidth, "{0} must fit the column", text.Text);
            }
            second.TranslatePoint(new Point(0, 0), list).Y
                .Should().BeGreaterThan(first.TranslatePoint(new Point(0, 0), list).Y, "the second one wraps under the first");
            LayoutInformation.GetLayoutClip(first).Should().BeNull("the button is not clipped");
        });
    }
}
