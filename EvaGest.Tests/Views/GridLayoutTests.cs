using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using AwesomeAssertions;
using EvaGest.Models;
using EvaGest.Services;
using EvaGest.Tests.Infra;
using EvaGest.ViewModels.Elements;
using EvaGest.Views.Elements;
using Xunit;

namespace EvaGest.Tests.Views;

/// <summary>
/// Real layout arithmetic: these arrange the grid and compare where the hour ruler
/// actually puts "10:00" against where an appointment at 10:00 actually lands. Pure
/// ViewModel tests cannot catch a misalignment that WPF introduces during arrange.
/// </summary>
[Collection(WpfCollection.Name)]
public class GridLayoutTests(ApplicationWpf app)
{
    private static readonly DateOnly Monday = new(2026, 9, 7);

    private static async Task<WeekGridViewModel> Build(TestDatabase testDb, int opens, int closes)
    {
        await using (var db = testDb.Context())
        {
            for (int d = 0; d < 7; d++)
                db.ShopSchedule.Add(new ShopSchedule
                {
                    Weekday = (Weekday)d,
                    OpeningTime = new TimeOnly(opens, 0),
                    ClosingTime = new TimeOnly(closes, 0)
                });
            db.Appointments.Add(new Appointment
            {
                Date = Monday, Time = new TimeOnly(10, 0), DurationMin = 30, GuestName = "Anna"
            });
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var factory = new TestFactory(testDb.Options);
        var vm = new WeekGridViewModel(
            new AppointmentService(factory), new AvailabilityService(factory), new SettingsService(factory),
            ModeGrid.Agenda, (_, _) => { });
        await vm.LoadRange(Monday);
        return vm;
    }

    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        int n = VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < n; i++)
        {
            var fill = VisualTreeHelper.GetChild(root, i);
            if (fill is T found) yield return found;
            foreach (var net in Descendants<T>(fill)) yield return net;
        }
    }

    /// <summary>Absolute Y of an element relative to the whole control.</summary>
    private static double YAbsolute(Visual element, Visual root)
        => element.TransformToAncestor(root).Transform(new Point(0, 0)).Y;

    private static (double yRuler, double yAppointment) Positions(WeekGridViewModel vm, double heightWindow)
    {
        var view = new WeekGridView { DataContext = vm };
        view.Measure(new Size(1200, heightWindow));
        view.Arrange(new Rect(0, 0, 1200, heightWindow));
        view.UpdateLayout();

        var label = Descendants<TextBlock>(view).First(t => t.Text == "10:00");

        // The appointment block is the only Button carrying a GridAppointmentViewModel
        var block = Descendants<Button>(view).First(b => b.DataContext is GridAppointmentViewModel);

        return (YAbsolute(label, view), YAbsolute(block, view));
    }

    [Fact] // L-01
    public async Task The_ruler_and_the_appointments_align_when_the_grid_does_not_fit_the_window()
    {
        await using var testDb = new TestDatabase();
        var vm = await Build(testDb, 9, 20);          // 11 h x 88 px/h = 968 px

        double yRuler = 0, yAppointment = 0;
        app.Runs(() => (yRuler, yAppointment) = Positions(vm, 600));

        // The label is centred on the line (margin -8); the appointment starts on it
        (yAppointment - yRuler).Should().BeApproximately(8, 1.5);
    }

    [Fact] // L-02
    public async Task The_ruler_and_the_appointments_align_even_when_the_grid_has_a_remainder()
    {
        // Short week: 9-12 = 3 h x 88 = 264 px inside a 900 px window. This is the case
        // that used to leave the hour out of line with the appointment.
        await using var testDb = new TestDatabase();
        var vm = await Build(testDb, 9, 12);

        double yRuler = 0, yAppointment = 0;
        app.Runs(() => (yRuler, yAppointment) = Positions(vm, 900));

        (yAppointment - yRuler).Should().BeApproximately(8, 1.5);
    }

    [Fact] // L-03
    public async Task The_columns_do_not_stretch_past_the_visible_schedule()
    {
        await using var testDb = new TestDatabase();
        var vm = await Build(testDb, 9, 12);

        app.Runs(() =>
        {
            var view = new WeekGridView { DataContext = vm };
            view.Measure(new Size(1200, 900));
            view.Arrange(new Rect(0, 0, 1200, 900));
            view.UpdateLayout();

            var panel = (ItemsControl)view.FindName("DaysPanel")!;
            panel.ActualHeight.Should().BeApproximately(vm.TotalHeightPx, 1);
        });
    }

    [Fact] // L-05
    public async Task The_seven_columns_fit_the_dialog_picker_before_and_after_loading()
    {
        // The dialog is SizeToContent: it measures once, while the grid is still
        // loading. If the picker's space is not reserved from the first moment, the
        // window opens too narrow to show the whole week.
        await using var testDb = new TestDatabase();
        var factory = new TestFactory(testDb.Options);
        var vm = new EvaGest.ViewModels.Dialogs.AppointmentDialogViewModel(
            new AppointmentService(factory), new AvailabilityService(factory), new ClientService(factory),
            new CatalogService(factory), new WorkerService(factory), new SettingsService(factory),
            new TestDialogService(), Monday);

        app.Runs(() =>
        {
            var view = new EvaGest.Views.Dialogs.AppointmentDialogView { DataContext = vm };
            view.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

            // 420 for the form + 20 of margin + 700 for the picker
            view.DesiredSize.Width.Should().BeGreaterThanOrEqualTo(1140);
        });

        await vm.Initialization;
        vm.Grid!.Days.Should().HaveCount(3, "the picker opens on the default three-day view");
    }

    [Fact] // L-06
    public async Task The_dialog_picker_moves_by_one_day_and_jumps_by_three()
    {
        await using var testDb = new TestDatabase();
        var factory = new TestFactory(testDb.Options);
        var vm = new EvaGest.ViewModels.Dialogs.AppointmentDialogViewModel(
            new AppointmentService(factory), new AvailabilityService(factory), new ClientService(factory),
            new CatalogService(factory), new WorkerService(factory), new SettingsService(factory),
            new TestDialogService(), Monday);
        await vm.Initialization;

        var grid = vm.Grid!;
        grid.RangeText.Should().NotBeEmpty();

        grid.RangeStart.Should().Be(Monday, "three days start on the appointment's own date");

        await grid.StepForwardCommand.ExecuteAsync(null);
        grid.RangeStart.Should().Be(Monday.AddDays(1));

        await grid.JumpForwardCommand.ExecuteAsync(null);
        grid.RangeStart.Should().Be(Monday.AddDays(4));

        await grid.JumpBackCommand.ExecuteAsync(null);
        await grid.StepBackCommand.ExecuteAsync(null);
        grid.RangeStart.Should().Be(Monday);
    }

    [Fact] // L-04
    public async Task The_hour_label_does_not_invade_the_first_column()
    {
        await using var testDb = new TestDatabase();
        var vm = await Build(testDb, 9, 20);

        app.Runs(() =>
        {
            var view = new WeekGridView { DataContext = vm };
            view.Measure(new Size(1200, 600));
            view.Arrange(new Rect(0, 0, 1200, 600));
            view.UpdateLayout();

            var label = Descendants<TextBlock>(view).First(t => t.Text == "10:00");
            double right = label.TransformToAncestor(view)
                .Transform(new Point(label.ActualWidth, 0)).X;

            right.Should().BeLessThanOrEqualTo(vm.RulerWidthPx);
        });
    }
}
