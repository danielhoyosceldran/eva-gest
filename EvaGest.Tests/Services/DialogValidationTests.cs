using AwesomeAssertions;
using EvaGest.Models;
using EvaGest.Resources;
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
        var vm = new MovementDialogViewModel(MovementType.In)
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
}
