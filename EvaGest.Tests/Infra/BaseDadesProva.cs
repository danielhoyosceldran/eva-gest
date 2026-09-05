using EvaGest.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace EvaGest.Tests.Infra;

/// <summary>
/// A real SQLite engine held in memory. The connection must stay open for the whole
/// test: SQLite drops an in-memory database as soon as the last connection closes.
/// </summary>
public sealed class BaseDadesProva : IAsyncDisposable
{
    private readonly SqliteConnection _connexio;
    public DbContextOptions<BarberiaDbContext> Opcions { get; }

    public BaseDadesProva()
    {
        _connexio = new SqliteConnection("Data Source=:memory:");
        _connexio.Open();

        Opcions = new DbContextOptionsBuilder<BarberiaDbContext>()
            .UseSqlite(_connexio)
            .UseSnakeCaseNamingConvention()
            .Options;

        using var db = new BarberiaDbContext(Opcions);
        db.Database.EnsureCreated();

        // Foreign keys are off by default in SQLite and must be enabled per connection
        using var cmd = _connexio.CreateCommand();
        cmd.CommandText = "PRAGMA foreign_keys = ON;";
        cmd.ExecuteNonQuery();
    }

    public BarberiaDbContext Context() => new(Opcions);

    public async ValueTask DisposeAsync()
    {
        await _connexio.DisposeAsync();
    }
}
