using System.IO;
using Microsoft.Extensions.Configuration;
using Serilog;

namespace EvaGest.Services;

/// <summary>
/// The first step of startup: where the database lives, according to appsettings.json,
/// and the folders around it (Backups, Logs) created on first run. Kept out of
/// App.xaml.cs so what it does, and how it fails, can be tested without a desktop.
/// </summary>
public static class StartupFolders
{
    /// <summary>The database file and the folder that holds it and its Backups/Logs.</summary>
    public sealed record Layout(string DbPath, string BaseFolder)
    {
        public string LogsFolder => Path.Combine(BaseFolder, "Logs");
    }

    /// <summary>
    /// Reads DatabasePath from the appsettings.json in <paramref name="appDirectory"/>,
    /// expands variables such as %LOCALAPPDATA% and makes sure the three folders exist.
    /// </summary>
    /// <exception cref="Exception">The file is missing or unreadable, it has no
    /// DatabasePath, or a folder cannot be created. The caller must stop the startup:
    /// with no folder for the database there is nothing to open.</exception>
    public static Layout Prepare(string appDirectory)
    {
        var config = new ConfigurationBuilder()
            .SetBasePath(appDirectory)
            .AddJsonFile("appsettings.json")
            .Build();

        string dbPath = Environment.ExpandEnvironmentVariables(
            config["DatabasePath"] ?? throw new InvalidOperationException(
                "appsettings.json is missing DatabasePath"));

        // GetDirectoryName is null for a bare root ("C:\"); an empty one means a file
        // name with no folder, which would land wherever the process happened to start.
        string baseFolder = Path.GetDirectoryName(Path.GetFullPath(dbPath))
            ?? throw new InvalidOperationException($"DatabasePath '{dbPath}' has no folder");

        Directory.CreateDirectory(baseFolder);
        Directory.CreateDirectory(Path.Combine(baseFolder, "Backups"));
        Directory.CreateDirectory(Path.Combine(baseFolder, "Logs"));

        return new Layout(dbPath, baseFolder);
    }

    /// <summary>
    /// Where a failure that happens before the day's log exists is written (E-02): the
    /// real log lives in a folder this step may have just failed to find or create, and
    /// the static logger is still the silent default, so that failure used to leave no
    /// trace at all. The temp folder is the one place the process can always write.
    /// </summary>
    public static string FallbackLogFile =>
        Path.Combine(Path.GetTempPath(), "EvaGest", "startup-failure.txt");

    /// <summary>Records <paramref name="ex"/> in <see cref="FallbackLogFile"/>. Never
    /// throws: it runs while the app is already failing and must not hide why.</summary>
    public static void RecordEarlyFailure(Exception ex)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FallbackLogFile)!);
            using var log = new LoggerConfiguration()
                .WriteTo.File(FallbackLogFile)
                .CreateLogger();
            log.Fatal(ex, "Startup failed before the log was configured");
        }
        catch (Exception logEx)
        {
            // Nowhere left on disk to write it. The debugger output and the message box
            // the caller shows next are the only record, which is still more than the
            // silent hang this replaces.
            System.Diagnostics.Trace.TraceError($"Startup failed: {ex}\nand could not be logged: {logEx}");
        }
    }
}
