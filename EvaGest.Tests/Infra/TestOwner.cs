using EvaGest.Services;

namespace EvaGest.Tests.Infra;

/// <summary>
/// A real <see cref="OwnerAccessService"/> over in-memory settings, with one hash
/// iteration so the suite stays fast, and a clock the test can move forward.
/// </summary>
public static class TestOwner
{
    /// <summary>No PIN yet, owner mode closed: what the pages see on a fresh install.</summary>
    public static OwnerAccessService New(TestClock? clock = null)
        => new(new TestSettings(), (clock ?? new TestClock()).Now, iterations: 1);

    /// <summary>A PIN already created, so owner mode is open.</summary>
    public static async Task<OwnerAccessService> Unlocked(string pin = "1234")
    {
        var owner = New();
        await owner.CreatePin(pin);
        return owner;
    }
}

/// <summary>A clock that only moves when the test says so.</summary>
public class TestClock
{
    public DateTime Current { get; set; } = new(2026, 9, 26, 10, 0, 0);

    public DateTime Now() => Current;

    public void Advance(TimeSpan by) => Current += by;
}
