using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using AwesomeAssertions;
using EvaGest.Services;
using EvaGest.Tests.Infra;
using EvaGest.Models;
using EvaGest.ViewModels.Elements;
using EvaGest.ViewModels.Pages;
using Xunit;

namespace EvaGest.Tests.Views;

/// <summary>
/// A XAML typo only shows up when the view is first navigated to, which in this app can
/// be several clicks deep. These tests parse the real dictionaries and the real views on
/// an STA thread so a broken StaticResource or binding path fails the build instead of
/// the user's afternoon.
/// </summary>
[Collection(WpfCollection.Name)]
public class XamlLoadTests(ApplicationWpf app)
{
    [Fact] // X-01
    public void The_resource_dictionaries_load()
        => app.Runs(() =>
        {
            Application.Current.Resources["PrimaryButton"].Should().NotBeNull();
            Application.Current.Resources["GridAppointment"].Should().NotBeNull();
            Application.Current.Resources["DateOnlyConverter"].Should().NotBeNull();
        });

    [Fact] // X-02
    public async Task The_weekly_grid_builds_and_fills_with_real_data()
    {
        await using var testDb = new TestDatabase();
        var factory = new TestFactory(testDb.Options);
        var vm = new WeekGridViewModel(
            new AppointmentService(factory), new AvailabilityService(factory), new SettingsService(factory),
            ModeGrid.Agenda, (_, _) => { });
        await vm.LoadWeek(new DateOnly(2026, 9, 7));

        app.Runs(() =>
        {
            var view = new EvaGest.Views.Elements.WeekGridView { DataContext = vm };

            // Measure/Arrange forces the templates to expand, which is what actually
            // resolves every StaticResource and MultiBinding inside them.
            view.Measure(new Size(1200, 800));
            view.Arrange(new Rect(0, 0, 1200, 800));

            view.ActualWidth.Should().BeGreaterThan(0);
        });
    }

    [Fact] // X-03
    public void The_agenda_view_builds()
        => app.Runs(() =>
        {
            var view = new EvaGest.Views.Pages.AgendaView();
            view.Measure(new Size(1200, 800));
            view.Arrange(new Rect(0, 0, 1200, 800));
        });

    [Fact] // X-04
    public void The_appointment_dialog_builds()
        => app.Runs(() =>
        {
            var view = new EvaGest.Views.Dialogs.AppointmentDialogView();
            view.Measure(new Size(1200, 800));
            view.Arrange(new Rect(0, 0, 1200, 800));
        });

    [Fact] // X-05
    public void The_settings_view_builds()
        => app.Runs(() =>
        {
            var view = new EvaGest.Views.Pages.SettingsView();
            view.Measure(new Size(1200, 800));
            view.Arrange(new Rect(0, 0, 1200, 800));
        });

    [Fact] // X-06
    public async Task The_workers_view_builds_with_real_data()
    {
        await using var testDb = new TestDatabase();
        var service = new WorkerService(new TestFactory(testDb.Options));
        await service.Create(Make.Worker(), new Dictionary<Weekday, List<(TimeOnly, TimeOnly)>>
        {
            [Weekday.Mon] = [(new TimeOnly(9, 0), new TimeOnly(14, 0))]
        });

        var vm = new WorkersViewModel(service, new TestDialogService());
        await vm.Load();

        app.Runs(() =>
        {
            var view = new EvaGest.Views.Pages.WorkersView { DataContext = vm };
            view.Measure(new Size(1200, 800));
            view.Arrange(new Rect(0, 0, 1200, 800));
            view.ActualWidth.Should().BeGreaterThan(0);
        });
    }

    [Fact] // X-08
    public async Task The_sales_view_builds_with_filters_and_footer()
    {
        await using var testDb = new TestDatabase();
        var factory = new TestFactory(testDb.Options);
        var config = new SettingsService(factory);
        await new SeedService(factory, config).Seed();

        var vm = new SalesViewModel(
            new SaleService(factory, config), new ClientService(factory), new CatalogService(factory),
            new WorkerService(factory), new TestSoundService(), config,
            new ExportService(factory), new TestDialogService());
        await vm.Load();

        app.Runs(() =>
        {
            var view = new EvaGest.Views.Pages.SalesView { DataContext = vm };
            view.Measure(new Size(1400, 800));
            view.Arrange(new Rect(0, 0, 1400, 800));
            view.ActualWidth.Should().BeGreaterThan(0);
        });
    }

    [Fact] // X-09
    public async Task The_agenda_view_draws_the_day_detail()
    {
        await using var testDb = new TestDatabase();
        var factory = new TestFactory(testDb.Options);
        var config = new SettingsService(factory);
        await new SeedService(factory, config).Seed();

        var day = new DateOnly(2026, 9, 7);
        await using (var db = testDb.Context())
        {
            db.Appointments.Add(new Appointment
            {
                Date = day, Time = new TimeOnly(10, 0), DurationMin = 30,
                GuestName = "Pere", Status = AppointmentStatus.Pending
            });
            await db.SaveChangesAsync();
        }

        var vm = new AgendaViewModel(
            new AppointmentService(factory), new SaleService(factory, config),
            new AvailabilityService(factory), new ClientService(factory),
            new CatalogService(factory), new WorkerService(factory), config,
            new TestSoundService(), new TestDialogService());
        await vm.Grid.LoadWeek(day);
        await vm.SelectDayCommand.ExecuteAsync(day);

        app.Runs(() =>
        {
            var view = new EvaGest.Views.Pages.AgendaView { DataContext = vm };
            view.Measure(new Size(1400, 800));
            view.Arrange(new Rect(0, 0, 1400, 800));
            view.ActualWidth.Should().BeGreaterThan(0);
        });
    }

    [Fact] // X-10
    public void The_datepicker_calendar_uses_the_app_templates()
        => app.Runs(() =>
        {
            var view = new EvaGest.Views.Dialogs.AppointmentDialogView();
            view.Measure(new Size(1200, 800));
            view.Arrange(new Rect(0, 0, 1200, 800));

            // DatePicker builds its popup Calendar in code and binds Calendar.Style to this
            // property, so an implicit Style TargetType="Calendar" never reaches it.
            var picker = Descendants<DatePicker>(view).First();
            picker.CalendarStyle.Should().NotBeNull();

            // Expanding the calendar's own templates is what resolves every StaticResource
            // and the day/month cell templates inside them.
            var calendar = new Calendar { Style = picker.CalendarStyle };
            calendar.Measure(new Size(600, 600));
            calendar.Arrange(new Rect(0, 0, 600, 600));

            var days = Descendants<CalendarDayButton>(calendar).ToList();
            days.Should().HaveCount(42);
            days.Should().AllSatisfy(d => d.ActualWidth.Should().Be(40));
        });

    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var fill = VisualTreeHelper.GetChild(root, i);
            if (fill is T found) yield return found;
            foreach (var net in Descendants<T>(fill)) yield return net;
        }
    }

    [Fact] // X-07
    public void The_worker_dialog_builds()
        => app.Runs(() =>
        {
            var view = new EvaGest.Views.Dialogs.WorkerDialogView
            {
                DataContext = new EvaGest.ViewModels.Dialogs.WorkerDialogViewModel([])
            };
            view.Measure(new Size(1200, 800));
            view.Arrange(new Rect(0, 0, 1200, 800));
        });
}
