using System.IO;

namespace EvaGest.Services;

/// <summary>Resolved paths, injected so services never compute them again.</summary>
public record RutesApp(string BaseDades, string CarpetaBase)
{
    public string CarpetaBackups => Path.Combine(CarpetaBase, "Backups");
}
