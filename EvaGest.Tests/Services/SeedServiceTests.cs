using AwesomeAssertions;
using EvaGest.Models;
using EvaGest.Services;
using EvaGest.Tests.Infra;
using Xunit;

namespace EvaGest.Tests.Services;

/// <summary>
/// Block P: the initial contents. Without a payment method no sale can be saved, so a
/// freshly created database with no seed is an application that installs and then
/// refuses to take money.
/// </summary>
public class SeedServiceTests
{
    private static (SeedService seed, SettingsService config) Build(TestDatabase testDb)
    {
        var factory = new TestFactory(testDb.Options);
        var config = new SettingsService(factory);
        return (new SeedService(factory, config), config);
    }

    [Fact] // P-01
    public async Task A_new_database_gets_payment_methods_to_charge_with()
    {
        await using var testDb = new TestDatabase();
        var (seed, _) = Build(testDb);

        await seed.Seed();

        await using var db = testDb.Context();
        db.PaymentMethods.Select(m => m.Name).Should().Contain("Efectiu");
        db.PaymentMethods.Should().OnlyContain(m => m.Active);
    }

    [Fact] // P-02
    public async Task The_settings_keys_are_created_with_the_documented_values()
    {
        await using var testDb = new TestDatabase();
        var (seed, config) = Build(testDb);

        await seed.Seed();

        (await config.Get(ConfigKeys.CurrentVatMode)).Should().Be(nameof(VatMode.Included));
        (await config.GetInt(ConfigKeys.DefaultVatBp, 0)).Should().Be(2100);
        (await config.GetInt(ConfigKeys.DefaultAppointmentDurationMin, 0)).Should().Be(30);
        (await config.GetInt(ConfigKeys.BackupsToKeep, 0)).Should().Be(15);
        (await config.Get(ConfigKeys.BackupTime)).Should().Be("20:00");
    }

    [Fact] // P-03
    public async Task Booleans_seeded_as_zero_and_one_read_back_correctly()
    {
        await using var testDb = new TestDatabase();
        var (seed, config) = Build(testDb);

        await seed.Seed();

        // The stored shape is "1"/"0", which bool.TryParse rejects: reading these with
        // it alone silently returned the fallback for every single flag.
        (await config.GetBool(ConfigKeys.ShowGuestNotice, false)).Should().BeTrue();
        (await config.GetBool(ConfigKeys.ConfirmationSound, false)).Should().BeTrue();
        (await config.GetBool(ConfigKeys.ApplyVatToTill, true)).Should().BeFalse();
    }

    [Fact] // P-04
    public async Task Seeding_again_does_not_undo_what_the_user_changed()
    {
        await using var testDb = new TestDatabase();
        var (seed, config) = Build(testDb);
        await seed.Seed();

        await config.Save(ConfigKeys.ShopName, "Barberia Eva");
        await config.SaveBool(ConfigKeys.ConfirmationSound, false);

        await using (var db = testDb.Context())
        {
            db.PaymentMethods.RemoveRange(db.PaymentMethods.Where(m => m.Name == "Bizum"));
            await db.SaveChangesAsync();
        }

        await seed.Seed();

        (await config.Get(ConfigKeys.ShopName)).Should().Be("Barberia Eva");
        (await config.GetBool(ConfigKeys.ConfirmationSound, true)).Should().BeFalse();

        await using var check = testDb.Context();
        check.PaymentMethods.Select(m => m.Name).Should().NotContain("Bizum",
            "a deliberately deleted method must not come back on every startup");
    }

    [Fact] // P-05
    public async Task A_new_key_reaches_a_database_that_already_existed()
    {
        await using var testDb = new TestDatabase();
        var (seed, config) = Build(testDb);

        // Simulates an older database that predates the settings key
        await config.Save(ConfigKeys.ShopName, "Barberia Eva");
        (await config.Get(ConfigKeys.BackupTime)).Should().BeNull();

        await seed.Seed();

        (await config.Get(ConfigKeys.BackupTime)).Should().Be("20:00");
    }
}
