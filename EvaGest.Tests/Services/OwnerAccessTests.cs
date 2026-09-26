using AwesomeAssertions;
using EvaGest.Models;
using EvaGest.Resources;
using EvaGest.Services;
using EvaGest.Tests.Infra;
using EvaGest.ViewModels.Dialogs;
using Xunit;

namespace EvaGest.Tests.Services;

/// <summary>
/// Owner mode: the PIN that opens the private pages, its paper recovery code, the
/// lockout after repeated wrong attempts and the idle lock.
/// </summary>
public class OwnerAccessTests
{
    private static OwnerAccessService Build(TestSettings settings, TestClock clock)
        => new(settings, clock.Now, iterations: 1);

    // ── PIN format and hashing ───────────────────────────────────────────────

    [Theory]
    [InlineData("1234", true)]
    [InlineData("123456", true)]
    [InlineData("123", false)]
    [InlineData("1234567", false)]
    [InlineData("12a4", false)]
    [InlineData("12 34", false)]
    [InlineData("", false)]
    [InlineData("١٢٣٤", false)] // Arabic-Indic digits: char.IsDigit says yes, a PIN says no
    public void A_pin_is_four_to_six_plain_digits(string pin, bool valid)
        => OwnerPin.IsValid(pin).Should().Be(valid);

    [Fact]
    public void A_hashed_pin_verifies_only_against_itself()
    {
        string stored = OwnerPin.Hash("4821", iterations: 1);

        stored.Should().NotContain("4821");
        OwnerPin.Verify("4821", stored).Should().BeTrue();
        OwnerPin.Verify("4822", stored).Should().BeFalse();
    }

    [Fact]
    public void The_same_pin_hashes_differently_each_time()
        => OwnerPin.Hash("4821", 1).Should().NotBe(OwnerPin.Hash("4821", 1));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("4821")]
    [InlineData("pbkdf2-sha256$x$AAAA$AAAA")]
    [InlineData("pbkdf2-sha256$1$not base64!$AAAA")]
    [InlineData("md5$1$AAAA$AAAA")]
    public void A_malformed_stored_value_never_matches(string? stored)
        => OwnerPin.Verify("4821", stored).Should().BeFalse();

    [Fact]
    public void A_recovery_code_is_three_groups_of_four_unambiguous_characters()
    {
        string code = OwnerPin.NewRecoveryCode();

        code.Should().MatchRegex("^[A-Z2-9]{4}-[A-Z2-9]{4}-[A-Z2-9]{4}$");
        code.Should().NotContainAny("0", "O", "1", "I", "L", "5", "S");
    }

    [Fact]
    public void A_recovery_code_copied_back_in_lower_case_with_spaces_still_matches()
        => OwnerPin.NormaliseRecoveryCode("k7qm 2xpa-9trd").Should().Be("K7QM2XPA9TRD");

    // ── Creating the PIN ─────────────────────────────────────────────────────

    [Fact]
    public async Task A_fresh_install_has_no_pin_and_owner_mode_is_closed()
    {
        var owner = TestOwner.New();

        (await owner.HasPin()).Should().BeFalse();
        owner.IsUnlocked.Should().BeFalse();
    }

    [Fact]
    public async Task Creating_the_pin_stores_only_hashes_and_opens_owner_mode()
    {
        var settings = new TestSettings();
        var owner = Build(settings, new TestClock());

        string code = await owner.CreatePin("4821");

        owner.IsUnlocked.Should().BeTrue();
        (await owner.HasPin()).Should().BeTrue();
        (await settings.Get(ConfigKeys.OwnerPin)).Should().NotContain("4821");
        (await settings.Get(ConfigKeys.OwnerRecoveryCode)).Should().NotContain(OwnerPin.NormaliseRecoveryCode(code));
    }

    [Fact]
    public async Task An_existing_pin_cannot_be_replaced_through_create()
    {
        var owner = await TestOwner.Unlocked();

        var act = () => owner.CreatePin("9999");

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    // ── Unlocking and lockout ────────────────────────────────────────────────

    [Fact]
    public async Task The_right_pin_opens_owner_mode_and_a_wrong_one_does_not()
    {
        var owner = await TestOwner.Unlocked("4821");
        owner.Lock();

        (await owner.Unlock("1111")).Should().Be(AccessResult.Wrong);
        owner.IsUnlocked.Should().BeFalse();

        (await owner.Unlock("4821")).Should().Be(AccessResult.Accepted);
        owner.IsUnlocked.Should().BeTrue();
    }

    [Fact]
    public async Task Five_wrong_pins_in_a_row_lock_out_even_the_right_one_for_thirty_seconds()
    {
        var clock = new TestClock();
        var owner = Build(new TestSettings(), clock);
        await owner.CreatePin("4821");
        owner.Lock();

        for (int i = 0; i < OwnerAccessService.MaxAttempts - 1; i++)
            (await owner.Unlock("0000")).Should().Be(AccessResult.Wrong);
        (await owner.Unlock("0000")).Should().Be(AccessResult.LockedOut);

        (await owner.Unlock("4821")).Should().Be(AccessResult.LockedOut);
        owner.LockoutRemaining.Should().Be(OwnerAccessService.LockoutDuration);

        clock.Advance(OwnerAccessService.LockoutDuration);
        (await owner.Unlock("4821")).Should().Be(AccessResult.Accepted);
    }

    [Fact]
    public async Task A_right_pin_resets_the_count_of_wrong_ones()
    {
        var owner = await TestOwner.Unlocked("4821");
        owner.Lock();

        for (int i = 0; i < OwnerAccessService.MaxAttempts - 1; i++) await owner.Unlock("0000");
        await owner.Unlock("4821");
        owner.Lock();

        (await owner.Unlock("0000")).Should().Be(AccessResult.Wrong);
    }

    [Fact]
    public async Task Wrong_recovery_codes_share_the_pin_attempt_counter()
    {
        var owner = await TestOwner.Unlocked("4821");
        owner.Lock();

        for (int i = 0; i < OwnerAccessService.MaxAttempts - 1; i++) await owner.Unlock("0000");

        (await owner.CheckRecoveryCode("AAAA-AAAA-AAAA")).Should().Be(AccessResult.LockedOut);
    }

    // ── Recovery ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task The_recovery_code_allows_a_new_pin_and_is_then_replaced()
    {
        var owner = TestOwner.New();
        string oldCode = await owner.CreatePin("4821");
        owner.Lock();

        (await owner.CheckRecoveryCode(oldCode.ToLowerInvariant().Replace('-', ' ')))
            .Should().Be(AccessResult.Accepted);
        owner.IsUnlocked.Should().BeFalse("the code only allows choosing a new PIN");

        string newCode = await owner.CreatePin("7777");
        owner.IsUnlocked.Should().BeTrue();
        owner.Lock();

        (await owner.Unlock("4821")).Should().Be(AccessResult.Wrong);
        (await owner.Unlock("7777")).Should().Be(AccessResult.Accepted);
        owner.Lock();
        (await owner.CheckRecoveryCode(oldCode)).Should().Be(AccessResult.Wrong);
        (await owner.CheckRecoveryCode(newCode)).Should().Be(AccessResult.Accepted);
    }

    [Fact]
    public async Task An_accepted_recovery_code_allows_only_one_new_pin()
    {
        var owner = TestOwner.New();
        string code = await owner.CreatePin("4821");
        await owner.CheckRecoveryCode(code);
        await owner.CreatePin("7777");

        var act = () => owner.CreatePin("8888");

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    // ── Changing the PIN ─────────────────────────────────────────────────────

    [Fact]
    public async Task Changing_the_pin_needs_the_current_one_and_keeps_the_recovery_code()
    {
        var owner = TestOwner.New();
        string code = await owner.CreatePin("4821");

        (await owner.ChangePin("0000", "7777")).Should().Be(AccessResult.Wrong);
        (await owner.ChangePin("4821", "7777")).Should().Be(AccessResult.Accepted);
        owner.Lock();

        (await owner.Unlock("4821")).Should().Be(AccessResult.Wrong);
        (await owner.Unlock("7777")).Should().Be(AccessResult.Accepted);
        owner.Lock();
        (await owner.CheckRecoveryCode(code)).Should().Be(AccessResult.Accepted);
    }

    // ── Idle lock ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Owner_mode_closes_after_five_idle_minutes_and_activity_restarts_the_count()
    {
        var clock = new TestClock();
        var owner = Build(new TestSettings(), clock);
        await owner.CreatePin("4821");
        int changes = 0;
        owner.Changed += () => changes++;

        clock.Advance(TimeSpan.FromMinutes(4));
        owner.RegisterActivity();
        clock.Advance(TimeSpan.FromMinutes(4));
        owner.LockIfIdle();
        owner.IsUnlocked.Should().BeTrue();

        clock.Advance(TimeSpan.FromMinutes(1));
        owner.LockIfIdle();
        owner.IsUnlocked.Should().BeFalse();
        changes.Should().Be(1);
    }

    // ── Dialogs ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Pressing_enter_on_an_empty_pin_does_not_use_up_an_attempt()
    {
        var owner = await TestOwner.Unlocked("4821");
        owner.Lock();
        var vm = new OwnerUnlockDialogViewModel(owner);

        for (int i = 0; i < OwnerAccessService.MaxAttempts + 1; i++)
            await vm.UnlockCommand.ExecuteAsync(null);

        vm.ErrorValidation.Should().Be(Texts.WrongPin);
        owner.LockoutRemaining.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public async Task The_unlock_dialog_says_how_long_to_wait_once_locked_out()
    {
        var owner = await TestOwner.Unlocked("4821");
        owner.Lock();
        var vm = new OwnerUnlockDialogViewModel(owner) { Pin = "0000" };

        for (int i = 0; i < OwnerAccessService.MaxAttempts; i++)
            await vm.UnlockCommand.ExecuteAsync(null);

        vm.ErrorValidation.Should().Be(string.Format(Texts.TooManyAttempts, 30));
    }

    [Fact]
    public async Task A_correct_recovery_code_closes_the_unlock_dialog_as_recovered()
    {
        var owner = TestOwner.New();
        string code = await owner.CreatePin("4821");
        owner.Lock();
        var vm = new OwnerUnlockDialogViewModel(owner);
        bool? closed = null;
        vm.Close += ok => closed = ok;

        vm.ForgotPinCommand.Execute(null);
        vm.RecoveryCode = code;
        await vm.CheckRecoveryCodeCommand.ExecuteAsync(null);

        closed.Should().BeTrue();
        vm.Recovered.Should().BeTrue();
    }

    [Fact]
    public async Task The_new_pin_has_to_be_typed_the_same_twice()
    {
        var owner = TestOwner.New();
        var vm = new OwnerPinDialogViewModel(owner, OwnerPinMode.Create) { NewPin = "4821", RepeatPin = "4812" };

        await vm.SaveCommand.ExecuteAsync(null);

        vm.ErrorValidation.Should().Be(Texts.PinsDoNotMatch);
        (await owner.HasPin()).Should().BeFalse();
    }

    [Fact]
    public async Task Creating_a_pin_from_the_dialog_hands_back_the_recovery_code()
    {
        var owner = TestOwner.New();
        var vm = new OwnerPinDialogViewModel(owner, OwnerPinMode.Create) { NewPin = "4821", RepeatPin = "4821" };

        await vm.SaveCommand.ExecuteAsync(null);

        vm.ErrorValidation.Should().BeNull();
        vm.NewRecoveryCode.Should().NotBeNullOrEmpty();
        owner.IsUnlocked.Should().BeTrue();
    }

    [Fact]
    public async Task Changing_the_pin_from_the_dialog_with_a_wrong_current_pin_explains_it()
    {
        var owner = await TestOwner.Unlocked("4821");
        var vm = new OwnerPinDialogViewModel(owner, OwnerPinMode.Change)
            { CurrentPin = "0000", NewPin = "7777", RepeatPin = "7777" };

        await vm.SaveCommand.ExecuteAsync(null);

        vm.ErrorValidation.Should().Be(Texts.WrongCurrentPin);
    }
}
