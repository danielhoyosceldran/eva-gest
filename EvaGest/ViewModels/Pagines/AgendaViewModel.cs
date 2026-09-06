using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EvaGest.Helpers;
using EvaGest.Models;
using EvaGest.Services;
using EvaGest.ViewModels.Dialegs;
using EvaGest.ViewModels.Elements;

namespace EvaGest.ViewModels.Pagines;

/// <summary>
/// Weekly agenda (pantalles 2.2). The week itself is drawn by the shared time grid,
/// which the cita dialog also embeds; this page only owns the toolbar and turns the
/// grid's two callbacks into "new appointment here" and "edit that appointment".
/// </summary>
public partial class AgendaViewModel : PaginaViewModelBase
{
    private readonly ICitaService _cites;
    private readonly IDisponibilitatService _disponibilitat;
    private readonly IClientService _clients;
    private readonly ICatalegService _cataleg;
    private readonly ITreballadoraService _treballadores;
    private readonly IConfiguracioService _configuracio;
    private readonly IDialogService _dialegs;

    public AgendaViewModel(
        ICitaService cites, IDisponibilitatService disponibilitat,
        IClientService clients, ICatalegService cataleg, ITreballadoraService treballadores,
        IConfiguracioService configuracio, IDialogService dialegs)
    {
        _cites = cites;
        _disponibilitat = disponibilitat;
        _clients = clients;
        _cataleg = cataleg;
        _treballadores = treballadores;
        _configuracio = configuracio;
        _dialegs = dialegs;

        Graella = new GraellaSetmanaViewModel(cites, disponibilitat, configuracio, ModeGraella.Agenda,
            alClicarSlot: (data, hora) => _ = NovaCitaA(data, hora),
            alClicarCita: cita => _ = EditarCita(cita));
    }

    public override string Titol => "Agenda";

    public GraellaSetmanaViewModel Graella { get; }

    /// <summary>Which week is shown and its label both live on the grid, so this toolbar
    /// and the picker embedded in the cita dialog drive exactly the same navigation.</summary>
    private DateOnly InicSetmana => Graella.InicSetmana;

    public async Task Carregar()
        => await CarregarSetmana(SetmanaHelper.DilluIrsDeLaSetmana(DateOnly.FromDateTime(DateTime.Today)));

    /// <summary>Toolbar button: books on today when today is in view, otherwise on the
    /// Monday shown, and always at that day's first opening slot rather than a fixed hour.</summary>
    [RelayCommand]
    private async Task NovaCita()
    {
        var avui = DateOnly.FromDateTime(DateTime.Today);
        var data = Graella.Dies.Any(d => d.Data == avui) ? avui : InicSetmana;
        await NovaCitaA(data, GraellaHelper.AHora(Graella.PrimerSlotDe(data)));
    }

    private async Task NovaCitaA(DateOnly data, TimeOnly hora)
    {
        var vm = new CitaDialogViewModel(_cites, _disponibilitat, _clients, _cataleg,
            _treballadores, _configuracio, data, hora);
        if (await _dialegs.MostrarDialeg(vm)) await CarregarSetmana(InicSetmana);
    }

    private async Task EditarCita(Cita cita)
    {
        var vm = new CitaDialogViewModel(_cites, _disponibilitat, _clients, _cataleg,
            _treballadores, _configuracio, cita);
        if (await _dialegs.MostrarDialeg(vm)) await CarregarSetmana(InicSetmana);
    }

    private async Task CarregarSetmana(DateOnly dilluns)
    {
        Carregant = true;
        try { await Graella.CarregarSetmana(dilluns); }
        finally { Carregant = false; }
    }
}
