using AwesomeAssertions;
using EvaGest.Helpers;
using Xunit;

namespace EvaGest.Tests.Calculations;

public class GridHelperTests
{
    private const double HeightSlot = 44;
    private static readonly int New = 9 * 60;

    // ---------- Geometria vertical ----------

    [Theory] // G-01
    [InlineData(15, 44 / 15.0)]
    [InlineData(30, 44 / 30.0)]
    [InlineData(60, 44 / 60.0)]
    public void PixelsPerMinute_depends_on_the_granularity(int slotMinutes, double expected)
        => GridHelper.PixelsPerMinute(HeightSlot, slotMinutes).Should().BeApproximately(expected, 1e-9);

    [Fact] // G-02
    public void Top_lists_the_ten_longest_since_their_last_visit()
    {
        double ppm = GridHelper.PixelsPerMinute(HeightSlot, 30);
        GridHelper.Top(new TimeOnly(10, 0), New, ppm).Should().BeApproximately(88, 1e-9);
        GridHelper.Top(new TimeOnly(9, 0), New, ppm).Should().Be(0);
    }

    [Fact] // G-03
    public void The_height_is_proportional_to_the_duration_minus_the_gap()
    {
        double ppm = GridHelper.PixelsPerMinute(HeightSlot, 30);
        GridHelper.Height(45, ppm, 18).Should().BeApproximately(66 - 1, 1e-9);
    }

    [Fact] // G-04
    public void The_height_never_drops_below_the_clickable_minimum()
    {
        double ppm = GridHelper.PixelsPerMinute(HeightSlot, 60);
        GridHelper.Height(5, ppm, 18).Should().BeApproximately(18 - 1, 1e-9);
    }

    // ---------- Slot from a position ----------

    [Fact] // G-05
    public void SlotFromOfY_a_the_boundary_exact_falls_a_the_interval_that_starts
        () {
        double ppm = GridHelper.PixelsPerMinute(HeightSlot, 30);
        GridHelper.SlotFromY(0, New, ppm, 30).Should().Be(new TimeOnly(9, 0));
        GridHelper.SlotFromY(44, New, ppm, 30).Should().Be(new TimeOnly(9, 30));
        GridHelper.SlotFromY(88, New, ppm, 30).Should().Be(new TimeOnly(10, 0));
    }

    [Fact] // G-06
    public void SlotFromY_snaps_half_a_slot_downwards()
    {
        double ppm = GridHelper.PixelsPerMinute(HeightSlot, 30);
        GridHelper.SlotFromY(43.9, New, ppm, 30).Should().Be(new TimeOnly(9, 0));
        GridHelper.SlotFromY(60, New, ppm, 30).Should().Be(new TimeOnly(9, 30));
    }

    [Theory] // G-07
    [InlineData(15, 9, 15)]
    [InlineData(30, 9, 0)]
    [InlineData(60, 9, 0)]
    public void SlotFromY_snaps_to_the_configured_granularity(int slotMinutes, int timeExpected, int minuteExpected)
    {
        double ppm = GridHelper.PixelsPerMinute(HeightSlot, slotMinutes);
        // 20 minutes after the start of the grid
        GridHelper.SlotFromY(20 * ppm, New, ppm, slotMinutes)
            .Should().Be(new TimeOnly(timeExpected, minuteExpected));
    }

    [Fact] // G-08
    public void SlotFromY_is_clamped_at_the_end_of_the_day()
    {
        double ppm = GridHelper.PixelsPerMinute(HeightSlot, 30);
        GridHelper.SlotFromY(100_000, New, ppm, 30).Should().Be(new TimeOnly(23, 30));
    }

    // ---------- Rang visible ----------

    [Fact] // G-09
    public void VisibleRange_with_a_split_shift_spans_the_first_opening_to_the_last_closing_plus_an_hour_each_side()
    {
        var intervals = new[] { (new TimeOnly(9, 0), new TimeOnly(13, 0)), (new TimeOnly(16, 0), new TimeOnly(20, 0)) };
        GridHelper.VisibleRange(intervals, []).Should().Be((8 * 60, 21 * 60));
    }

    [Fact] // G-10
    public void VisibleRange_widens_for_an_appointment_before_opening()
    {
        var intervals = new[] { (new TimeOnly(9, 0), new TimeOnly(20, 0)) };
        var appointments = new[] { (new TimeOnly(7, 30), 30) };
        GridHelper.VisibleRange(intervals, appointments).Should().Be((7 * 60, 21 * 60));
    }

    [Fact] // G-11
    public void VisibleRange_widens_for_an_appointment_after_closing()
    {
        var intervals = new[] { (new TimeOnly(9, 0), new TimeOnly(20, 0)) };
        var appointments = new[] { (new TimeOnly(21, 15), 45) };
        GridHelper.VisibleRange(intervals, appointments).Should().Be((8 * 60, 22 * 60));
    }

    [Fact] // G-12
    public void VisibleRange_treats_midnight_as_the_end_of_the_day()
    {
        var intervals = new[] { (new TimeOnly(18, 0), TimeOnly.MinValue) };
        GridHelper.VisibleRange(intervals, []).Should().Be((17 * 60, 24 * 60));
    }

    [Fact]
    public void VisibleRange_padding_never_runs_past_either_end_of_the_day()
    {
        var intervals = new[] { (new TimeOnly(0, 30), new TimeOnly(23, 30)) };
        GridHelper.VisibleRange(intervals, []).Should().Be((0, 24 * 60));
    }

    [Fact]
    public void VisibleRange_an_appointment_inside_the_padding_hour_does_not_widen_further()
    {
        var intervals = new[] { (new TimeOnly(9, 0), new TimeOnly(20, 0)) };
        var appointments = new[] { (new TimeOnly(8, 30), 30), (new TimeOnly(20, 15), 45) };
        GridHelper.VisibleRange(intervals, appointments).Should().Be((8 * 60, 21 * 60));
    }

    [Fact] // G-13
    public void VisibleRange_without_schedules_or_appointments_falls_back_to_the_default()
    {
        GridHelper.VisibleRange([], []).Should().Be(GridHelper.DefaultRange);
    }

    [Fact] // G-14
    public void VisibleRange_never_returns_a_grid_shorter_than_an_hour()
    {
        var appointments = new[] { (new TimeOnly(10, 10), 5) };
        var (start, fi) = GridHelper.VisibleRange([], appointments);
        (fi - start).Should().BeGreaterThanOrEqualTo(60);
    }

    // ---------- Bands fora d'schedule ----------

    [Fact] // G-15
    public void BandsOutsideSchedule_without_intervals_covers_the_whole_day()
    {
        GridHelper.OutsideScheduleBands([], 9 * 60, 20 * 60)
            .Should().ContainSingle().Which.Should().Be((9 * 60, 20 * 60));
    }

    [Fact] // G-16
    public void BandsOutsideSchedule_with_one_interval_gives_two_bands()
    {
        var intervals = new[] { (new TimeOnly(10, 0), new TimeOnly(18, 0)) };
        GridHelper.OutsideScheduleBands(intervals, 9 * 60, 20 * 60)
            .Should().Equal((9 * 60, 10 * 60), (18 * 60, 20 * 60));
    }

    [Fact] // G-17
    public void BandsOutsideSchedule_with_a_split_shift_gives_the_midday_band()
    {
        var intervals = new[] { (new TimeOnly(9, 0), new TimeOnly(13, 0)), (new TimeOnly(16, 0), new TimeOnly(20, 0)) };
        GridHelper.OutsideScheduleBands(intervals, 9 * 60, 20 * 60)
            .Should().ContainSingle().Which.Should().Be((13 * 60, 16 * 60));
    }

    [Fact] // G-18
    public void BandsOutsideSchedule_with_an_interval_wider_than_the_range_gives_no_band()
    {
        var intervals = new[] { (new TimeOnly(6, 0), new TimeOnly(23, 0)) };
        GridHelper.OutsideScheduleBands(intervals, 9 * 60, 20 * 60).Should().BeEmpty();
    }

    // ---------- Lane distribution ----------

    private static TimeBlock Block(int startTime, int startMinute, int duration)
    {
        int start = startTime * 60 + startMinute;
        return new TimeBlock(start, start + duration);
    }

    [Fact] // G-19
    public void DistributeLanes_without_blocks_does_not_crash()
        => GridHelper.DistributeLanes([]).Should().BeEmpty();

    [Fact] // G-20
    public void DistributeLanes_leaves_everything_in_lane_zero_when_nothing_overlaps()
    {
        var blocks = new[] { Block(10, 0, 30), Block(12, 0, 30), Block(15, 0, 30) };
        GridHelper.DistributeLanes(blocks).Should().AllBeEquivalentTo(new LaneAssignment(0, 1));
    }

    [Fact] // G-21
    public void DistributeLanes_splits_the_column_between_two_overlapping_blocks()
    {
        var blocks = new[] { Block(10, 0, 60), Block(10, 30, 60) };
        GridHelper.DistributeLanes(blocks)
            .Should().Equal(new LaneAssignment(0, 2), new LaneAssignment(1, 2));
    }

    [Fact] // G-22
    public void DistributeLanes_treats_a_chain_as_a_single_cluster()
    {
        // A(10-11) B(10:30-11:30) C(11-12): C overlaps B, so all three share the width.
        // Only 2 lanes are needed — C reuses A's — and all three must carry the same
        // Total, or A and C would be drawn wider than B.
        var blocks = new[] { Block(10, 0, 60), Block(10, 30, 60), Block(11, 0, 60) };
        var r = GridHelper.DistributeLanes(blocks);
        r.Should().OnlyContain(a => a.Total == 2);
        r.Select(a => a.Index).Should().Equal(0, 1, 0);
    }

    [Fact] // G-23
    public void DistributeLanes_treats_blocks_that_merely_touch_as_not_overlapping()
    {
        // A acaba exactament quan comença B: dos clústers independents, column sencera cadascun
        var blocks = new[] { Block(10, 0, 60), Block(11, 0, 60) };
        GridHelper.DistributeLanes(blocks).Should().AllBeEquivalentTo(new LaneAssignment(0, 1));
    }

    [Fact] // G-24
    public void DistributeLanes_gives_a_nested_block_its_own_lane()
    {
        var blocks = new[] { Block(10, 0, 120), Block(10, 30, 30) };
        GridHelper.DistributeLanes(blocks)
            .Should().Equal(new LaneAssignment(0, 2), new LaneAssignment(1, 2));
    }

    [Fact] // G-25
    public void DistributeLanes_puts_three_identical_blocks_in_three_lanes()
    {
        var blocks = new[] { Block(10, 0, 30), Block(10, 0, 30), Block(10, 0, 30) };
        var r = GridHelper.DistributeLanes(blocks);
        r.Should().OnlyContain(a => a.Total == 3);
        r.Select(a => a.Index).Should().BeEquivalentTo([0, 1, 2]);
    }

    [Fact] // G-26
    public void DistributeLanes_does_not_narrow_one_cluster_because_of_another()
    {
        // A triple overlap at 10:00 plus a lone appointment at 18:00
        var blocks = new[] { Block(10, 0, 60), Block(10, 0, 60), Block(10, 0, 60), Block(18, 0, 30) };
        var r = GridHelper.DistributeLanes(blocks);
        r[0].Total.Should().Be(3);
        r[3].Should().Be(new LaneAssignment(0, 1));
    }

    [Fact] // G-27
    public void DistributeLanes_does_not_let_a_zero_length_block_keep_a_lane_forever()
    {
        var blocks = new[] { new TimeBlock(600, 600), Block(11, 0, 30) };
        GridHelper.DistributeLanes(blocks).Should().AllBeEquivalentTo(new LaneAssignment(0, 1));
    }

    // ---------- Granularitat ----------

    [Theory] // G-28
    [InlineData(15, 15)]
    [InlineData(30, 30)]
    [InlineData(60, 60)]
    [InlineData(45, 30)]
    [InlineData(0, 30)]
    [InlineData(-5, 30)]
    [InlineData(1440, 30)]
    public void IsValidSlotMinutes_accepts_only_fifteen_thirty_or_sixty(int cashIn, int expected)
        => GridHelper.IsValidSlotMinutes(cashIn).Should().Be(expected);
}
