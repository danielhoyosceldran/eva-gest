using CommunityToolkit.Mvvm.ComponentModel;

namespace EvaGest.ViewModels.Dialegs;

/// <summary>Base for every dialog ViewModel. Raises <see cref="Tancar"/> instead of
/// closing a window directly, so dialog ViewModels stay unaware of WPF (capa-mvvm 3.2).</summary>
public abstract partial class DialegViewModelBase : ObservableObject
{
    [ObservableProperty]
    private string? _errorValidacio;

    public abstract string Titol { get; }

    public event Action<bool>? Tancar;

    protected void SolicitarTancar(bool confirmat) => Tancar?.Invoke(confirmat);
}
