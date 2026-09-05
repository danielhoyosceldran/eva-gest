using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EvaGest.Services;
using EvaGest.ViewModels.Dialegs;

namespace EvaGest.ViewModels.Pagines;

/// <summary>
/// Configuració (pantalles 2.9). Fase 9 builds only the "Còpies de seguretat" and
/// export blocks; the rest (dades de barberia, horaris, IVA, avisos) needs
/// ConfiguracioService, not yet built.
/// </summary>
public partial class ConfiguracioViewModel(IBackupService backup, IExportService export, IDialogService dialegs)
    : PaginaViewModelBase
{
    public override string Titol => "Configuració";

    [ObservableProperty] private string _ultimaCopiaText = "Encara no s'ha fet cap còpia.";

    public async Task Carregar()
    {
        var copies = await backup.Llistar();
        var ultima = copies.OrderByDescending(c => c.Data).FirstOrDefault();
        UltimaCopiaText = ultima is null
            ? "Encara no s'ha fet cap còpia."
            : $"Última còpia: {ultima.Data:dd/MM/yyyy HH:mm} ({(ultima.EsAutomatica ? "automàtica" : "manual")})";
    }

    [RelayCommand]
    private async Task FerCopiaAra()
    {
        var copia = await backup.FerCopiaManual();
        await dialegs.Informar("Còpia feta",
            $"S'ha creat la còpia de seguretat del {copia.Data:dd/MM/yyyy HH:mm}.");
        await Carregar();
    }

    [RelayCommand]
    private async Task RestaurarCopia()
    {
        var vm = new RestaurarDialogViewModel(backup, dialegs);
        await vm.Carregar();
        if (await dialegs.MostrarDialeg(vm)) await Carregar();
    }

    [RelayCommand]
    private async Task ExportarDades()
    {
        var vm = new ExportDialogViewModel(export, dialegs);
        await dialegs.MostrarDialeg(vm);
    }
}
