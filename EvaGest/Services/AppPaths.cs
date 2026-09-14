using System.IO;

namespace EvaGest.Services;

/// <summary>Resolved paths, injected so services never compute them again.</summary>
public record AppPaths(string DbPath, string BaseFolder)
{
    public string BackupsFolder => Path.Combine(BaseFolder, "Backups");
}
