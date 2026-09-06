using EvaGest.Services;

namespace EvaGest.Tests.Infra;

/// <summary>
/// Stand-in for the real dialogs: tests never show windows or message boxes. Both
/// answers default to "cancelled", so a test that does not opt in cannot accidentally
/// confirm a destructive action.
/// </summary>
public class DialogServiceDeProva : IDialogService
{
    /// <summary>What <see cref="MostrarDialeg"/> reports the user pressed.</summary>
    public bool ResultatDialeg { get; set; }

    /// <summary>What <see cref="Confirmar"/> reports the user pressed.</summary>
    public bool ResultatConfirmar { get; set; }

    /// <summary>
    /// Runs against the dialog ViewModel before it "closes", which is where a test fills
    /// in the form the user would have typed into. Async because some of those dialogs
    /// save through a service, and an async void handler would race the assertions.
    /// </summary>
    public Func<object, Task>? OmplirDialeg { get; set; }

    public List<object> DialegsMostrats { get; } = [];
    public List<string> ConfirmacionsDemanades { get; } = [];
    public List<string> InformacionsMostrades { get; } = [];

    public string? CarpetaTriada { get; set; }

    public async Task<bool> MostrarDialeg<TViewModel>(TViewModel viewModel) where TViewModel : class
    {
        DialegsMostrats.Add(viewModel);
        if (OmplirDialeg is { } omplir) await omplir(viewModel);
        return ResultatDialeg;
    }

    public Task<bool> Confirmar(string titol, string missatge, string textConfirmar, string textCancellar = "Cancel·lar")
    {
        ConfirmacionsDemanades.Add(titol);
        return Task.FromResult(ResultatConfirmar);
    }

    public Task Informar(string titol, string missatge)
    {
        InformacionsMostrades.Add(titol);
        return Task.CompletedTask;
    }

    public Task<string?> DemanarCarpeta(string titol) => Task.FromResult(CarpetaTriada);
}
