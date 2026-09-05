using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EvaGest.Helpers;
using EvaGest.Models;
using EvaGest.Services;
using EvaGest.ViewModels.Dialegs;

namespace EvaGest.ViewModels.Pagines;

/// <summary>
/// Weekly agenda (pantalles 2.2). Keeps one week loaded at a time: changing week
/// replaces the 7 collections in a single round trip (ICitaService.ObtenirPerRang),
/// not one query per day.
/// </summary>
public partial class AgendaViewModel(
    ICitaService cites, IDisponibilitatService disponibilitat,
    IClientService clients, ICatalegService cataleg, ITreballadoraService treballadores,
    IDialogService dialegs) : PaginaViewModelBase
{
    public override string Titol => "Agenda";

    [ObservableProperty] private DateOnly _inicSetmana;
    [ObservableProperty] private string _rangText = string.Empty;
    [ObservableProperty] private DiaAgendaViewModel? _diaSeleccionat;

    public ObservableCollection<DiaAgendaViewModel> Dies { get; } = [];

    public async Task Carregar() => await CarregarSetmana(SetmanaHelper.DilluIrsDeLaSetmana(DateOnly.FromDateTime(DateTime.Today)));

    [RelayCommand] private async Task SetmanaAnterior() => await CarregarSetmana(InicSetmana.AddDays(-7));
    [RelayCommand] private async Task SetmanaSeguent() => await CarregarSetmana(InicSetmana.AddDays(7));
    [RelayCommand] private async Task AquestaSetmana()
        => await CarregarSetmana(SetmanaHelper.DilluIrsDeLaSetmana(DateOnly.FromDateTime(DateTime.Today)));

    [RelayCommand] private void SeleccionarDia(DiaAgendaViewModel dia) => DiaSeleccionat = dia;

    [RelayCommand]
    private async Task NovaCita()
    {
        var data = DiaSeleccionat?.Data ?? InicSetmana;
        var vm = new CitaDialogViewModel(cites, disponibilitat, clients, cataleg, treballadores, data);
        if (await dialegs.MostrarDialeg(vm)) await CarregarSetmana(InicSetmana);
    }

    [RelayCommand]
    private async Task EditarCita(Cita cita)
    {
        var vm = new CitaDialogViewModel(cites, disponibilitat, clients, cataleg, treballadores, cita);
        if (await dialegs.MostrarDialeg(vm)) await CarregarSetmana(InicSetmana);
    }

    /// <summary>
    /// Loads all seven days of the week in one round trip, then distributes the
    /// appointments client-side.
    /// </summary>
    private async Task CarregarSetmana(DateOnly dilluns)
    {
        Carregant = true;
        try
        {
            InicSetmana = dilluns;
            var diumenge = dilluns.AddDays(6);
            RangText = FormatarRang(dilluns, diumenge);

            var totes = await cites.ObtenirPerRang(dilluns, diumenge);
            var tancats = await disponibilitat.DiesTancatsA(dilluns, diumenge);

            var seleccionatData = DiaSeleccionat?.Data;
            Dies.Clear();
            for (int i = 0; i < 7; i++)
            {
                var data = dilluns.AddDays(i);
                var dia = new DiaAgendaViewModel
                {
                    Data = data,
                    EsAvui = data == DateOnly.FromDateTime(DateTime.Today),
                    Tancat = tancats.ContainsKey(data),
                    MotiuTancat = tancats.GetValueOrDefault(data),
                    Cites = new(totes.Where(c => c.Data == data).OrderBy(c => c.Hora))
                };
                Dies.Add(dia);
            }
            DiaSeleccionat = Dies.FirstOrDefault(d => d.Data == seleccionatData)
                          ?? Dies.FirstOrDefault(d => d.EsAvui)
                          ?? Dies[0];
        }
        finally { Carregant = false; }
    }

    private static string FormatarRang(DateOnly dilluns, DateOnly diumenge)
    {
        var cultura = new CultureInfo("ca-ES");
        return dilluns.Month == diumenge.Month
            ? $"{dilluns.Day} – {diumenge.Day} de {diumenge.ToString("MMMM yyyy", cultura)}"
            : $"{dilluns.ToString("d MMM", cultura)} – {diumenge.ToString("d MMM yyyy", cultura)}";
    }
}
