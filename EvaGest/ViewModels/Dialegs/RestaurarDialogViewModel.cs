using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EvaGest.Services;

namespace EvaGest.ViewModels.Dialegs;

/// <summary>Restaurar còpia de seguretat, pantalles 3.9.</summary>
public partial class RestaurarDialogViewModel : DialegViewModelBase
{
    private readonly IBackupService _backup;
    private readonly IDialogService _dialegs;

    public ObservableCollection<CopiaSeguretat> Copies { get; } = [];

    [ObservableProperty] private CopiaSeguretat? _seleccionada;

    public override string Titol => "Restaurar còpia de seguretat";

    public RestaurarDialogViewModel(IBackupService backup, IDialogService dialegs)
    {
        _backup = backup;
        _dialegs = dialegs;
    }

    public async Task Carregar()
    {
        Copies.Clear();
        foreach (var c in await _backup.Llistar()) Copies.Add(c);
    }

    [RelayCommand]
    private async Task Restaurar()
    {
        if (Seleccionada is null)
        {
            ErrorValidacio = "Tria una còpia de la llista.";
            return;
        }

        bool confirmat = await _dialegs.Confirmar(
            "Restaurar aquesta còpia?",
            "Es reemplaçaran totes les dades actuals per les d'aquesta còpia. "
            + "L'estat actual es desarà primer com a còpia manual, per si cal desfer-ho.\n\n"
            + "Caldrà reiniciar l'aplicació perquè els canvis tinguin efecte.",
            "Restaurar");

        if (!confirmat) return;

        await _backup.Restaurar(Seleccionada.Ruta);
        await _dialegs.Informar("Còpia restaurada",
            "Les dades s'han restaurat correctament. Tanca i torna a obrir l'aplicació.");

        ErrorValidacio = null;
        SolicitarTancar(true);
    }

    [RelayCommand]
    private void Cancellar() => SolicitarTancar(false);
}
