using EvaGest.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace EvaGest.Tests.Infra;

/// <summary>
/// A real SQLite engine held in memory. The connection must stay open for the whole
/// test: SQLite drops an in-memory database as soon as the last connection closes.
/// </summary>
public sealed class TestDatabase : IAsyncDisposable
{
    private readonly SqliteConnection _connection;
    public DbContextOptions<ShopDbContext> Options { get; }

    public TestDatabase()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        Options = new DbContextOptionsBuilder<ShopDbContext>()
            .UseSqlite(_connection)
            .UseSnakeCaseNamingConvention()
            .Options;

        using var db = new ShopDbContext(Options);
        db.Database.EnsureCreated();

        // Foreign keys are off by default in SQLite and must be enabled per connection
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "PRAGMA foreign_keys = ON;";
        cmd.ExecuteNonQuery();
    }

    public ShopDbContext Context() => new(Options);

    public async ValueTask DisposeAsync()
    {
        await _connection.DisposeAsync();
    }
}
