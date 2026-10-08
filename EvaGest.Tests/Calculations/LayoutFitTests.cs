using AwesomeAssertions;
using EvaGest.Helpers;
using Xunit;

namespace EvaGest.Tests.Calculations;

/// <summary>The screen arithmetic behind the responsive layout (block R).</summary>
public class LayoutFitTests
{
    [Fact]
    public void A_dialog_may_grow_to_the_work_area_less_its_margin_on_every_side()
        => LayoutFit.MaxWindowSize(1093, 566, 12).Should().Be((1069.0, 542.0));

    [Fact]
    public void A_dialog_cap_is_never_negative_on_a_tiny_screen()
        => LayoutFit.MaxWindowSize(10, 10, 12).Should().Be((0.0, 0.0));

    [Theory]
    [InlineData(100, 300, 0, 1000, 100)]   // fits: untouched
    [InlineData(800, 300, 0, 1000, 700)]   // runs past the end: moved back
    [InlineData(-50, 300, 0, 1000, 0)]     // starts before the area: moved in
    [InlineData(100, 1200, 0, 1000, 0)]    // longer than the area: pinned to its start
    public void A_window_is_kept_inside_the_work_area(double start, double length, double from, double to, double expected)
        => LayoutFit.KeepInside(start, length, from, to).Should().Be(expected);

    [Theory]
    [InlineData(double.PositiveInfinity, 700)] // SizeToContent measuring: full width
    [InlineData(1000, 700)]                    // more room than needed: full width
    [InlineData(500, 500)]                     // in between: takes what there is
    [InlineData(300, 440)]                     // too little: never below the floor
    public void A_flexible_column_takes_what_it_is_offered_between_its_limits(double available, double expected)
        => LayoutFit.Flexible(available, 440, 700).Should().Be(expected);

    [Theory]
    [InlineData(1656, 260, 6, 6)]   // a wide screen: one row
    [InlineData(1102, 260, 6, 4)]   // 4 + 2
    [InlineData(760, 260, 6, 2)]    // 2 + 2 + 2
    [InlineData(100, 260, 6, 1)]    // never fewer than one column
    [InlineData(5000, 260, 3, 3)]   // never more columns than cards
    [InlineData(double.PositiveInfinity, 260, 4, 4)]
    public void A_row_of_cards_wraps_into_as_many_columns_as_fit(double width, double minItem, int items, int expected)
        => LayoutFit.Columns(width, minItem, items).Should().Be(expected);

    [Fact]
    public void An_empty_row_of_cards_still_has_one_column()
        => LayoutFit.Columns(1000, 260, 0).Should().Be(1);
}
