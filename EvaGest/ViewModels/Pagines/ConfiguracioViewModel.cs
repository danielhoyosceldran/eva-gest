using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EvaGest.Helpers;
using EvaGest.Models;
using EvaGest.Services;
using EvaGest.ViewModels.Dialegs;
using EvaGest.ViewModels.Elements;

namespace EvaGest.ViewModels.Pagines;

/// <summary>
/// Configuració (pantalles 2.9). Còpies de seguretat, exportació, granularitat de
/// l'agenda i horari d'obertura. Pendents encara: dades de la barberia, IVA i avisos.
/// </summary>
public partial class ConfiguracioViewModel(
    IBackupService backup, IExportService export, IConfiguracioService configuracio,
    IDisponibilitatService disponibilitat, IDialogService dialegs)
    : PaginaViewModelBase
{
    public override string Titol => "Configuració";

    [ObservableProperty] private string _ultimaCopiaText = "Encara no s'ha fet cap còpia.";

    /// <summary>Row granularity of the agenda grid. Only 15/30/60 are offered, because
    /// anything else would not divide the hour cleanly.</summary>
    public IReadOnlyList<int> OpcionsMinutsSlot { get; } = [15, 30, 60];

    [ObservableProperty] private int _minutsSlot = GraellaHelper.MinutsSlotPerDefecte;

    /// <summary>Guards the first assignment during Carregar from writing straight back.</summary>
    private bool _carregat;

    /// <summary>The seven rows of the opening-hours form, always in weekday order.</summary>
    public ObservableCollection<HorariDiaViewModel> DiesHorari { get; } =
        [.. Enum.GetValues<DiaSetmana>().Select(d => new HorariDiaViewModel { Dia = d })];

    [ObservableProperty] private string? _errorHorari;
    [ObservableProperty] private string? _confirmacioHorari;

    public async Task Carregar()
    {
        var copies = await backup.Llistar();
        var ultima = copies.OrderByDescending(c => c.Data).FirstOrDefault();
        UltimaCopiaText = ultima is null
            ? "Encara no s'ha fet cap còpia."
            : $"Última còpia: {ultima.Data:dd/MM/yyyy HH:mm} ({(ultima.EsAutomatica ? "automàtica" : "manual")})";

        _carregat = false;
        MinutsSlot = GraellaHelper.MinutsSlotValid(
            await configuracio.ObtenirInt(ClausConfig.MinutsSlotAgenda, GraellaHelper.MinutsSlotPerDefecte));
        _carregat = true;

        var horari = await disponibilitat.FranjesSetmanals();
        foreach (var dia in DiesHorari) dia.Omplir(horari.GetValueOrDefault(dia.Dia, []));
        ErrorHorari = null;
        ConfirmacioHorari = null;
    }

    /// <summary>Copies Monday onto Tuesday-Friday, which is how most weeks actually look.</summary>
    [RelayCommand]
    private void AplicarDillunsALaResta()
    {
        var dilluns = DiesHorari[0];
        foreach (var dia in DiesHorari.Skip(1).Take(4)) dilluns.CopiarA(dia);
        ConfirmacioHorari = null;
    }

    [RelayCommand]
    private async Task GuardarHorari()
    {
        ConfirmacioHorari = null;

        // Validate every row first, so all the bad ones light up at once rather than
        // one per attempt.
        var perDia = new Dictionary<DiaSetmana, List<(TimeOnly inici, TimeOnly fi)>>();
        bool hiHaErrors = false;

        foreach (var dia in DiesHorari)
        {
            var resultat = dia.Comprovar();
            if (!resultat.EsValid) { hiHaErrors = true; continue; }
            if (resultat.Franges.Count > 0) perDia[dia.Dia] = resultat.Franges;
        }

        if (hiHaErrors)
        {
            ErrorHorari = "Revisa els dies marcats en vermell.";
            return;
        }

        ErrorHorari = null;
        await disponibilitat.GuardarHorariSetmanal(perDia);
        ConfirmacioHorari = perDia.Count == 0
            ? "Horari desat. Amb tots els dies tancats, l'agenda no sabrà quan obres."
            : "Horari desat. L'agenda ja el fa servir.";
    }

    partial void OnMinutsSlotChanged(int value)
    {
        if (!_carregat) return;
        _ = configuracio.Guardar(ClausConfig.MinutsSlotAgenda,
            GraellaHelper.MinutsSlotValid(value).ToString());
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
