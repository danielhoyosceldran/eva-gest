using Microsoft.Data.Sqlite;

namespace EvaGest.Tests.Infra;

/// <summary>
/// Releases the pooled connections a test opened on its own database file, and no others.
///
/// The file-database tests used to call <see cref="SqliteConnection.ClearAllPools"/> on
/// cleanup, which also closed the idle pooled connections of whichever other test class
/// was running in parallel. Closing the last connection to a WAL database checkpoints it
/// under an exclusive lock, so a backup or restore that other test was running on its own
/// file at that instant failed now and then with "SQLite Error 5: database is locked"
/// (SQLite's backup API has no busy wait). Clearing only the test's own pool removes that
/// interference.
/// </summary>
public static class SqlitePools
{
    /// <summary>Closes the idle pooled connections opened as <c>Data Source={path}</c>,
    /// the connection string the tests give EF, so the file can be deleted.</summary>
    public static void Release(string path)
    {
        using var connection = new SqliteConnection($"Data Source={path}");
        SqliteConnection.ClearPool(connection);
    }
}
