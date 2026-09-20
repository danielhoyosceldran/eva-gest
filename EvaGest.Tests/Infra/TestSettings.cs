using EvaGest.Services;

namespace EvaGest.Tests.Infra;

/// <summary>
/// In-memory settings, for the services that need to read a key but whose test has no
/// database of its own (the backup service works on plain rows). Starts empty, so a
/// test that does not set a key exercises the service's own fallback.
/// </summary>
public class TestSettings : ISettingsService
{
    private readonly Dictionary<string, string> _values = [];

    public TestSettings(params (string key, string value)[] initial)
    {
        foreach (var (key, value) in initial) _values[key] = value;
    }

    public int TimesInvalidated { get; private set; }

    public Task<string?> Get(string key) => Task.FromResult(_values.GetValueOrDefault(key));

    public Task<int> GetInt(string key, int perDefault)
        => Task.FromResult(int.TryParse(_values.GetValueOrDefault(key), out int v) ? v : perDefault);

    public Task<bool> GetBool(string key, bool perDefault)
    {
        string? value = _values.GetValueOrDefault(key);
        if (string.IsNullOrEmpty(value)) return Task.FromResult(perDefault);
        if (value is "1") return Task.FromResult(true);
        if (value is "0") return Task.FromResult(false);
        return Task.FromResult(bool.TryParse(value, out bool r) ? r : perDefault);
    }

    /// <summary>Keys whose write should fail, so a test can check what the caller does
    /// when the database refuses it rather than only the happy path.</summary>
    public HashSet<string> FailsToSave { get; } = [];

    public Task Save(string key, string value)
    {
        if (FailsToSave.Contains(key))
            return Task.FromException(new InvalidOperationException($"refusing to save {key}"));

        _values[key] = value;
        return Task.CompletedTask;
    }

    public Task SaveBool(string key, bool value) => Save(key, value ? "1" : "0");

    public void InvalidateCache() => TimesInvalidated++;
}
