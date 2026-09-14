using EvaGest.Data;
using Microsoft.EntityFrameworkCore;

namespace EvaGest.Tests.Infra;

/// <summary>Adapts TestDatabase's options to IDbContextFactory, since the real
/// services depend on the factory abstraction rather than a raw DbContext.</summary>
public sealed class TestFactory(DbContextOptions<ShopDbContext> options)
    : IDbContextFactory<ShopDbContext>
{
    public ShopDbContext CreateDbContext() => new(options);

    public Task<ShopDbContext> CreateDbContextAsync(CancellationToken ct = default)
        => Task.FromResult(CreateDbContext());
}
