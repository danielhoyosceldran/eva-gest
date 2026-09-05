using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EvaGest.Models;
using EvaGest.Services;

namespace EvaGest.ViewModels.Dialegs;

/// <summary>
/// Moviment de caixa (entrada / sortida), pantalles 3.5. Base and quota are computed
/// once here and frozen: if the VAT breakdown were derived later instead of stored,
/// changing the global VAT setting would retroactively alter past movements.
/// </summary>
public partial class MovimentDialogViewModel : DialegViewModelBase
{
    private readonly int? _id;

    [ObservableProperty] private TipusMoviment _tipus;
    [ObservableProperty] private string _preuText = "0,00";
    [ObservableProperty] private MetodePagament? _metode;
    [ObservableProperty] private string _concepte = string.Empty;
    [ObservableProperty] private bool _desglossarIva;
    [ObservableProperty] private string _ivaText = "21";
    [ObservableProperty] private string? _observacions;

    public ObservableCollection<MetodePagament> MetodesActius { get; } = [];

    public override string Titol => Tipus == TipusMoviment.Entrada ? "Nova entrada" : "Nova sortida";

    public MovimentDialogViewModel(TipusMoviment tipus) => Tipus = tipus;

    public MovimentDialogViewModel(MovimentCaixa moviment)
    {
        _id = moviment.Id;
        Tipus = moviment.Tipus;
        PreuText = Diners.FormatExport(moviment.ImportCents);
        Concepte = moviment.Concepte;
        Observacions = moviment.Observacions;
        DesglossarIva = moviment.BaseCents is not null;
        if (moviment.IvaBp is int bp) IvaText = Percentatges.Format(bp).Replace(" %", "");
    }

    public async Task CarregarMetodes(ICatalegService cataleg)
    {
        foreach (var m in await cataleg.ObtenirMetodes(nomesActius: true)) MetodesActius.Add(m);
        Metode ??= MetodesActius.FirstOrDefault();
    }

    [RelayCommand]
    private void Guardar()
    {
        if (!Diners.TryParse(PreuText, out int importCents) || importCents <= 0)
        {
            ErrorValidacio = "L'import ha de ser més gran que zero.";
            return;
        }
        if (Metode is null)
        {
            ErrorValidacio = "Cal triar un mètode de pagament.";
            return;
        }
        if (string.IsNullOrWhiteSpace(Concepte))
        {
            ErrorValidacio = "Cal indicar el concepte.";
            return;
        }

        ErrorValidacio = null;
        SolicitarTancar(true);
    }

    [RelayCommand]
    private void Cancellar() => SolicitarTancar(false);

    public MovimentCaixa AModel()
    {
        Diners.TryParse(PreuText, out int importCents);
        int? baseCents = null, ivaCents = null, ivaBp = null;

        if (DesglossarIva)
        {
            ivaBp = (int)Math.Round(decimal.Parse(IvaText.Replace(",", ".").Replace("%", ""),
                System.Globalization.CultureInfo.InvariantCulture) * 100);
            var linia = new VendaLinia { ImportCents = importCents, IvaBp = ivaBp.Value };
            var d = IvaCalculator.Calcular([linia], IvaMode.Inclos);
            baseCents = d.BaseCents;
            ivaCents = d.IvaCents;
        }

        return new MovimentCaixa
        {
            Id = _id ?? 0,
            Data = DateOnly.FromDateTime(DateTime.Today),
            Tipus = Tipus,
            ImportCents = importCents,
            BaseCents = baseCents,
            IvaCents = ivaCents,
            IvaBp = ivaBp,
            MetodePagamentId = Metode!.Id,
            Concepte = Concepte.Trim(),
            Observacions = string.IsNullOrWhiteSpace(Observacions) ? null : Observacions.Trim()
        };
    }
}
