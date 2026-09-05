using CommunityToolkit.Mvvm.ComponentModel;

namespace EvaGest.ViewModels.Pagines;

/// <summary>Common shape for every page ViewModel: a title and a loading flag,
/// so the shell and each page share the same binding names.</summary>
public abstract partial class PaginaViewModelBase : ObservableObject
{
    [ObservableProperty]
    private bool _carregant;

    public abstract string Titol { get; }
}
