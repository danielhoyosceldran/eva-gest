using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using AwesomeAssertions;
using EvaGest.Models;
using EvaGest.Resources;
using EvaGest.Services;
using EvaGest.Tests.Infra;
using EvaGest.ViewModels.Dialogs;
using EvaGest.Views.Dialogs;
using Xunit;

namespace EvaGest.Tests.Views;

/// <summary>
/// B-34: the question before a delete showed "Eliminar" as the blue main button with the
/// focus on it, so an Enter pressed out of habit deleted. A question whose answer loses
/// something now shows it in red and starts on the way back; the others keep the blue
/// answer and its focus, as before.
/// </summary>
[Collection(WpfCollection.Name)]
public class ConfirmDialogViewTests(ApplicationWpf app)
{
    private static ConfirmDialogView Laid(ConfirmDialogViewModel vm)
    {
        var view = new ConfirmDialogView { DataContext = vm };
        view.Measure(new Size(500, 400));
        view.Arrange(new Rect(0, 0, 500, 400));
        // The buttons sit in the shell's Actions, whose bindings resolve on a later
        // dispatcher pass, as they do when the window opens.
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, () => frame.Continue = false);
        Dispatcher.PushFrame(frame);
        return view;
    }

    private static Button Named(FrameworkElement view, string name) => (Button)view.FindName(name);

    [Fact]
    public void A_destructive_answer_is_red_and_the_focus_starts_on_the_way_back()
        => app.Runs(() =>
        {
            var view = Laid(new ConfirmDialogViewModel(Texts.DeleteAppointmentTitle,
                Texts.DeleteAppointmentMessage, Texts.Delete, destructive: true));

            Named(view, "DestructiveConfirmButton").Visibility.Should().Be(Visibility.Visible);
            Named(view, "ConfirmButton").Visibility.Should().Be(Visibility.Collapsed, "the blue one is for answers that lose nothing");
            view.FirstFocus.Should().BeSameAs(Named(view, "CancelButton"));
        });

    [Fact]
    public void An_ordinary_answer_stays_blue_and_keeps_the_focus()
        => app.Runs(() =>
        {
            var view = Laid(new ConfirmDialogViewModel(Texts.DuplicateServiceTitle, "?",
                Texts.AddAnotherOne, Texts.DontAdd));

            Named(view, "ConfirmButton").Visibility.Should().Be(Visibility.Visible);
            Named(view, "DestructiveConfirmButton").Visibility.Should().Be(Visibility.Collapsed);
            view.FirstFocus.Should().BeSameAs(Named(view, "ConfirmButton"));
        });

    [Fact]
    public async Task Deleting_or_discarding_an_appointment_asks_a_destructive_question()
    {
        await using var testDb = new TestDatabase();
        var f = new TestFactory(testDb.Options);
        var dialogs = new TestDialogService();
        int clientId = await new ClientService(f).Create(Make.Client("Joan García"));
        int id = await new AppointmentService(f).Create(new Appointment
        {
            Date = DateOnly.FromDateTime(DateTime.Today), Time = new TimeOnly(10, 0), DurationMin = 30, ClientId = clientId
        });
        var appointment = (await new AppointmentService(f).GetById(id))!;
        var vm = new AppointmentDialogViewModel(new AppointmentService(f), new AvailabilityService(f),
            new ClientService(f), new CatalogService(f), new WorkerService(f), new SettingsService(f),
            dialogs, appointment);
        await vm.Initialization;

        await vm.DeleteCommand.ExecuteAsync(null);
        vm.TimeText = "16:45";
        await vm.CanDiscard();

        dialogs.DestructiveConfirmations.Should().Equal(Texts.DeleteAppointmentTitle, Texts.DiscardChangesTitle);
    }
}
