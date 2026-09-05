using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EvaGest.Models;

namespace EvaGest.ViewModels.Dialegs;

public partial class MetodePagamentDialogViewModel : DialegViewModelBase
{
    private readonly int? _id;

    [ObservableProperty] private string _nom = string.Empty;

    public override string Titol => _id is null ? "Nou mètode de pagament" : "Editar mètode de pagament";

    public MetodePagamentDialogViewModel() { }

    public MetodePagamentDialogViewModel(MetodePagament metode)
    {
        _id = metode.Id;
        Nom = metode.Nom;
    }

    [RelayCommand]
    private void Guardar()
    {
        if (string.IsNullOrWhiteSpace(Nom))
        {
            ErrorValidacio = "Cal indicar el nom del mètode per guardar.";
            return;
        }

        ErrorValidacio = null;
        SolicitarTancar(true);
    }

    [RelayCommand]
    private void Cancellar() => SolicitarTancar(false);

    public MetodePagament AModel() => new() { Id = _id ?? 0, Nom = Nom.Trim(), Actiu = true };
}
