using AwesomeAssertions;
using EvaGest.Models;
using EvaGest.Services;
using EvaGest.Tests.Infra;
using Xunit;

namespace EvaGest.Tests.Services;

public class SettingsServiceTests
{
    private static SettingsService Service(TestDatabase testDb) => new(new TestFactory(testDb.Options));

    [Fact] // C-01
    public async Task A_key_that_does_not_exist_returns_null()
    {
        await using var testDb = new TestDatabase();
        (await Service(testDb).Get("no_hi_es")).Should().BeNull();
    }

    [Fact] // C-02
    public async Task Saving_and_reading_back_gives_the_same_value()
    {
        await using var testDb = new TestDatabase();
        var config = Service(testDb);

        await config.Save(ConfigKeys.AgendaSlotMinutes, "15");

        (await config.Get(ConfigKeys.AgendaSlotMinutes)).Should().Be("15");
    }

    [Fact] // C-03
    public async Task Saving_the_same_key_twice_overwrites_it()
    {
        await using var testDb = new TestDatabase();
        var config = Service(testDb);

        await config.Save(ConfigKeys.AgendaSlotMinutes, "15");
        await config.Save(ConfigKeys.AgendaSlotMinutes, "60");

        (await config.GetInt(ConfigKeys.AgendaSlotMinutes, 30)).Should().Be(60);
        await using var db = testDb.Context();
        db.Settings.Count(c => c.Key == ConfigKeys.AgendaSlotMinutes).Should().Be(1);
    }

    [Fact] // C-04
    public async Task GetInt_falls_back_to_the_default_on_a_non_numeric_value()
    {
        await using var testDb = new TestDatabase();
        var config = Service(testDb);
        await config.Save(ConfigKeys.AgendaSlotMinutes, "molt");

        (await config.GetInt(ConfigKeys.AgendaSlotMinutes, 30)).Should().Be(30);
    }

    [Fact] // C-05
    public async Task GetBool_reads_the_saved_value()
    {
        await using var testDb = new TestDatabase();
        var config = Service(testDb);
        await config.Save(ConfigKeys.ConfirmationSound, "false");

        (await config.GetBool(ConfigKeys.ConfirmationSound, true)).Should().BeFalse();
        (await config.GetBool("clau_absent", true)).Should().BeTrue();
    }

    [Fact] // C-06
    public async Task InvalidateCache_makes_the_next_read_hit_the_database()
    {
        await using var testDb = new TestDatabase();
        var config = Service(testDb);
        await config.Get(ConfigKeys.AgendaSlotMinutes);   // fills the cache with the miss

        await using (var db = testDb.Context())
        {
            db.Settings.Add(new SettingItem { Key = ConfigKeys.AgendaSlotMinutes, Value = "60" });
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        (await config.Get(ConfigKeys.AgendaSlotMinutes)).Should().BeNull("the cache is still valid");
        config.InvalidateCache();
        (await config.Get(ConfigKeys.AgendaSlotMinutes)).Should().Be("60");
    }
}
