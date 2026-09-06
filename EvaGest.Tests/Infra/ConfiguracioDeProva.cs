using EvaGest.Services;

namespace EvaGest.Tests.Infra;

/// <summary>
/// In-memory settings, for the services that need to read a key but whose test has no
/// database of its own (the backup service works on plain files). Starts empty, so a
/// test that does not set a key exercises the service's own fallback.
/// </summary>
public class ConfiguracioDeProva : IConfiguracioService
{
    private readonly Dictionary<string, string> _valors = [];

    public ConfiguracioDeProva(params (string clau, string valor)[] inicials)
    {
        foreach (var (clau, valor) in inicials) _valors[clau] = valor;
    }

    public int VegadesInvalidada { get; private set; }

    public Task<string?> Obtenir(string clau) => Task.FromResult(_valors.GetValueOrDefault(clau));

    public Task<int> ObtenirInt(string clau, int perDefecte)
        => Task.FromResult(int.TryParse(_valors.GetValueOrDefault(clau), out int v) ? v : perDefecte);

    public Task<bool> ObtenirBool(string clau, bool perDefecte)
    {
        string? valor = _valors.GetValueOrDefault(clau);
        if (string.IsNullOrEmpty(valor)) return Task.FromResult(perDefecte);
        if (valor is "1") return Task.FromResult(true);
        if (valor is "0") return Task.FromResult(false);
        return Task.FromResult(bool.TryParse(valor, out bool r) ? r : perDefecte);
    }

    public Task Guardar(string clau, string valor)
    {
        _valors[clau] = valor;
        return Task.CompletedTask;
    }

    public Task GuardarBool(string clau, bool valor) => Guardar(clau, valor ? "1" : "0");

    public void InvalidarCache() => VegadesInvalidada++;
}
