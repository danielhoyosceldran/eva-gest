using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EvaGest.Helpers;
using EvaGest.Models;
using EvaGest.Services;

namespace EvaGest.ViewModels.Elements;

/// <summary>Full-size on the Agenda page, compact when embedded in the cita dialog.</summary>
public enum ModeGraella { Agenda, Selector }

/// <summary>
/// The weekly time grid, shared by the Agenda page and the cita dialog. The only
/// behavioural difference between the two hosts is the pair of callbacks passed in,
/// so the layout, the lane packing and the loading pipeline exist exactly once.
///
/// Nothing here may touch Dispatcher, Brush or Application.Current: the tests build
/// this off the UI thread. The now-line timer lives in the view's code-behind.
/// </summary>
public partial class GraellaSetmanaViewModel : ObservableObject
{
    private readonly ICitaService _cites;
    private readonly IDisponibilitatService _disponibilitat;
    private readonly IConfiguracioService _configuracio;
    private readonly Action<DateOnly, TimeOnly> _alClicarSlot;
    private readonly Action<Cita>? _alClicarCita;
    private readonly Action<DateOnly>? _alSeleccionarDia;

    private CitaGraellaViewModel? _fantasma;
    private DateOnly? _dataFantasma;

    public GraellaSetmanaViewModel(
        ICitaService cites, IDisponibilitatService disponibilitat, IConfiguracioService configuracio,
        ModeGraella mode,
        Action<DateOnly, TimeOnly> alClicarSlot, Action<Cita>? alClicarCita = null,
        Action<DateOnly>? alSeleccionarDia = null)
    {
        _cites = cites;
        _disponibilitat = disponibilitat;
        _configuracio = configuracio;
        _alClicarSlot = alClicarSlot;
        _alClicarCita = alClicarCita;
        _alSeleccionarDia = alSeleccionarDia;

        Mode = mode;
        AlcadaSlotPx = mode is ModeGraella.Agenda ? 44 : 26;
        AmpladaReglaPx = mode is ModeGraella.Agenda ? 64 : 44;
        AlcadaMinimaCitaPx = mode is ModeGraella.Agenda ? 22 : 14;

        for (int i = 0; i < 7; i++) Dies.Add(new DiaGraellaViewModel(this));
    }

    public ModeGraella Mode { get; }
    public double AlcadaSlotPx { get; }
    public double AmpladaReglaPx { get; }
    public double AlcadaMinimaCitaPx { get; }

    [ObservableProperty] private DateOnly _inicSetmana;
    [ObservableProperty] private string _rangText = string.Empty;
    [ObservableProperty] private bool _carregant;
    [ObservableProperty] private int _minutsSlot = GraellaHelper.MinutsSlotPerDefecte;
    [ObservableProperty] private int _minutIniciGraella = GraellaHelper.RangPerDefecte.inici;
    [ObservableProperty] private int _minutFiGraella = GraellaHelper.RangPerDefecte.fi;
    [ObservableProperty] private double _pixelsPerMinut;
    [ObservableProperty] private double _alcadaTotalPx;
    [ObservableProperty] private double _alcadaHoraPx;
    [ObservableProperty] private double _desplacamentInicialPx;
    [ObservableProperty] private double _araTop;
    [ObservableProperty] private bool _araVisible;
    [ObservableProperty] private int? _citaSeleccionadaId;

    public ObservableCollection<DiaGraellaViewModel> Dies { get; } = [];
    public ObservableCollection<HoraReglaViewModel> Hores { get; } = [];

    /// <summary>First opening minute of the week, used for the initial scroll position.</summary>
    private int _primerMinutObert = GraellaHelper.RangPerDefecte.inici;

    /// <summary>First opening minute per weekday, for "book on this day" defaults.</summary>
    private Dictionary<DiaSetmana, int> _obertures = [];

    [RelayCommand]
    private void ClicCita(CitaGraellaViewModel? bloc)
    {
        if (bloc?.Model is { } cita) _alClicarCita?.Invoke(cita);
    }

    // Week navigation lives on the grid itself so both hosts get it: the Agenda toolbar
    // and the picker embedded in the cita dialog, which otherwise had no way to leave
    // the current week.
    [RelayCommand] private async Task SetmanaAnterior() => await CarregarSetmana(InicSetmana.AddDays(-7));
    [RelayCommand] private async Task SetmanaSeguent() => await CarregarSetmana(InicSetmana.AddDays(7));
    [RelayCommand] private async Task AquestaSetmana()
        => await CarregarSetmana(SetmanaHelper.DilluIrsDeLaSetmana(DateOnly.FromDateTime(DateTime.Today)));

    /// <summary>The whole day header books on that day, at its first opening slot.</summary>
    [RelayCommand]
    /// <summary>
    /// The day header opens that day's detail when the host offers one (pantalles 2.2).
    /// Without a handler — the cita dialog's picker — it keeps its older meaning of
    /// "book at this day's first open slot", which is the only useful action there.
    /// </summary>
    private void ClicCapcalera(DiaGraellaViewModel? dia)
    {
        if (dia is null) return;

        if (_alSeleccionarDia is { } seleccionar) seleccionar(dia.Data);
        else ActivarSlot(dia.Data, GraellaHelper.AHora(PrimerSlotDe(dia.Data)));
    }

    /// <summary>Entry point for a click on empty grid space, from DiaGraellaViewModel.</summary>
    public void ActivarSlot(DateOnly data, TimeOnly hora) => _alClicarSlot(data, hora);

    /// <summary>First bookable slot of a day: its opening time, or the top of the grid
    /// when the day has no schedule. Used by the header and by the toolbar button, so a
    /// new appointment never defaults to an arbitrary fixed hour.</summary>
    public int PrimerSlotDe(DateOnly data)
    {
        int minut = _obertures.GetValueOrDefault(SetmanaHelper.ADiaSetmana(data), _primerMinutObert);
        return Math.Max(minut, MinutIniciGraella);
    }

    // ---------- Navegació amb teclat ----------

    private int _diaEnfocat = -1;
    private int _minutEnfocat;

    /// <summary>Arrow keys walk the grid; deltaDies moves sideways, deltaSlots up/down.</summary>
    public void MoureFocus(int deltaDies, int deltaSlots)
    {
        if (_diaEnfocat < 0)
        {
            _diaEnfocat = Math.Max(0, Dies.ToList().FindIndex(d => d.EsAvui));
            _minutEnfocat = PrimerSlotDe(Dies[_diaEnfocat].Data);
        }
        else
        {
            _diaEnfocat = Math.Clamp(_diaEnfocat + deltaDies, 0, Dies.Count - 1);
            _minutEnfocat = Math.Clamp(_minutEnfocat + deltaSlots * MinutsSlot,
                                       MinutIniciGraella, Math.Max(MinutIniciGraella, MinutFiGraella - MinutsSlot));
        }
        PintarFocus();
    }

    public void AnarAlPrimerSlot()
    {
        if (_diaEnfocat < 0) _diaEnfocat = Math.Max(0, Dies.ToList().FindIndex(d => d.EsAvui));
        _minutEnfocat = PrimerSlotDe(Dies[_diaEnfocat].Data);
        PintarFocus();
    }

    public void ActivarFocus()
    {
        if (_diaEnfocat < 0) return;
        ActivarSlot(Dies[_diaEnfocat].Data, GraellaHelper.AHora(_minutEnfocat));
    }

    /// <summary>The focused slot reuses the hover highlight, so there is one visual
    /// language for "this is where the appointment would go".</summary>
    private void PintarFocus()
    {
        for (int i = 0; i < Dies.Count; i++)
        {
            if (i == _diaEnfocat) Dies[i].SobrevolarAMinut(_minutEnfocat);
            else Dies[i].DeixarDeSobrevolar();
        }
    }

    public async Task CarregarSetmana(DateOnly dilluns)
    {
        Carregant = true;
        try
        {
            InicSetmana = dilluns;
            var diumenge = dilluns.AddDays(6);
            RangText = FormatarRang(dilluns, diumenge);

            // Three round trips for the whole week, never one per day.
            var totes = await _cites.ObtenirPerRang(dilluns, diumenge);
            var tancats = await _disponibilitat.DiesTancatsA(dilluns, diumenge);
            var franjesPerDia = await _disponibilitat.FranjesSetmanals();
            MinutsSlot = GraellaHelper.MinutsSlotValid(
                await _configuracio.ObtenirInt(ClausConfig.MinutsSlotAgenda, GraellaHelper.MinutsSlotPerDefecte));

            var frangesDeLaSetmana = new List<(TimeOnly inici, TimeOnly fi)>();
            for (int i = 0; i < 7; i++)
            {
                var dia = SetmanaHelper.ADiaSetmana(dilluns.AddDays(i));
                if (franjesPerDia.TryGetValue(dia, out var franges)) frangesDeLaSetmana.AddRange(franges);
            }

            var visibles = totes.Select(c => (c.Hora, c.DuradaMin));
            if (_fantasma is { } f && _dataFantasma is not null)
                visibles = visibles.Append((GraellaHelper.AHora(FantasmaMinutInici), FantasmaDurada));

            (MinutIniciGraella, MinutFiGraella) = GraellaHelper.RangVisible(frangesDeLaSetmana, visibles.ToList());
            _primerMinutObert = frangesDeLaSetmana.Count == 0
                ? MinutIniciGraella
                : frangesDeLaSetmana.Min(f => GraellaHelper.MinutsDelDia(f.inici));
            _obertures = franjesPerDia.ToDictionary(
                p => p.Key, p => p.Value.Min(f => GraellaHelper.MinutsDelDia(f.inici)));

            RecalcularMetriques();
            OmplirRegla();

            for (int i = 0; i < 7; i++)
            {
                var data = dilluns.AddDays(i);
                var dia = Dies[i];
                dia.Data = data;
                dia.EsAvui = data == DateOnly.FromDateTime(DateTime.Today);
                dia.Tancat = tancats.ContainsKey(data);
                dia.MotiuTancat = tancats.GetValueOrDefault(data);

                var frangesDelDia = dia.Tancat
                    ? []
                    : franjesPerDia.GetValueOrDefault(SetmanaHelper.ADiaSetmana(data), []);

                OmplirBandes(dia, frangesDelDia, franjesPerDia.Count > 0);
                OmplirCites(dia, totes.Where(c => c.Data == data).ToList());
            }

            AplicarFantasma();
            RefrescarAra();
        }
        finally { Carregant = false; }
    }

    /// <summary>
    /// Ghost block for the dialog: shows where the appointment being edited would land,
    /// and how it collides with its neighbours, before the warnings even fire.
    /// </summary>
    public void MostrarFantasma(DateOnly data, TimeOnly hora, int duradaMin)
    {
        FantasmaMinutInici = GraellaHelper.MinutsDelDia(hora);
        FantasmaDurada = Math.Max(1, duradaMin);
        _dataFantasma = data;

        _fantasma ??= new CitaGraellaViewModel
        {
            EsFantasma = true,
            Titol = "Aquesta cita",
            HoraText = string.Empty
        };

        AplicarFantasma();
    }

    private int FantasmaMinutInici { get; set; }
    private int FantasmaDurada { get; set; } = 30;

    private void AplicarFantasma()
    {
        if (_fantasma is not { } fantasma || _dataFantasma is not { } data) return;

        foreach (var dia in Dies) dia.Cites.Remove(fantasma);

        var columna = Dies.FirstOrDefault(d => d.Data == data);
        if (columna is null) return;

        fantasma.Top = GraellaHelper.Top(FantasmaMinutInici, MinutIniciGraella, PixelsPerMinut);
        fantasma.Alcada = GraellaHelper.Alcada(FantasmaDurada, PixelsPerMinut, AlcadaMinimaCitaPx);
        fantasma.CarrilIndex = 0;
        fantasma.NombreCarrils = 1;
        columna.Cites.Add(fantasma);
    }

    /// <summary>Moves the red "now" marker. Called once a minute by the view.</summary>
    public void RefrescarAra()
    {
        var avui = DateOnly.FromDateTime(DateTime.Today);
        int ara = GraellaHelper.MinutsDelDia(TimeOnly.FromDateTime(DateTime.Now));

        AraVisible = Dies.Any(d => d.Data == avui) && ara >= MinutIniciGraella && ara <= MinutFiGraella;
        AraTop = GraellaHelper.Top(ara, MinutIniciGraella, PixelsPerMinut);
    }

    private static string FormatarRang(DateOnly dilluns, DateOnly diumenge)
    {
        var cultura = new System.Globalization.CultureInfo("ca-ES");
        return dilluns.Month == diumenge.Month
            ? $"{dilluns.Day} – {diumenge.Day} de {diumenge.ToString("MMMM yyyy", cultura)}"
            : $"{dilluns.ToString("d MMM", cultura)} – {diumenge.ToString("d MMM yyyy", cultura)}";
    }

    private void RecalcularMetriques()
    {
        PixelsPerMinut = GraellaHelper.PixelsPerMinut(AlcadaSlotPx, MinutsSlot);
        AlcadaHoraPx = PixelsPerMinut * 60;
        AlcadaTotalPx = (MinutFiGraella - MinutIniciGraella) * PixelsPerMinut;
        DesplacamentInicialPx = GraellaHelper.Top(_primerMinutObert, MinutIniciGraella, PixelsPerMinut);
    }

    private void OmplirRegla()
    {
        Hores.Clear();
        foreach (int minut in GraellaHelper.HoresDeLaRegla(MinutIniciGraella, MinutFiGraella, AlcadaHoraPx))
            Hores.Add(new HoraReglaViewModel
            {
                Top = GraellaHelper.Top(minut, MinutIniciGraella, PixelsPerMinut),
                Etiqueta = GraellaHelper.AHora(minut).ToString("HH\\:mm")
            });
    }

    private void OmplirBandes(DiaGraellaViewModel dia,
        IReadOnlyList<(TimeOnly inici, TimeOnly fi)> franges, bool hiHaHorarisConfigurats)
    {
        dia.Bandes.Clear();

        // With no opening hours configured at all, shading every column head to toe as
        // "outside opening hours" is technically true and completely useless. Leave the
        // week clear until the barbershop has a schedule.
        if (!hiHaHorarisConfigurats && !dia.Tancat) return;

        var tipus = dia.Tancat ? TipusBanda.DiaTancat : TipusBanda.ForaHorari;

        foreach (var (inici, fi) in GraellaHelper.BandesForaHorari(franges, MinutIniciGraella, MinutFiGraella))
            dia.Bandes.Add(new BandaGraellaViewModel
            {
                Top = GraellaHelper.Top(inici, MinutIniciGraella, PixelsPerMinut),
                Alcada = (fi - inici) * PixelsPerMinut,
                Tipus = tipus
            });
    }

    private void OmplirCites(DiaGraellaViewModel dia, IReadOnlyList<Cita> cites)
    {
        dia.Cites.Clear();

        // Cancelled and no-show appointments free up their slot, so they must not take
        // a lane either — same rule DisponibilitatService.Comprovar already applies.
        var actives = cites
            .Where(c => c.Estat is not (EstatCita.Cancellada or EstatCita.NoAssistida))
            .OrderBy(c => c.Hora).ToList();

        var blocs = actives
            .Select(c => new BlocTemporal(GraellaHelper.MinutsDelDia(c.Hora),
                                          GraellaHelper.MinutsDelDia(c.Hora) + c.DuradaMin))
            .ToList();
        var carrils = GraellaHelper.RepartirCarrils(blocs);

        for (int i = 0; i < actives.Count; i++)
        {
            var bloc = CrearBloc(actives[i]);
            bloc.CarrilIndex = carrils[i].Index;
            bloc.NombreCarrils = Math.Max(1, carrils[i].Total);
            dia.Cites.Add(bloc);
        }

        // Cancelled ones ride along in lane 0, dimmed, so the history stays visible.
        foreach (var cita in cites.Where(c => c.Estat is EstatCita.Cancellada or EstatCita.NoAssistida))
            dia.Cites.Add(CrearBloc(cita));
    }

    private CitaGraellaViewModel CrearBloc(Cita cita)
    {
        var bloc = CitaGraellaViewModel.Des(cita,
            GraellaHelper.Top(cita.Hora, MinutIniciGraella, PixelsPerMinut),
            GraellaHelper.Alcada(cita.DuradaMin, PixelsPerMinut, AlcadaMinimaCitaPx));
        bloc.EsSeleccionada = CitaSeleccionadaId == cita.Id;
        return bloc;
    }

    partial void OnCitaSeleccionadaIdChanged(int? value)
    {
        foreach (var dia in Dies)
            foreach (var bloc in dia.Cites)
                bloc.EsSeleccionada = value is not null && bloc.Id == value;
    }
}
