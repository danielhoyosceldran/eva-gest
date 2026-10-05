namespace EvaGest.Services;

/// <summary>
/// The backup is a sound EvaGest database, but a later version of the app made it: its
/// migration history names migrations this build does not have. Restoring it would leave
/// this version reading tables and columns it does not know, so the restore is refused
/// before the live database is touched. Its own type rather than an
/// InvalidDataException (sealed) because the way out is different: update the app, not
/// just pick another copy.
/// </summary>
public class BackupFromNewerVersionException(string path, IReadOnlyCollection<string> unknownMigrations)
    : Exception(
        $"The backup {path} was made by a newer version of EvaGest " +
        $"(unknown migrations: {string.Join(", ", unknownMigrations)}).")
{
    public IReadOnlyCollection<string> UnknownMigrations { get; } = unknownMigrations;
}
