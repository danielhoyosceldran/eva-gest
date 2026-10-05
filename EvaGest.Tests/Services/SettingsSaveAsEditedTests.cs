using AwesomeAssertions;
using EvaGest.Models;
using EvaGest.Services;
using EvaGest.Tests.Infra;
using EvaGest.ViewModels.Pages;
using Xunit;

namespace EvaGest.Tests.Services;

/// <summary>
/// The Settings controls that save the moment they change (apply VAT to the till, the
/// agenda slot size, the guest notice, the confirmation sound, the language). Their write
/// used to be fired and forgotten: when it failed, the control went on showing a value
/// that was never stored, and the page said nothing. A failed write must put the control
/// back to what is stored and show an error in that section; a write that works stores it.
/// </summary>
public sealed class SettingsSaveAsEditedTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "EvaGestSettingsTests_" + Guid.NewGuid());

    public void Dispose()
    {
        if (Directory.Exists(_folder)) Directory.Delete(_folder, recursive: true);
    }

    private async Task<SettingsViewModel> Load(TestDatabase testDb, TestSettings config)
    {
        var factory = new TestFactory(testDb.Options);
        var vm = new SettingsViewModel(
            new BackupService(new AppPaths(Path.Combine(_folder, "eva.db"), _folder), config),
            new ExportService(factory), config, new AvailabilityService(factory),
            new TestDialogService(), TestOwner.New());
        await vm.Load();
        return vm;
    }

    // ── Apply VAT to the till ────────────────────────────────────────────────

    [Fact]
    public async Task Apply_vat_to_till_that_cannot_be_saved_goes_back_and_says_so()
    {
        await using var testDb = new TestDatabase();
        var config = new TestSettings((ConfigKeys.ApplyVatToTill, "0"));
        config.FailsToSave.Add(ConfigKeys.ApplyVatToTill);
        var vm = await Load(testDb, config);

        vm.ApplyVatToTill = true;
        await vm.PendingSave;

        vm.ApplyVatToTill.Should().BeFalse("nothing was stored, so the box cannot look ticked");
        vm.VatError.Should().NotBeNull("the VAT section has to say the change was not saved");
        (await config.Get(ConfigKeys.ApplyVatToTill)).Should().Be("0");
    }

    [Fact]
    public async Task Apply_vat_to_till_that_saves_is_stored()
    {
        await using var testDb = new TestDatabase();
        var config = new TestSettings((ConfigKeys.ApplyVatToTill, "0"));
        var vm = await Load(testDb, config);

        vm.ApplyVatToTill = true;
        await vm.PendingSave;

        vm.ApplyVatToTill.Should().BeTrue();
        vm.VatError.Should().BeNull();
        (await config.GetBool(ConfigKeys.ApplyVatToTill, false)).Should().BeTrue();
    }

    /// <summary>The VAT section's error line is shared with the default-rate box. Ticking
    /// the till box successfully used to clear it, wiping the rate's validation message
    /// while the bad rate was still sitting in its box, unsaved.</summary>
    [Fact]
    public async Task Apply_vat_to_till_saving_fine_keeps_the_default_rate_error()
    {
        await using var testDb = new TestDatabase();
        var config = new TestSettings((ConfigKeys.ApplyVatToTill, "0"));
        var vm = await Load(testDb, config);
        vm.DefaultVatText = "abc";
        await vm.SaveDefaultVatCommand.ExecuteAsync(null);
        var rateError = vm.VatError;

        vm.ApplyVatToTill = true;
        await vm.PendingSave;

        rateError.Should().NotBeNull("the premise: an invalid default rate shows an error");
        vm.VatError.Should().Be(rateError, "a checkbox saving fine says nothing about the rate box");
    }

    // ── Agenda slot size ─────────────────────────────────────────────────────

    [Fact]
    public async Task A_slot_size_that_cannot_be_saved_goes_back_and_says_so()
    {
        await using var testDb = new TestDatabase();
        var config = new TestSettings((ConfigKeys.AgendaSlotMinutes, "30"));
        config.FailsToSave.Add(ConfigKeys.AgendaSlotMinutes);
        var vm = await Load(testDb, config);

        vm.SlotMinutes = 15;
        await vm.PendingSave;

        vm.SlotMinutes.Should().Be(30);
        vm.ErrorAgenda.Should().NotBeNull();
        (await config.Get(ConfigKeys.AgendaSlotMinutes)).Should().Be("30");
    }

    [Fact]
    public async Task A_slot_size_that_saves_is_stored()
    {
        await using var testDb = new TestDatabase();
        var config = new TestSettings((ConfigKeys.AgendaSlotMinutes, "30"));
        var vm = await Load(testDb, config);

        vm.SlotMinutes = 15;
        await vm.PendingSave;

        vm.SlotMinutes.Should().Be(15);
        vm.ErrorAgenda.Should().BeNull();
        (await config.Get(ConfigKeys.AgendaSlotMinutes)).Should().Be("15");
    }

    // ── Guest notice ─────────────────────────────────────────────────────────

    [Fact]
    public async Task A_guest_notice_switch_that_cannot_be_saved_goes_back_and_says_so()
    {
        await using var testDb = new TestDatabase();
        var config = new TestSettings((ConfigKeys.ShowGuestNotice, "1"));
        config.FailsToSave.Add(ConfigKeys.ShowGuestNotice);
        var vm = await Load(testDb, config);

        vm.ShowGuestNotice = false;
        await vm.PendingSave;

        vm.ShowGuestNotice.Should().BeTrue();
        vm.ErrorNotices.Should().NotBeNull();
        (await config.Get(ConfigKeys.ShowGuestNotice)).Should().Be("1");
    }

    [Fact]
    public async Task A_guest_notice_switch_that_saves_is_stored()
    {
        await using var testDb = new TestDatabase();
        var config = new TestSettings((ConfigKeys.ShowGuestNotice, "1"));
        var vm = await Load(testDb, config);

        vm.ShowGuestNotice = false;
        await vm.PendingSave;

        vm.ShowGuestNotice.Should().BeFalse();
        vm.ErrorNotices.Should().BeNull();
        (await config.GetBool(ConfigKeys.ShowGuestNotice, true)).Should().BeFalse();
    }

    // ── Confirmation sound ───────────────────────────────────────────────────

    [Fact]
    public async Task A_confirmation_sound_switch_that_cannot_be_saved_goes_back_and_says_so()
    {
        await using var testDb = new TestDatabase();
        var config = new TestSettings((ConfigKeys.ConfirmationSound, "1"));
        config.FailsToSave.Add(ConfigKeys.ConfirmationSound);
        var vm = await Load(testDb, config);

        vm.ConfirmationSound = false;
        await vm.PendingSave;

        vm.ConfirmationSound.Should().BeTrue();
        vm.ErrorNotices.Should().NotBeNull();
        (await config.Get(ConfigKeys.ConfirmationSound)).Should().Be("1");
    }

    [Fact]
    public async Task A_confirmation_sound_switch_that_saves_is_stored()
    {
        await using var testDb = new TestDatabase();
        var config = new TestSettings((ConfigKeys.ConfirmationSound, "1"));
        var vm = await Load(testDb, config);

        vm.ConfirmationSound = false;
        await vm.PendingSave;

        vm.ConfirmationSound.Should().BeFalse();
        (await config.GetBool(ConfigKeys.ConfirmationSound, true)).Should().BeFalse();
    }

    // ── Language ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_language_that_cannot_be_saved_goes_back_and_says_so()
    {
        await using var testDb = new TestDatabase();
        var config = new TestSettings((ConfigKeys.Language, nameof(Language.Catalan)));
        config.FailsToSave.Add(ConfigKeys.Language);
        var vm = await Load(testDb, config);

        vm.Language = Language.Spanish;
        await vm.PendingSave;

        vm.Language.Should().Be(Language.Catalan);
        vm.ErrorLanguage.Should().NotBeNull();
        vm.LanguageConfirmation.Should().BeNull("a restart would not switch to a language that was never stored");
        (await config.Get(ConfigKeys.Language)).Should().Be(nameof(Language.Catalan));
    }

    [Fact]
    public async Task A_language_that_saves_is_stored()
    {
        await using var testDb = new TestDatabase();
        var config = new TestSettings((ConfigKeys.Language, nameof(Language.Catalan)));
        var vm = await Load(testDb, config);

        vm.Language = Language.Spanish;
        await vm.PendingSave;

        vm.Language.Should().Be(Language.Spanish);
        vm.ErrorLanguage.Should().BeNull();
        (await config.Get(ConfigKeys.Language)).Should().Be(nameof(Language.Spanish));
    }

    // ── After a failure ──────────────────────────────────────────────────────

    [Fact]
    public async Task After_a_failed_save_changing_the_control_again_retries_and_clears_the_error()
    {
        await using var testDb = new TestDatabase();
        var config = new TestSettings((ConfigKeys.ConfirmationSound, "1"));
        config.FailsToSave.Add(ConfigKeys.ConfirmationSound);
        var vm = await Load(testDb, config);

        vm.ConfirmationSound = false;
        await vm.PendingSave;

        config.FailsToSave.Clear();
        vm.ConfirmationSound = false;
        await vm.PendingSave;

        vm.ConfirmationSound.Should().BeFalse();
        vm.ErrorNotices.Should().BeNull();
        (await config.GetBool(ConfigKeys.ConfirmationSound, true)).Should().BeFalse();
    }
}
