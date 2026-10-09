using AwesomeAssertions;
using EvaGest.Helpers;
using Xunit;

namespace EvaGest.Tests.Calculations;

/// <summary>
/// E-10. The global error handler showed one message box per exception, so a failure
/// that repeats (a timer, a binding) produced a stream of identical boxes, even stacked
/// on top of each other. The gate lets the first through and keeps the rest quiet.
/// </summary>
public class ErrorNoticeGateTests
{
    private static readonly DateTime T0 = new(2026, 10, 9, 10, 0, 0);

    /// <summary>Two exceptions thrown from the same method, as a repeating failure is.</summary>
    private static Exception Thrown(string message)
    {
        try { ThrowFromHere(message); }
        catch (Exception ex) { return ex; }
        return null!;
    }

    private static void ThrowFromHere(string message) => throw new InvalidOperationException(message);

    private static Exception ThrownElsewhere()
    {
        try { throw new FormatException("other"); }
        catch (Exception ex) { return ex; }
    }

    [Fact]
    public void The_first_failure_is_shown()
    {
        var gate = new ErrorNoticeGate(TimeSpan.FromMinutes(1));

        gate.TryEnter(Thrown("a"), T0).Should().BeTrue();
    }

    [Fact]
    public void Nothing_else_is_shown_while_a_message_is_up()
    {
        var gate = new ErrorNoticeGate(TimeSpan.FromMinutes(1));
        gate.TryEnter(Thrown("a"), T0);

        gate.TryEnter(ThrownElsewhere(), T0.AddSeconds(1)).Should().BeFalse(
            "a message box pumps the dispatcher; a second one would open on top of it");
    }

    [Fact]
    public void The_same_failure_repeating_soon_after_is_kept_quiet()
    {
        var gate = new ErrorNoticeGate(TimeSpan.FromMinutes(1));
        gate.TryEnter(Thrown("sale 12"), T0);
        gate.Exit();

        gate.TryEnter(Thrown("sale 13"), T0.AddSeconds(30)).Should().BeFalse(
            "the same method failing again, whatever id its message carries");
    }

    [Fact]
    public void The_same_failure_is_shown_again_once_the_quiet_time_has_passed()
    {
        var gate = new ErrorNoticeGate(TimeSpan.FromMinutes(1));
        gate.TryEnter(Thrown("a"), T0);
        gate.Exit();

        gate.TryEnter(Thrown("a"), T0.AddMinutes(1)).Should().BeTrue();
    }

    [Fact]
    public void A_different_failure_is_shown_after_the_first_message_closes()
    {
        var gate = new ErrorNoticeGate(TimeSpan.FromMinutes(1));
        gate.TryEnter(Thrown("a"), T0);
        gate.Exit();

        gate.TryEnter(ThrownElsewhere(), T0.AddSeconds(5)).Should().BeTrue();
    }
}
