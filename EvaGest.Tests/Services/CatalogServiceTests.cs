using AwesomeAssertions;
using EvaGest.Services;
using EvaGest.Tests.Infra;
using Xunit;

namespace EvaGest.Tests.Services;

/// <summary>
/// Phase 3 smoke tests: the catalogue is the first CRUD, and these confirm the
/// View -> ViewModel -> Service pattern actually reaches a real SQLite engine.
/// </summary>
public class CatalogServiceTests
{
    private static CatalogService CreatesService(TestDatabase testDb)
        => new(new TestFactory(testDb.Options));

    [Fact]
    public async Task Create_and_read_back_a_service()
    {
        await using var testDb = new TestDatabase();
        var catalog = CreatesService(testDb);

        int id = await catalog.CreateService(Make.Service("Tall", 1500, 2100));

        var service = await catalog.GetService(id);
        service.Should().NotBeNull();
        service!.Name.Should().Be("Tall");
        service.PriceCents.Should().Be(1500);
    }

    [Fact]
    public async Task Deactivating_a_service_does_not_delete_it()
    {
        await using var testDb = new TestDatabase();
        var catalog = CreatesService(testDb);

        int id = await catalog.CreateService(Make.Service());
        await catalog.ChangeServiceStatus(id, false);

        var all = await catalog.GetServices();
        var onlyActive = await catalog.GetServices(onlyActive: true);

        all.Should().ContainSingle(s => s.Id == id);
        onlyActive.Should().BeEmpty();
    }

    [Fact]
    public async Task Create_and_deactivate_a_product()
    {
        await using var testDb = new TestDatabase();
        var catalog = CreatesService(testDb);

        int id = await catalog.CreateProduct(Make.Product("Cera", 900, 2100));
        await catalog.ChangeProductStatus(id, false);

        var product = await catalog.GetProduct(id);
        product!.Active.Should().BeFalse();
    }

    [Fact]
    public async Task The_last_active_payment_method_cannot_be_deactivated()
    {
        await using var testDb = new TestDatabase();
        var catalog = CreatesService(testDb);

        int id = await catalog.CreateMethod("Efectiu");

        (await catalog.CanDeactivateMethod(id)).Should().BeFalse();
    }

    [Fact]
    public async Task A_payment_method_can_be_deactivated_while_another_stays_active()
    {
        await using var testDb = new TestDatabase();
        var catalog = CreatesService(testDb);

        int cash = await catalog.CreateMethod("Efectiu");
        await catalog.CreateMethod("Targeta");

        (await catalog.CanDeactivateMethod(cash)).Should().BeTrue();
    }
}
