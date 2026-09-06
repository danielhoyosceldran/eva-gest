namespace EvaGest.Services;

/// <summary>
/// The initial contents a brand-new database needs to be usable (CU-12, step E).
/// Runs on every startup, not only the first: it fills in what is missing and never
/// overwrites what the user has already changed.
/// </summary>
public interface ISeedService
{
    Task Sembrar();
}
