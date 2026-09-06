using System.Collections.ObjectModel;
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
/// which the cita dialog also embeds; this page owns the toolbar, turns the grid's
/// callbacks into "new appointment here" and "edit that appointment", and hosts the
/// day detail that the compact grid cards deliberately leave out.
/// </summary>
public partial class AgendaViewModel : PaginaViewModelBase
{
    private static readonly CultureInfo Cultura = new("ca-ES");

    private readonly ICitaService _cites;
    private readonly IVendaService _vendes;
    private readonly IDisponibilitatService _disponibilitat;
    private readonly IClientService _clients;
    private readonly ICatalegService _cataleg;
    private readonly ITreballadoraService _treballadores;
    private readonly IConfiguracioService _configuracio;
    private readonly ISoundService _so;
    private readonly IDialogService _dialegs;

    public AgendaViewModel(
        ICitaService cites, IVendaService vendes, IDisponibilitatService disponibilitat,
        IClientService clients, ICatalegService cataleg, ITreballadoraService treballadores,
        IConfiguracioService configuracio, ISoundService so, IDialogService dialegs)
    {
        _cites = cites;
        _vendes = vendes;
        _disponibilitat = disponibilitat;
        _clients = clients;
        _cataleg = cataleg;
        _treballadores = treballadores;
        _configuracio = configuracio;
        _so = so;
        _dialegs = dialegs;

        Graella = new GraellaSetmanaViewModel(cites, disponibilitat, configuracio, ModeGraella.Agenda,
            alClicarSlot: (data, hora) => _ = NovaCitaA(data, hora),
            alClicarCita: cita => _ = EditarCita(cita),
            alSeleccionarDia: data => _ = SeleccionarDia(data));
    }

    public override string Titol => "Agenda";

    public GraellaSetmanaViewModel Graella { get; }

    /// <summary>
    /// One row of the day detail. The grid cards hide their status buttons until hover
    /// so a busy week stays readable; here they are always visible, which is the reason
    /// the panel exists at all.
    /// </summary>
    public record FilaDia(
        Cita Cita, string HoraText, string DuradaText, string ClientText, bool EsConvidat,
        string ServeiText, string TreballadoraText, string ColorHex, string EstatText, bool EsPendent);

    [ObservableProperty] private DateOnly? _diaSeleccionat;
    [ObservableProperty] private string _diaSeleccionatText = string.Empty;

    public ObservableCollection<FilaDia> CitesDelDia { get; } = [];

    public bool HiHaDiaSeleccionat => DiaSeleccionat is not null;
    public bool DiaSenseCites => DiaSeleccionat is not null && CitesDelDia.Count == 0;

    /// <summary>Which week is shown and its label both live on the grid, so this toolbar
    /// and the picker embedded in the cita dialog drive exactly the same navigation.</summary>
    private DateOnly InicSetmana => Graella.InicSetmana;

    public async Task Carregar()
        => await CarregarSetmana(SetmanaHelper.DilluIrsDeLaSetmana(DateOnly.FromDateTime(DateTime.Today)));

    /// <summary>Toolbar button: books on the day being examined if there is one, else on
    /// today when today is in view, else on the Monday shown — and always at that day's
    /// first opening slot rather than a fixed hour.</summary>
    [RelayCommand]
    private async Task NovaCita()
    {
        var avui = DateOnly.FromDateTime(DateTime.Today);
        var data = DiaSeleccionat
                   ?? (Graella.Dies.Any(d => d.Data == avui) ? avui : InicSetmana);
        await NovaCitaA(data, GraellaHelper.AHora(Graella.PrimerSlotDe(data)));
    }

    private async Task NovaCitaA(DateOnly data, TimeOnly hora)
    {
        var vm = new CitaDialogViewModel(_cites, _disponibilitat, _clients, _cataleg,
            _treballadores, _configuracio, _dialegs, data, hora);
        if (await _dialegs.MostrarDialeg(vm)) await CarregarSetmana(InicSetmana);
    }

    private async Task EditarCita(Cita cita)
    {
        var vm = new CitaDialogViewModel(_cites, _disponibilitat, _clients, _cataleg,
            _treballadores, _configuracio, _dialegs, cita);
        if (await _dialegs.MostrarDialeg(vm)) await CarregarSetmana(InicSetmana);
    }

    // ── Detall del dia ──────────────────────────────────────────────────────

    [RelayCommand]
    private async Task SeleccionarDia(DateOnly data)
    {
        DiaSeleccionat = data;
        DiaSeleccionatText = Capitalitzar(data.ToString("dddd d 'de' MMMM", Cultura));
        await CarregarDetallDelDia();
    }

    [RelayCommand]
    private void TancarDetall()
    {
        DiaSeleccionat = null;
        CitesDelDia.Clear();
        OnPropertyChanged(nameof(HiHaDiaSeleccionat));
        OnPropertyChanged(nameof(DiaSenseCites));
    }

    /// <summary>
    /// Same as the Inici page: marking an appointment as done opens the sale dialog
    /// prefilled from it (CU-02). Closing that without charging leaves the appointment
    /// Realitzada and unpaid, which is a state the business really has.
    /// </summary>
    [RelayCommand]
    private async Task MarcarRealitzada(Cita cita)
    {
        await _cites.CanviarEstat(cita.Id, EstatCita.Realitzada);

        var vm = await VendaDialogViewModel.DesDeCita(
            _vendes, _clients, _cataleg, _treballadores, _so, _configuracio, _dialegs, cita);
        await _dialegs.MostrarDialeg(vm);

        await CarregarSetmana(InicSetmana);
    }

    [RelayCommand] private Task MarcarCancellada(Cita cita) => CanviarEstat(cita, EstatCita.Cancellada);
    [RelayCommand] private Task MarcarNoAssistida(Cita cita) => CanviarEstat(cita, EstatCita.NoAssistida);

    private async Task CanviarEstat(Cita cita, EstatCita nou)
    {
        await _cites.CanviarEstat(cita.Id, nou);
        await CarregarSetmana(InicSetmana);
    }

    private async Task CarregarDetallDelDia()
    {
        CitesDelDia.Clear();

        if (DiaSeleccionat is DateOnly dia)
            foreach (var cita in await _cites.ObtenirPerDia(dia))
                CitesDelDia.Add(AFila(cita));

        OnPropertyChanged(nameof(HiHaDiaSeleccionat));
        OnPropertyChanged(nameof(DiaSenseCites));
    }

    private static FilaDia AFila(Cita cita) => new(
        cita,
        cita.Hora.ToString("HH\\:mm"),
        $"{cita.DuradaMin} min",
        cita.NomMostrat,
        cita.ClientId is null,
        cita.Servei?.Nom ?? "Sense servei",
        cita.Treballadora?.Nom ?? "Sense assignar",
        cita.Treballadora?.Color ?? "#00000000",
        Etiquetes.Text(cita.Estat),
        cita.Estat == EstatCita.Pendent);

    private static string Capitalitzar(string text)
        => text.Length == 0 ? text : char.ToUpper(text[0], Cultura) + text[1..];

    private async Task CarregarSetmana(DateOnly dilluns)
    {
        Carregant = true;
        try
        {
            await Graella.CarregarSetmana(dilluns);

            // The detail panel would otherwise keep showing the appointments of a day
            // that is no longer on screen, or stale rows after an edit.
            if (DiaSeleccionat is DateOnly dia && Graella.Dies.Any(d => d.Data == dia))
                await CarregarDetallDelDia();
            else
                TancarDetall();
        }
        finally { Carregant = false; }
    }
}
