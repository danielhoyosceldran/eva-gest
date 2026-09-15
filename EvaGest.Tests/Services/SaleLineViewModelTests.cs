using AwesomeAssertions;
using EvaGest.Tests.Infra;
using EvaGest.ViewModels.Elements;
using Xunit;

namespace EvaGest.Tests.Services;

/// <summary>
/// The four boxes of one sale line. Quantity used to bind the int and VAT the raw
/// basis points, so WPF coerced whatever it could and a typed "21" in the VAT box
/// meant 0,21 % — frozen onto the sale for good once it was charged.
/// </summary>
public class SaleLineViewModelTests
{
    [Fact]
    public void The_vat_box_is_a_percentage_not_basis_points()
    {
        var line = SaleLineViewModel.Free();

        line.VatText = "21";

        line.VatBp.Should().Be(2100, "21 in the box means 21 %, as in every other VAT box");
    }

    [Fact]
    public void A_line_from_the_catalogue_shows_its_rate_as_a_percentage()
    {
        var line = SaleLineViewModel.FromService(Make.Service("Tall", priceCents: 1500, vatBp: 2100));

        line.VatText.Should().Be("21");
        line.VatBp.Should().Be(2100);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("1,5")]
    [InlineData("dos")]
    [InlineData("")]
    public void A_quantity_that_is_not_a_whole_positive_number_makes_the_line_invalid(string typed)
    {
        var line = SaleLineViewModel.Free();
        line.Description = "Tall";
        line.PriceText = "15,00";
        line.VatText = "21";

        line.QuantityText = typed;

        line.IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData("101")] // over 100 %
    [InlineData("-1")]
    [InlineData("vint-i-u")]
    [InlineData("")]
    public void A_vat_rate_outside_0_to_100_makes_the_line_invalid(string typed)
    {
        var line = SaleLineViewModel.Free();
        line.Description = "Tall";
        line.PriceText = "15,00";
        line.QuantityText = "1";

        line.VatText = typed;

        line.IsValid.Should().BeFalse();
    }

    [Fact]
    public void A_line_with_no_concept_is_invalid()
    {
        var line = SaleLineViewModel.Free();
        line.QuantityText = "1";
        line.PriceText = "15,00";
        line.VatText = "21";

        line.IsValid.Should().BeFalse("a sale line with no concept says nothing on the ticket");
    }

    [Fact]
    public void A_fully_filled_line_is_valid_and_totals_up()
    {
        var line = SaleLineViewModel.Free();
        line.Description = "Tall";
        line.QuantityText = "3";
        line.PriceText = "15,00";
        line.VatText = "21";

        line.IsValid.Should().BeTrue();
        line.AmountCents.Should().Be(4500);
    }

    [Fact]
    public void A_half_typed_quantity_does_not_move_the_amount()
    {
        var line = SaleLineViewModel.Free();
        line.Description = "Tall";
        line.PriceText = "15,00";
        line.QuantityText = "2";

        line.QuantityText = "dos";

        line.Quantity.Should().Be(2, "the last good value stays until a real one replaces it");
        line.AmountCents.Should().Be(3000);
    }
}
