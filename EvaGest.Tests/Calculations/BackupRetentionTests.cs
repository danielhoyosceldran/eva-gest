using AwesomeAssertions;
using EvaGest.Services;
using Xunit;

namespace EvaGest.Tests.Calculations;

/// <summary>
/// Which backups a prune keeps. Keeping only the newest N left nothing older than a
/// couple of weeks, so damage noticed late was already in every copy.
/// </summary>
public class BackupRetentionTests
{
    /// <summary>One daily backup per day from <paramref name="first"/> to
    /// <paramref name="last"/>, newest first, the way ListAll returns them.</summary>
    private static List<BackupInfo> Daily(DateTime first, DateTime last)
    {
        var list = new List<BackupInfo>();
        for (var d = last; d >= first; d = d.AddDays(-1))
            list.Add(new BackupInfo($"{d:yyyyMMdd}_200000000_auto.db", d.AddHours(20), true, 1));
        return list;
    }

    [Fact]
    public void The_newest_copies_are_always_kept()
    {
        var backups = Daily(new DateTime(2026, 9, 1), new DateTime(2026, 9, 20));

        var retained = BackupService.Retained(backups, toKeep: 15);

        retained.Should().Contain(backups.Take(15));
    }

    [Fact]
    public void The_newest_copy_of_each_of_the_last_twelve_months_survives()
    {
        var backups = Daily(new DateTime(2025, 1, 1), new DateTime(2026, 9, 20));

        var retained = BackupService.Retained(backups, toKeep: 15);

        // End of each month from October 2025 to August 2026, plus September's newest.
        for (var month = new DateTime(2025, 10, 1); month < new DateTime(2026, 9, 1); month = month.AddMonths(1))
        {
            var lastDay = month.AddMonths(1).AddDays(-1);
            retained.Should().Contain(b => b.Date.Date == lastDay, $"{month:yyyy-MM} must keep its newest copy");
        }
    }

    [Fact]
    public void The_newest_copy_of_every_year_is_kept_for_good()
    {
        var backups = Daily(new DateTime(2021, 6, 1), new DateTime(2026, 9, 20));

        var retained = BackupService.Retained(backups, toKeep: 15);

        foreach (int year in new[] { 2021, 2022, 2023, 2024, 2025 })
            retained.Should().Contain(b => b.Date.Date == new DateTime(year, 12, 31));
    }

    [Fact]
    public void Everything_else_is_pruned()
    {
        var backups = Daily(new DateTime(2025, 1, 1), new DateTime(2026, 9, 20));

        var retained = BackupService.Retained(backups, toKeep: 15);

        // 15 newest + 11 previous month-ends (Sep's newest is already among the 15)
        // + the end of 2025, which is also December's month-end: 26.
        retained.Should().HaveCount(26);
    }
}
