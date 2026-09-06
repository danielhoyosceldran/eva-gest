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
/// Configuració (pantalles 2.9). Everything the user can change lives in the database
/// (RF-23), so this page is the only place those keys are written from.
///
/// Flags and free-text fields save as they are edited; the opening hours and the VAT
/// mode do not, because both need validating or confirming first.
/// </summary>
public partial class ConfiguracioViewModel(
    IBackupService backup, IExportService export, IConfiguracioService configuracio,
    IDisponibilitatService disponibilitat, IDialogService dialegs)
    : PaginaViewModelBase
{
    public override string Titol => "Configuració";

    /// <summary>Guards the initial assignments during Carregar from writing straight back.</summary>
    private bool _carregat;

    // ── Dades de la barberia ────────────────────────────────────────────────
    [ObservableProperty] private string _barberiaNom = string.Empty;
    [ObservableProperty] private string _barberiaAdreca = string.Empty;
    [ObservableProperty] private string _barberiaTelefon = string.Empty;
    [ObservableProperty] private string? _confirmacioBarberia;

    // ── IVA ─────────────────────────────────────────────────────────────────
    [ObservableProperty] private string _ivaDefecteText = "21";
    [ObservableProperty] private string? _errorIva;
    [ObservableProperty] private string? _confirmacioIva;
    [ObservableProperty] private bool _aplicarIvaCaixa;

    public IReadOnlyList<IvaMode> ModesIva { get; } = [IvaMode.Inclos, IvaMode.NoInclos];

    [ObservableProperty] private IvaMode _modeIva = IvaMode.Inclos;

    /// <summary>The value actually stored, so a declined change can be put back.</summary>
    private IvaMode _modeIvaDesat = IvaMode.Inclos;

    public string ModeIvaExplicacio => ModeIva == IvaMode.Inclos
        ? "Els preus del catàleg ja porten l'IVA inclòs. Un servei de 15,00 € es cobra a 15,00 €."
        : "Els preus del catàleg són sense IVA. Un servei de 15,00 € es cobra a 18,15 € amb el 21 %.";

    // ── Agenda ──────────────────────────────────────────────────────────────
    /// <summary>Row granularity of the agenda grid. Only 15/30/60 are offered, because
    /// anything else would not divide the hour cleanly.</summary>
    public IReadOnlyList<int> OpcionsMinutsSlot { get; } = [15, 30, 60];

    [ObservableProperty] private int _minutsSlot = GraellaHelper.MinutsSlotPerDefecte;
    [ObservableProperty] private string _duradaDefecteCitaText = "30";
    [ObservableProperty] private string? _errorAgenda;

    // ── Horari d'obertura ───────────────────────────────────────────────────
    /// <summary>The seven rows of the opening-hours form, always in weekday order.</summary>
    public ObservableCollection<HorariDiaViewModel> DiesHorari { get; } =
        [.. Enum.GetValues<DiaSetmana>().Select(d => new HorariDiaViewModel { Dia = d })];

    [ObservableProperty] private string? _errorHorari;
    [ObservableProperty] private string? _confirmacioHorari;

    // ── Dies tancats ────────────────────────────────────────────────────────
    public ObservableCollection<DiaTancat> DiesTancats { get; } = [];

    [ObservableProperty] private DateOnly _novaDataTancada = DateOnly.FromDateTime(DateTime.Today);
    [ObservableProperty] private string _nouMotiuTancat = string.Empty;

    public bool NoHiHaDiesTancats => DiesTancats.Count == 0;

    // ── Còpies de seguretat ─────────────────────────────────────────────────
    [ObservableProperty] private string _ultimaCopiaText = "Encara no s'ha fet cap còpia.";
    [ObservableProperty] private string _horaBackupText = "20:00";
    [ObservableProperty] private string _backupsAConservarText = "15";
    [ObservableProperty] private string? _errorBackup;

    // ── Avisos i sons ───────────────────────────────────────────────────────
    [ObservableProperty] private bool _mostrarAvisConvidat = true;
    [ObservableProperty] private bool _soConfirmacio = true;

    public async Task Carregar()
    {
        Carregant = true;
        _carregat = false;
        try
        {
            BarberiaNom = await configuracio.Obtenir(ClausConfig.BarberiaNom) ?? string.Empty;
            BarberiaAdreca = await configuracio.Obtenir(ClausConfig.BarberiaAdreca) ?? string.Empty;
            BarberiaTelefon = await configuracio.Obtenir(ClausConfig.BarberiaTelefon) ?? string.Empty;

            IvaDefecteText = Percentatges.FormatSenseUnitat(
                await configuracio.ObtenirInt(ClausConfig.IvaBpDefecte, 2100));
            _modeIvaDesat = Enum.TryParse<IvaMode>(
                await configuracio.Obtenir(ClausConfig.IvaModeActual), out var mode) ? mode : IvaMode.Inclos;
            ModeIva = _modeIvaDesat;
            AplicarIvaCaixa = await configuracio.ObtenirBool(ClausConfig.AplicarIvaCaixa, false);

            MinutsSlot = GraellaHelper.MinutsSlotValid(
                await configuracio.ObtenirInt(ClausConfig.MinutsSlotAgenda, GraellaHelper.MinutsSlotPerDefecte));
            DuradaDefecteCitaText =
                (await configuracio.ObtenirInt(ClausConfig.DuradaDefecteCitaMin, 30)).ToString();

            HoraBackupText = await configuracio.Obtenir(ClausConfig.HoraBackup) ?? "20:00";
            BackupsAConservarText =
                (await configuracio.ObtenirInt(ClausConfig.BackupsAConservar, 15)).ToString();

            MostrarAvisConvidat = await configuracio.ObtenirBool(ClausConfig.MostrarAvisConvidat, true);
            SoConfirmacio = await configuracio.ObtenirBool(ClausConfig.SoConfirmacio, true);

            var copies = await backup.Llistar();
            var ultima = copies.OrderByDescending(c => c.Data).FirstOrDefault();
            UltimaCopiaText = ultima is null
                ? "Encara no s'ha fet cap còpia."
                : $"Última còpia: {ultima.Data:dd/MM/yyyy HH:mm} ({(ultima.EsAutomatica ? "automàtica" : "manual")})";

            var horari = await disponibilitat.FranjesSetmanals();
            foreach (var dia in DiesHorari) dia.Omplir(horari.GetValueOrDefault(dia.Dia, []));

            await CarregarDiesTancats();

            ErrorHorari = null;
            ConfirmacioHorari = null;
            ErrorIva = null;
            ConfirmacioIva = null;
            ErrorAgenda = null;
            ErrorBackup = null;
            ConfirmacioBarberia = null;
        }
        finally
        {
            _carregat = true;
            Carregant = false;
        }
    }

    // ── Dades de la barberia ────────────────────────────────────────────────

    [RelayCommand]
    private async Task GuardarDadesBarberia()
    {
        await configuracio.Guardar(ClausConfig.BarberiaNom, BarberiaNom.Trim());
        await configuracio.Guardar(ClausConfig.BarberiaAdreca, BarberiaAdreca.Trim());
        await configuracio.Guardar(ClausConfig.BarberiaTelefon, BarberiaTelefon.Trim());
        ConfirmacioBarberia = "Dades desades.";
    }

    // ── IVA ─────────────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task GuardarIvaDefecte()
    {
        if (!Percentatges.TryParse(IvaDefecteText, out int bp))
        {
            ErrorIva = "El percentatge d'IVA ha de ser un número entre 0 i 100.";
            return;
        }

        ErrorIva = null;
        await configuracio.Guardar(ClausConfig.IvaBpDefecte, bp.ToString());
        ConfirmacioIva = $"L'IVA per defecte dels serveis i productes nous serà del {Percentatges.Format(bp)}.";
    }

    /// <summary>
    /// Changing the mode reinterprets every catalogue price, so it is the one setting
    /// that asks before saving (CU-10). Declining puts the picker back rather than
    /// leaving it showing a value that was never stored.
    /// </summary>
    partial void OnModeIvaChanged(IvaMode value)
    {
        OnPropertyChanged(nameof(ModeIvaExplicacio));
        if (!_carregat || value == _modeIvaDesat) return;

        _ = ConfirmarCanviDeModeIva(value);
    }

    private async Task ConfirmarCanviDeModeIva(IvaMode nou)
    {
        string cap = nou == IvaMode.NoInclos
            ? "Un servei de 15,00 € passaria a cobrar-se a 18,15 € (15,00 € + IVA)."
            : "Un servei de 15,00 € passaria a cobrar-se a 15,00 €, amb l'IVA ja inclòs dins del preu.";

        bool confirmat = await dialegs.Confirmar(
            "Canviar el mode d'IVA?",
            "Aquest canvi modifica el significat de tots els preus del catàleg. " + cap
            + " Les vendes ja registrades no es veuran afectades.",
            "Ho entenc, canviar");

        if (!confirmat)
        {
            _carregat = false;
            ModeIva = _modeIvaDesat;
            _carregat = true;
            return;
        }

        _modeIvaDesat = nou;
        await configuracio.Guardar(ClausConfig.IvaModeActual, nou.ToString());
        ConfirmacioIva = "Mode d'IVA desat. Les vendes noves el faran servir; les ja registrades no canvien.";
    }

    partial void OnAplicarIvaCaixaChanged(bool value)
    {
        if (!_carregat) return;
        _ = configuracio.GuardarBool(ClausConfig.AplicarIvaCaixa, value);
    }

    // ── Agenda ──────────────────────────────────────────────────────────────

    partial void OnMinutsSlotChanged(int value)
    {
        if (!_carregat) return;
        _ = configuracio.Guardar(ClausConfig.MinutsSlotAgenda,
            GraellaHelper.MinutsSlotValid(value).ToString());
    }

    [RelayCommand]
    private async Task GuardarDuradaDefecte()
    {
        if (!int.TryParse(DuradaDefecteCitaText, out int minuts) || minuts <= 0)
        {
            ErrorAgenda = "La durada per defecte ha de ser un número de minuts més gran que zero.";
            return;
        }

        ErrorAgenda = null;
        await configuracio.Guardar(ClausConfig.DuradaDefecteCitaMin, minuts.ToString());
    }

    // ── Horari d'obertura ───────────────────────────────────────────────────

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

    // ── Dies tancats ────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task AfegirDiaTancat()
    {
        await disponibilitat.AfegirDiaTancat(NovaDataTancada, NouMotiuTancat);
        NouMotiuTancat = string.Empty;
        await CarregarDiesTancats();
    }

    [RelayCommand]
    private async Task TreureDiaTancat(DiaTancat dia)
    {
        await disponibilitat.EliminarDiaTancat(dia.Id);
        await CarregarDiesTancats();
    }

    private async Task CarregarDiesTancats()
    {
        DiesTancats.Clear();
        foreach (var d in await disponibilitat.DiesTancats()) DiesTancats.Add(d);
        OnPropertyChanged(nameof(NoHiHaDiesTancats));
    }

    // ── Còpies de seguretat ─────────────────────────────────────────────────

    [RelayCommand]
    private async Task GuardarOpcionsBackup()
    {
        if (!TimeOnly.TryParse(HoraBackupText, CultureInfo.InvariantCulture, out var hora))
        {
            ErrorBackup = "L'hora de la còpia ha de tenir la forma 20:00.";
            return;
        }
        if (!int.TryParse(BackupsAConservarText, out int quantes) || quantes < 1)
        {
            ErrorBackup = "Cal conservar com a mínim una còpia.";
            return;
        }

        ErrorBackup = null;
        await configuracio.Guardar(ClausConfig.HoraBackup, hora.ToString("HH\\:mm", CultureInfo.InvariantCulture));
        await configuracio.Guardar(ClausConfig.BackupsAConservar, quantes.ToString());

        // Lowering the number has to take effect now, not at the next backup, or the
        // extra files sit there until something else happens to trigger a cleanup.
        await backup.NetejarAntigues();
        await Carregar();
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
        if (!await dialegs.MostrarDialeg(vm)) return;

        await dialegs.Informar("Còpia restaurada",
            "S'han recuperat les dades de la còpia. Tanca i torna a obrir l'aplicació "
            + "perquè tot es llegeixi de nou.");
        await Carregar();
    }

    [RelayCommand]
    private async Task ExportarDades()
    {
        var vm = new ExportDialogViewModel(export, dialegs);
        await dialegs.MostrarDialeg(vm);
    }

    // ── Avisos i sons ───────────────────────────────────────────────────────

    partial void OnMostrarAvisConvidatChanged(bool value)
    {
        if (!_carregat) return;
        _ = configuracio.GuardarBool(ClausConfig.MostrarAvisConvidat, value);
    }

    partial void OnSoConfirmacioChanged(bool value)
    {
        if (!_carregat) return;
        _ = configuracio.GuardarBool(ClausConfig.SoConfirmacio, value);
    }
}
