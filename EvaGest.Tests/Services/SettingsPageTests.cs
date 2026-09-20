using AwesomeAssertions;
using EvaGest.Models;
using EvaGest.Resources;
using EvaGest.Services;
using EvaGest.Tests.Infra;
using EvaGest.ViewModels.Pages;
using Xunit;

namespace EvaGest.Tests.Services;

/// <summary>
/// Block Q: the Settings page. Every RF-23 key has to survive closing and reopening
/// the screen; otherwise the user changes an option and nothing happens.
/// </summary>
public class SettingsPageTests
{
    private static SettingsViewModel Build(TestDatabase testDb, TestDialogService? dialogs = null)
    {
        var factory = new TestFactory(testDb.Options);
        var paths = new AppPaths(
            Path.Combine(Path.GetTempPath(), "eva-prova.db"), Path.GetTempPath());
        var config = new SettingsService(factory);

        return new SettingsViewModel(
            new BackupService(paths, config), new ExportService(factory), config,
            new AvailabilityService(factory), dialogs ?? new TestDialogService());
    }

    [Fact] // Q-01
    public async Task The_shop_details_are_saved_and_read_back()
    {
        await using var testDb = new TestDatabase();
        var vm = Build(testDb);
        await vm.Load();

        vm.ShopName = "Barberia Eva";
        vm.ShopAddress = "Carrer Major, 1";
        vm.ShopPhone = "600111222";
        await vm.SaveShopDetailsCommand.ExecuteAsync(null);

        var other = Build(testDb);
        await other.Load();
        other.ShopName.Should().Be("Barberia Eva");
        other.ShopAddress.Should().Be("Carrer Major, 1");
        other.ShopPhone.Should().Be("600111222");
    }

    [Fact] // Q-02
    public async Task The_cells_are_saved_as_soon_as_they_change()
    {
        await using var testDb = new TestDatabase();
        var vm = Build(testDb);
        await vm.Load();

        vm.ConfirmationSound = false;
        vm.ShowGuestNotice = false;
        vm.ApplyVatToTill = true;

        var other = Build(testDb);
        await other.Load();
        other.ConfirmationSound.Should().BeFalse();
        other.ShowGuestNotice.Should().BeFalse();
        other.ApplyVatToTill.Should().BeTrue();
    }

    [Fact] // Q-03
    public async Task A_malformed_default_vat_is_not_saved()
    {
        await using var testDb = new TestDatabase();
        var vm = Build(testDb);
        await vm.Load();

        vm.DefaultVatText = "cent-vint";
        await vm.SaveDefaultVatCommand.ExecuteAsync(null);

        vm.VatError.Should().NotBeNull();
        var other = Build(testDb);
        await other.Load();
        other.DefaultVatText.Should().Be("21");
    }

    [Fact] // Q-04
    public async Task Changing_the_vat_mode_asks_for_confirmation_and_saves_it()
    {
        await using var testDb = new TestDatabase();
        var dialogs = new TestDialogService { ResultConfirm = true };
        var vm = Build(testDb, dialogs);
        await vm.Load();

        vm.ModeVat = VatMode.NotIncluded;

        dialogs.ConfirmacionsRequested.Should().ContainSingle();
        var other = Build(testDb);
        await other.Load();
        other.ModeVat.Should().Be(VatMode.NotIncluded);
    }

    [Fact] // Q-05
    public async Task Declining_the_vat_mode_change_leaves_the_picker_as_it_was()
    {
        await using var testDb = new TestDatabase();
        var dialogs = new TestDialogService { ResultConfirm = false };
        var vm = Build(testDb, dialogs);
        await vm.Load();

        vm.ModeVat = VatMode.NotIncluded;

        vm.ModeVat.Should().Be(VatMode.Included, "the picker cannot show a value that was never saved");
        var other = Build(testDb);
        await other.Load();
        other.ModeVat.Should().Be(VatMode.Included);
    }

    [Fact] // Q-06
    public async Task The_saved_vat_mode_is_the_one_frozen_on_a_new_sale()
    {
        await using var testDb = new TestDatabase();
        var factory = new TestFactory(testDb.Options);
        var config = new SettingsService(factory);
        await config.Save(ConfigKeys.CurrentVatMode, nameof(VatMode.NotIncluded));

        int methodId;
        await using (var db = testDb.Context())
        {
            var m = Make.Method();
            db.PaymentMethods.Add(m);
            await db.SaveChangesAsync();
            methodId = m.Id;
        }

        var sales = new SaleService(factory, config);
        int id = await sales.Create(
            new Sale
            {
                Date = new DateOnly(2026, 9, 7), Time = new TimeOnly(10, 0),
                GuestName = "Client de prova", PaymentMethodId = methodId
            },
            [Make.Line(1000)]);

        var sale = await sales.GetById(id);
        sale!.VatMode.Should().Be(VatMode.NotIncluded);
        sale.BaseCents.Should().Be(1000, "with VAT excluded, the price typed is the base");
        sale.TotalCents.Should().Be(1210);
    }

    [Fact] // Q-07
    public async Task Editing_an_old_sale_does_not_reinterpret_it_with_todays_vat_mode()
    {
        await using var testDb = new TestDatabase();
        var factory = new TestFactory(testDb.Options);
        var config = new SettingsService(factory);
        await config.Save(ConfigKeys.CurrentVatMode, nameof(VatMode.Included));

        int methodId;
        await using (var db = testDb.Context())
        {
            var m = Make.Method();
            db.PaymentMethods.Add(m);
            await db.SaveChangesAsync();
            methodId = m.Id;
        }

        var sales = new SaleService(factory, config);
        var sale = new Sale
        {
            Date = new DateOnly(2026, 9, 7), Time = new TimeOnly(10, 0),
            GuestName = "Client de prova", PaymentMethodId = methodId
        };
        sale.Id = await sales.Create(sale, [Make.Line(1000)]);

        // The shop switches to VAT-exclusive prices, then an old ticket gets corrected
        await config.Save(ConfigKeys.CurrentVatMode, nameof(VatMode.NotIncluded));
        await sales.Update(sale, [Make.Line(1000)]);

        var saved = await sales.GetById(sale.Id);
        saved!.VatMode.Should().Be(VatMode.Included);
        saved.TotalCents.Should().Be(1000, "the ticket was charged with VAT inside the price");
    }

    [Fact] // Q-08
    public async Task Adding_a_closed_day_reaches_the_availability_check()
    {
        await using var testDb = new TestDatabase();
        var vm = Build(testDb);
        await vm.Load();

        vm.NewClosedDate = new DateOnly(2026, 12, 25);
        vm.NewClosedReason = "Nadal";
        await vm.AddClosedDayCommand.ExecuteAsync(null);

        vm.ClosedDays.Should().ContainSingle();
        vm.HasNoClosedDays.Should().BeFalse();
        vm.NewClosedReason.Should().BeEmpty("the field is cleared so the next one can be added");

        var result = await new AvailabilityService(new TestFactory(testDb.Options))
            .Check(new DateOnly(2026, 12, 25), new TimeOnly(10, 0), 30, null);
        result.ClosedDay.Should().BeTrue();
        result.ClosedDayReason.Should().Be("Nadal");
    }

    [Fact] // Q-09
    public async Task Marking_the_same_day_twice_updates_its_reason()
    {
        await using var testDb = new TestDatabase();
        var vm = Build(testDb);
        await vm.Load();

        vm.NewClosedDate = new DateOnly(2026, 12, 25);
        vm.NewClosedReason = "Nadal";
        await vm.AddClosedDayCommand.ExecuteAsync(null);

        vm.NewClosedReason = "Festiu";
        await vm.AddClosedDayCommand.ExecuteAsync(null);

        vm.ClosedDays.Should().ContainSingle("the unique index on the date must not blow up");
        vm.ClosedDays[0].Reason.Should().Be("Festiu");
    }

    [Fact] // Q-10
    public async Task Removing_a_closed_day_opens_it_again()
    {
        await using var testDb = new TestDatabase();
        var vm = Build(testDb);
        await vm.Load();
        vm.NewClosedDate = new DateOnly(2026, 12, 25);
        await vm.AddClosedDayCommand.ExecuteAsync(null);

        await vm.RemoveClosedDayCommand.ExecuteAsync(vm.ClosedDays[0]);

        vm.ClosedDays.Should().BeEmpty();
        vm.HasNoClosedDays.Should().BeTrue();
    }

    [Fact] // Q-11
    public async Task The_default_appointment_duration_is_saved_when_it_validates()
    {
        await using var testDb = new TestDatabase();
        var vm = Build(testDb);
        await vm.Load();

        vm.DefaultAppointmentDurationText = "0";
        await vm.SaveDefaultDurationCommand.ExecuteAsync(null);
        vm.ErrorAgenda.Should().NotBeNull();

        vm.DefaultAppointmentDurationText = "45";
        await vm.SaveDefaultDurationCommand.ExecuteAsync(null);
        vm.ErrorAgenda.Should().BeNull();

        var other = Build(testDb);
        await other.Load();
        other.DefaultAppointmentDurationText.Should().Be("45");
    }

    [Fact] // Q-12
    public async Task The_backup_options_are_validated_before_being_saved()
    {
        await using var testDb = new TestDatabase();
        var vm = Build(testDb);
        await vm.Load();

        vm.BackupTimeText = "vint";
        await vm.SaveBackupOptionsCommand.ExecuteAsync(null);
        vm.ErrorBackup.Should().NotBeNull();

        vm.BackupTimeText = "21:30";
        vm.BackupsToKeepText = "0";
        await vm.SaveBackupOptionsCommand.ExecuteAsync(null);
        vm.ErrorBackup.Should().NotBeNull();

        vm.BackupsToKeepText = "7";
        await vm.SaveBackupOptionsCommand.ExecuteAsync(null);
        vm.ErrorBackup.Should().BeNull();

        var other = Build(testDb);
        await other.Load();
        other.BackupTimeText.Should().Be("21:30");
        other.BackupsToKeepText.Should().Be("7");
    }

    [Fact] // Q-13
    public async Task A_malformed_shop_phone_is_not_saved()
    {
        await using var testDb = new TestDatabase();
        var vm = Build(testDb);
        await vm.Load();

        vm.ShopPhone = "123";
        await vm.SaveShopDetailsCommand.ExecuteAsync(null);
        vm.ErrorShop.Should().NotBeNull();

        vm.ShopPhone = "600 111 222";
        await vm.SaveShopDetailsCommand.ExecuteAsync(null);
        vm.ErrorShop.Should().BeNull();

        var other = Build(testDb);
        await other.Load();
        other.ShopPhone.Should().Be("600 111 222");
    }

    // ── Changing the VAT mode ────────────────────────────────────────────────
    // The one setting that confirms before saving (CU-10), because it reinterprets
    // every catalogue price. _modeVatSaved used to be set BEFORE the await, so a write
    // that failed left the picker and the saved value agreeing on a mode that was never
    // stored, with no error — while every later sale was still frozen in the old one.

    private static SettingsViewModel BuildWith(
        TestDatabase testDb, ISettingsService config, TestDialogService dialogs)
    {
        var factory = new TestFactory(testDb.Options);
        var paths = new AppPaths(
            Path.Combine(Path.GetTempPath(), "eva-prova.db"), Path.GetTempPath());

        return new SettingsViewModel(
            new BackupService(paths, config), new ExportService(factory), config,
            new AvailabilityService(factory), dialogs);
    }

    [Fact]
    public async Task A_confirmed_vat_mode_change_is_saved()
    {
        await using var testDb = new TestDatabase();
        var config = new TestSettings((ConfigKeys.CurrentVatMode, nameof(VatMode.Included)));
        var vm = BuildWith(testDb, config, new TestDialogService { ResultConfirm = true });
        await vm.Load();

        vm.ModeVat = VatMode.NotIncluded;
        await vm.VatModeChange;

        vm.ModeVat.Should().Be(VatMode.NotIncluded);
        (await config.Get(ConfigKeys.CurrentVatMode)).Should().Be(nameof(VatMode.NotIncluded));
        vm.VatError.Should().BeNull();
    }

    [Fact]
    public async Task A_vat_mode_change_that_cannot_be_saved_puts_the_picker_back_and_says_so()
    {
        await using var testDb = new TestDatabase();
        var config = new TestSettings((ConfigKeys.CurrentVatMode, nameof(VatMode.Included)));
        config.FailsToSave.Add(ConfigKeys.CurrentVatMode);

        var vm = BuildWith(testDb, config, new TestDialogService { ResultConfirm = true });
        await vm.Load();

        vm.ModeVat = VatMode.NotIncluded;
        await vm.VatModeChange;

        vm.ModeVat.Should().Be(VatMode.Included, "nothing was stored, so nothing may look stored");
        vm.VatError.Should().Be(Texts.VatModeNotSaved);
        vm.VatConfirmation.Should().BeNull();

        // And the failure must not have been recorded as the new baseline: changing the
        // picker again has to retry the save rather than decide there is nothing to do.
        config.FailsToSave.Clear();
        vm.ModeVat = VatMode.NotIncluded;
        await vm.VatModeChange;

        (await config.Get(ConfigKeys.CurrentVatMode)).Should().Be(nameof(VatMode.NotIncluded));
    }
}
