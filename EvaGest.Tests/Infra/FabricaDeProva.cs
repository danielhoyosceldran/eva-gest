using EvaGest.Data;
using Microsoft.EntityFrameworkCore;

namespace EvaGest.Tests.Infra;

/// <summary>Adapts BaseDadesProva's options to IDbContextFactory, since the real
/// services depend on the factory abstraction rather than a raw DbContext.</summary>
public sealed class FabricaDeProva(DbContextOptions<BarberiaDbContext> opcions)
    : IDbContextFactory<BarberiaDbContext>
{
    public BarberiaDbContext CreateDbContext() => new(opcions);

    public Task<BarberiaDbContext> CreateDbContextAsync(CancellationToken ct = default)
        => Task.FromResult(CreateDbContext());
}
