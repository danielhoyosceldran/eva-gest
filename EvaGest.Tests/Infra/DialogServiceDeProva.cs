using EvaGest.Services;

namespace EvaGest.Tests.Infra;

/// <summary>No-op stand-in: tests never show real windows or message boxes.</summary>
public class DialogServiceDeProva : IDialogService
{
    public Task<bool> MostrarDialeg<TViewModel>(TViewModel viewModel) where TViewModel : class
        => Task.FromResult(false);

    public Task<bool> Confirmar(string titol, string missatge, string textConfirmar, string textCancellar = "Cancel·lar")
        => Task.FromResult(false);

    public Task Informar(string titol, string missatge) => Task.CompletedTask;

    public Task<string?> DemanarCarpeta(string titol) => Task.FromResult<string?>(null);
}
