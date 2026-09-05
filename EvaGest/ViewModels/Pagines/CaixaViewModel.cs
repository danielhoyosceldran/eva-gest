using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EvaGest.Models;
using EvaGest.Services;

namespace EvaGest.ViewModels.Pagines;

public enum PeriodeCaixa { Avui, Ahir, AquestaSetmana, AquestMes, MesAnterior, Personalitzat }

public partial class CaixaViewModel(ICaixaService caixa, ICatalegService cataleg, IDialogService dialegs)
    : PaginaViewModelBase
{
    public override string Titol => "Caixa";

    [ObservableProperty] private PeriodeCaixa _periode = PeriodeCaixa.Avui;
    [ObservableProperty] private DateOnly _des = DateOnly.FromDateTime(DateTime.Today);
    [ObservableProperty] private DateOnly _fins = DateOnly.FromDateTime(DateTime.Today);
    [ObservableProperty] private ResumCaixa? _resum;

    public ObservableCollection<MovimentCaixa> Moviments { get; } = [];

    public string VendesText => Diners.Format((int)(Resum?.VendesCents ?? 0));
    public string EntradesText => Diners.Format((int)(Resum?.EntradesCents ?? 0));
    public string SortidesText => Diners.Format((int)(Resum?.SortidesCents ?? 0));
    public string BalancText => Diners.Format((int)(Resum?.BalancCents ?? 0));
    public string BaseText => Diners.Format((int)(Resum?.BaseCents ?? 0));
    public string IvaText => Diners.Format((int)(Resum?.IvaCents ?? 0));

    /// <summary>Only shown per rate when the period actually mixes VAT rates
    /// (pantalles 2.7): with a single rate the top-line breakdown already says it all.</summary>
    public bool MostrarDesglossamentPerTipus => (Resum?.DesglossamentIva.Count ?? 0) > 1;

    partial void OnPeriodeChanged(PeriodeCaixa value)
    {
        var avui = DateOnly.FromDateTime(DateTime.Today);
        (Des, Fins) = value switch
        {
            PeriodeCaixa.Avui => (avui, avui),
            PeriodeCaixa.Ahir => (avui.AddDays(-1), avui.AddDays(-1)),
            PeriodeCaixa.AquestaSetmana => (Helpers.SetmanaHelper.DilluIrsDeLaSetmana(avui), avui),
            PeriodeCaixa.AquestMes => (new DateOnly(avui.Year, avui.Month, 1), avui),
            PeriodeCaixa.MesAnterior => PrimerIUltimDelMesAnterior(avui),
            _ => (Des, Fins)
        };
        _ = Carregar();
    }

    partial void OnDesChanged(DateOnly value) { if (Periode == PeriodeCaixa.Personalitzat) _ = Carregar(); }
    partial void OnFinsChanged(DateOnly value) { if (Periode == PeriodeCaixa.Personalitzat) _ = Carregar(); }

    private static (DateOnly, DateOnly) PrimerIUltimDelMesAnterior(DateOnly avui)
    {
        var primerActual = new DateOnly(avui.Year, avui.Month, 1);
        var ultimAnterior = primerActual.AddDays(-1);
        return (new DateOnly(ultimAnterior.Year, ultimAnterior.Month, 1), ultimAnterior);
    }

    public async Task Carregar()
    {
        Carregant = true;
        try
        {
            Resum = await caixa.Resum(Des, Fins);
            Moviments.Clear();
            foreach (var m in await caixa.ObtenirPerPeriode(Des, Fins)) Moviments.Add(m);

            foreach (var text in new[]
                     { nameof(VendesText), nameof(EntradesText), nameof(SortidesText),
                       nameof(BalancText), nameof(BaseText), nameof(IvaText), nameof(MostrarDesglossamentPerTipus) })
                OnPropertyChanged(text);
        }
        finally { Carregant = false; }
    }

    [RelayCommand]
    private async Task NovaEntrada() => await ObrirDialegMoviment(TipusMoviment.Entrada);

    [RelayCommand]
    private async Task NovaSortida() => await ObrirDialegMoviment(TipusMoviment.Sortida);

    private async Task ObrirDialegMoviment(TipusMoviment tipus)
    {
        var vm = new ViewModels.Dialegs.MovimentDialogViewModel(tipus);
        await vm.CarregarMetodes(cataleg);
        if (await dialegs.MostrarDialeg(vm))
        {
            await caixa.Crear(vm.AModel());
            await Carregar();
        }
    }

    [RelayCommand]
    private async Task Eliminar(MovimentCaixa moviment)
    {
        bool confirmat = await dialegs.Confirmar("Eliminar moviment?",
            $"S'eliminarà el moviment de {Diners.Format(moviment.ImportCents)}.", "Eliminar");
        if (!confirmat) return;

        await caixa.Eliminar(moviment.Id);
        await Carregar();
    }
}
