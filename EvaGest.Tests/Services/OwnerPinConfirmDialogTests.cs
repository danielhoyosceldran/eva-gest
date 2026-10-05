using AwesomeAssertions;
using EvaGest.Resources;
using EvaGest.Services;
using EvaGest.Tests.Infra;
using EvaGest.ViewModels.Dialogs;
using Xunit;

namespace EvaGest.Tests.Services;

/// <summary>
/// F-05, the question itself: the dialog that asks for the owner's PIN before a delete or
/// a void. The right PIN confirms without opening or closing owner mode; a wrong one keeps
/// the dialog open; repeated wrong ones lock out, sharing the count with the unlock dialog
/// so the confirmation cannot be used to guess the PIN faster.
/// </summary>
public class OwnerPinConfirmDialogTests
{
    private static OwnerPinConfirmDialogViewModel Dialog(IOwnerAccessService owner)
        => new(owner, "Eliminar el client", "Es perdrà la fitxa.", "Eliminar");

    private sealed class CloseWatcher
    {
        public bool? Closed { get; private set; }

        public CloseWatcher(DialogViewModelBase dialog) => dialog.Close += ok => Closed = ok;
    }

    [Fact]
    public async Task The_right_pin_confirms_and_leaves_a_closed_owner_mode_closed()
    {
        var owner = await TestOwner.Unlocked("4821");
        owner.Lock();
        var vm = Dialog(owner);
        var watcher = new CloseWatcher(vm);

        vm.Pin = "4821";
        await vm.ConfirmCommand.ExecuteAsync(null);

        watcher.Closed.Should().BeTrue();
        owner.IsUnlocked.Should().BeFalse("confirming a delete is not opening owner mode");
    }

    [Fact]
    public async Task The_right_pin_confirms_and_leaves_an_open_owner_mode_open()
    {
        var owner = await TestOwner.Unlocked("4821");
        var vm = Dialog(owner);
        var watcher = new CloseWatcher(vm);

        vm.Pin = "4821";
        await vm.ConfirmCommand.ExecuteAsync(null);

        watcher.Closed.Should().BeTrue();
        owner.IsUnlocked.Should().BeTrue();
    }

    [Fact]
    public async Task A_wrong_pin_keeps_the_dialog_open_and_says_so()
    {
        var owner = await TestOwner.Unlocked("4821");
        var vm = Dialog(owner);
        var watcher = new CloseWatcher(vm);

        vm.Pin = "1111";
        await vm.ConfirmCommand.ExecuteAsync(null);

        watcher.Closed.Should().BeNull("a wrong PIN confirms nothing");
        vm.ErrorValidation.Should().NotBeNullOrWhiteSpace();
        owner.IsUnlocked.Should().BeTrue("a wrong confirmation PIN does not close owner mode either");
    }

    [Fact]
    public async Task Five_wrong_pins_lock_out_even_the_right_one()
    {
        var owner = await TestOwner.Unlocked("4821");
        var vm = Dialog(owner);
        var watcher = new CloseWatcher(vm);

        vm.Pin = "1111";
        for (int i = 0; i < OwnerAccessService.MaxAttempts; i++)
            await vm.ConfirmCommand.ExecuteAsync(null);

        vm.ErrorValidation.Should().Be(string.Format(Texts.TooManyAttempts, 30));
        owner.LockoutRemaining.Should().BeGreaterThan(TimeSpan.Zero);

        vm.Pin = "4821";
        await vm.ConfirmCommand.ExecuteAsync(null);
        watcher.Closed.Should().BeNull("locked out: not even the right PIN confirms");
    }

    [Fact]
    public async Task Cancelling_closes_as_not_confirmed()
    {
        var owner = await TestOwner.Unlocked("4821");
        var vm = Dialog(owner);
        var watcher = new CloseWatcher(vm);

        vm.Pin = "4821";
        vm.CancelCommand.Execute(null);

        watcher.Closed.Should().BeFalse();
    }

    // ── The service check behind it ──────────────────────────────────────────

    [Fact]
    public async Task Verifying_the_pin_changes_neither_side_of_owner_mode()
    {
        var owner = await TestOwner.Unlocked("4821");

        (await owner.VerifyPin("4821")).Should().Be(AccessResult.Accepted);
        owner.IsUnlocked.Should().BeTrue();

        owner.Lock();
        (await owner.VerifyPin("4821")).Should().Be(AccessResult.Accepted);
        owner.IsUnlocked.Should().BeFalse();
    }

    [Fact]
    public async Task Wrong_pins_at_the_unlock_and_the_confirmation_count_together()
    {
        var owner = await TestOwner.Unlocked("4821");
        owner.Lock();

        for (int i = 0; i < OwnerAccessService.MaxAttempts - 1; i++)
            (await owner.Unlock("0000")).Should().Be(AccessResult.Wrong);
        (await owner.VerifyPin("0000")).Should().Be(AccessResult.LockedOut,
            "the fifth wrong attempt locks out wherever it is typed");

        (await owner.Unlock("4821")).Should().Be(AccessResult.LockedOut);
        (await owner.VerifyPin("4821")).Should().Be(AccessResult.LockedOut);
    }

    [Fact]
    public async Task Wrong_pins_at_the_confirmation_lock_out_the_unlock_too()
    {
        var owner = await TestOwner.Unlocked("4821");
        owner.Lock();

        for (int i = 0; i < OwnerAccessService.MaxAttempts; i++)
            await owner.VerifyPin("0000");

        (await owner.Unlock("4821")).Should().Be(AccessResult.LockedOut);
        owner.IsUnlocked.Should().BeFalse();
    }
}
