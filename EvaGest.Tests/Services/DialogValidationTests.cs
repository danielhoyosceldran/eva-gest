using AwesomeAssertions;
using EvaGest.Models;
using EvaGest.Resources;
using EvaGest.Tests.Infra;
using EvaGest.Services;
using EvaGest.ViewModels.Dialogs;
using Xunit;

namespace EvaGest.Tests.Services;

/// <summary>
/// The typed boxes of the smaller dialogs. None of these need a database: they hold
/// text until Save judges it.
/// </summary>
public class DialogValidationTests
{
    private static MovementDialogViewModel Movement()
    {
        var vm = new MovementDialogViewModel(MovementType.In, DateOnly.FromDateTime(DateTime.Today))
        {
            PriceText = "15,00",
            Concept = "Canvi inicial"
        };
        vm.ActiveMethods.Add(Infra.Make.Method());
        vm.Method = vm.ActiveMethods[0];
        return vm;
    }

    [Theory]
    [InlineData("vint-i-u")]
    [InlineData("101")]
    [InlineData("-1")]
    [InlineData("")]
    public void A_movement_with_a_malformed_vat_rate_is_refused_not_crashed(string typed)
    {
        var vm = Movement();
        vm.SplitVat = true;
        vm.VatText = typed;

        vm.SaveCommand.Execute(null);

        vm.ErrorValidation.Should().Be(Texts.VatOutOfRange);
    }

    [Fact]
    public void A_movement_that_does_not_split_vat_ignores_the_rate_box()
    {
        var vm = Movement();
        vm.SplitVat = false;
        vm.VatText = "qualsevol cosa";

        vm.SaveCommand.Execute(null);

        vm.ErrorValidation.Should().BeNull("the rate is not used, so it is not judged");
        vm.AModel().VatBp.Should().BeNull();
    }

    [Fact]
    public void A_movement_that_splits_vat_freezes_the_rate_typed()
    {
        var vm = Movement();
        vm.SplitVat = true;
        vm.VatText = "21";

        vm.SaveCommand.Execute(null);

        vm.ErrorValidation.Should().BeNull();
        var movement = vm.AModel();
        movement.VatBp.Should().Be(2100);
        movement.BaseCents.Should().Be(1240);
        movement.VatCents.Should().Be(260);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-30")]
    [InlineData("30,5")]
    [InlineData("mitja hora")]
    public void A_service_with_a_malformed_duration_is_refused(string typed)
    {
        var vm = new ServiceDialogViewModel { Name = "Tall", PriceText = "15,00", VatText = "21" };
        vm.DurationMinText = typed;

        vm.SaveCommand.Execute(null);

        vm.ErrorValidation.Should().Be(Texts.DurationInvalid);
    }

    [Fact]
    public void A_service_with_no_duration_at_all_is_allowed_since_it_is_optional()
    {
        var vm = new ServiceDialogViewModel { Name = "Tall", PriceText = "15,00", VatText = "21" };
        vm.DurationMinText = "";

        vm.SaveCommand.Execute(null);

        vm.ErrorValidation.Should().BeNull();
        vm.AModel().DurationMin.Should().BeNull();
    }

    [Theory]
    [InlineData("quinze")]
    [InlineData("15€uros")]
    [InlineData("")]
    public void A_product_with_a_malformed_price_is_refused(string typed)
    {
        var vm = new ProductDialogViewModel { Name = "Cera", VatText = "21" };
        vm.PriceText = typed;

        vm.SaveCommand.Execute(null);

        vm.ErrorValidation.Should().Be(Texts.PriceInvalid);
    }

    [Theory]
    [InlineData("101")]
    [InlineData("-1")]
    [InlineData("vint-i-u")]
    public void A_product_with_a_vat_rate_outside_0_to_100_is_refused(string typed)
    {
        var vm = new ProductDialogViewModel { Name = "Cera", PriceText = "9,00" };
        vm.VatText = typed;

        vm.SaveCommand.Execute(null);

        vm.ErrorValidation.Should().Be(Texts.VatOutOfRange);
    }

    // ── The two settings the movement dialog is supposed to follow ───────────
    // pantalles 3.5: the VAT-split box is "Només visible si `aplicar_iva_caixa` està
    // activat", and the rate it starts on is the configured default. Both were written
    // by Configuració and read by nobody: the box always showed, and the rate was the
    // literal 21, so a shop on any other rate froze the wrong quota onto every movement.

    [Fact]
    public async Task The_vat_split_box_is_hidden_unless_the_setting_turns_it_on()
    {
        var vm = Movement();
        await vm.LoadDefaults(new Infra.TestSettings());

        vm.ShowVatSplit.Should().BeFalse("the seeded default for apply_vat_to_till is 0");
    }

    [Fact]
    public async Task The_vat_split_box_is_shown_when_the_setting_turns_it_on()
    {
        var vm = Movement();
        await vm.LoadDefaults(new Infra.TestSettings((ConfigKeys.ApplyVatToTill, "1")));

        vm.ShowVatSplit.Should().BeTrue();
    }

    [Fact]
    public async Task Turning_the_setting_off_clears_a_tick_the_user_can_no_longer_see()
    {
        var vm = Movement();
        vm.SplitVat = true;

        await vm.LoadDefaults(new Infra.TestSettings((ConfigKeys.ApplyVatToTill, "0")));

        vm.SplitVat.Should().BeFalse();
        vm.AModel().VatBp.Should().BeNull("a hidden box must not still split the VAT");
    }

    [Fact]
    public async Task A_new_movement_starts_on_the_configured_vat_rate_not_on_21()
    {
        var vm = Movement();
        await vm.LoadDefaults(new Infra.TestSettings(
            (ConfigKeys.ApplyVatToTill, "1"), (ConfigKeys.DefaultVatBp, "1000")));

        vm.VatText.Should().Be("10");

        vm.SplitVat = true;
        var model = vm.AModel();
        model.VatBp.Should().Be(1000, "the rate frozen onto the movement is the shop's own");
        model.BaseCents.Should().Be(1364);   // 1500 inc. 10 % -> 1363,64 -> 1364
        model.VatCents.Should().Be(136);
        (model.BaseCents + model.VatCents).Should().Be(model.AmountCents);
    }
}
