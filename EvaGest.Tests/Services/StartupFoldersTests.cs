using AwesomeAssertions;
using EvaGest.Services;
using Xunit;

namespace EvaGest.Tests.Services;

/// <summary>
/// E-02. The first step of startup, which used to sit unguarded in App.OnStartup: any
/// failure in it left an invisible process holding the single-instance mutex. App now
/// catches whatever <see cref="StartupFolders.Prepare"/> throws; these tests pin down
/// what it does on the good path and that each broken setup fails loudly there, before
/// a window or a database is involved.
/// </summary>
public sealed class StartupFoldersTests : IDisposable
{
    private readonly string _app = Path.Combine(Path.GetTempPath(), "EvaGestStartup_" + Guid.NewGuid());

    public StartupFoldersTests() => Directory.CreateDirectory(_app);

    public void Dispose() => Directory.Delete(_app, recursive: true);

    private void WriteSettings(string json) => File.WriteAllText(Path.Combine(_app, "appsettings.json"), json);

    private static string Json(string path) => $$"""{ "DatabasePath": "{{path.Replace("\\", "\\\\")}}" }""";

    [Fact]
    public void Prepares_the_database_folder_with_its_backups_and_logs()
    {
        string data = Path.Combine(_app, "data");
        WriteSettings(Json(Path.Combine(data, "barberia.db")));

        var layout = StartupFolders.Prepare(_app);

        layout.DbPath.Should().Be(Path.Combine(data, "barberia.db"));
        layout.BaseFolder.Should().Be(data);
        Directory.Exists(Path.Combine(data, "Backups")).Should().BeTrue();
        Directory.Exists(layout.LogsFolder).Should().BeTrue();
    }

    [Fact]
    public void Expands_environment_variables_in_the_path()
    {
        Environment.SetEnvironmentVariable("EVAGEST_TEST_ROOT", _app);
        WriteSettings(Json("%EVAGEST_TEST_ROOT%\\env\\barberia.db"));

        var layout = StartupFolders.Prepare(_app);

        layout.BaseFolder.Should().Be(Path.Combine(_app, "env"));
    }

    [Fact]
    public void A_missing_settings_file_fails_here()
    {
        var prepare = () => StartupFolders.Prepare(_app);

        prepare.Should().Throw<FileNotFoundException>();
    }

    [Fact]
    public void A_settings_file_without_DatabasePath_fails_here()
    {
        WriteSettings("{ }");

        var prepare = () => StartupFolders.Prepare(_app);

        prepare.Should().Throw<InvalidOperationException>().WithMessage("*DatabasePath*");
    }

    [Fact]
    public void A_corrupt_settings_file_fails_here()
    {
        WriteSettings("{ \"DatabasePath\": ");

        var prepare = () => StartupFolders.Prepare(_app);

        prepare.Should().Throw<Exception>();
    }

    [Fact]
    public void A_folder_that_cannot_be_created_fails_here()
    {
        // A file where the data folder should be: CreateDirectory cannot make it.
        string blocker = Path.Combine(_app, "blocked");
        File.WriteAllText(blocker, "");
        WriteSettings(Json(Path.Combine(blocker, "barberia.db")));

        var prepare = () => StartupFolders.Prepare(_app);

        prepare.Should().Throw<IOException>();
    }

    [Fact]
    public void An_early_failure_is_written_where_it_can_be_found()
    {
        string marker = "early-failure-" + Guid.NewGuid();

        StartupFolders.RecordEarlyFailure(new InvalidOperationException(marker));

        File.ReadAllText(StartupFolders.FallbackLogFile).Should().Contain(marker);
    }
}
