using CommunityToolkit.Mvvm.ComponentModel;
using EvaGest.Models;
using EvaGest.Services;

namespace EvaGest.ViewModels.Elements;

/// <summary>Editable row inside the sale dialog. Its job is to allow modifying any
/// line, whether it came from the catalogue or not (pantalles 3.2).</summary>
public partial class VendaLiniaViewModel : ObservableObject
{
    // Catalogue origin, kept only for reporting. Never the source of price or VAT.
    public int? ServeiId { get; init; }
    public int? ProducteId { get; init; }

    [ObservableProperty] private string _descripcio = string.Empty;
    [ObservableProperty] private int _quantitat = 1;
    [ObservableProperty] private string _preuText = "0,00";
    [ObservableProperty] private int _ivaBp;

    /// <summary>Raised so the parent dialog can recompute the sale totals.</summary>
    public event Action? Canviada;

    public int ImportCents => (Diners.TryParse(PreuText, out int preuCents) ? preuCents : 0) * Quantitat;

    partial void OnDescripcioChanged(string value) => Notificar();
    partial void OnQuantitatChanged(int value) => Notificar();
    partial void OnPreuTextChanged(string value) => Notificar();
    partial void OnIvaBpChanged(int value) => Notificar();

    private void Notificar()
    {
        OnPropertyChanged(nameof(ImportCents));
        Canviada?.Invoke();
    }

    public VendaLinia AModel() => new()
    {
        ServeiId = ServeiId,
        ProducteId = ProducteId,
        Descripcio = Descripcio,
        Quantitat = Quantitat,
        PreuUnitariCents = Diners.TryParse(PreuText, out int p) ? p : 0,
        IvaBp = IvaBp,
        ImportCents = ImportCents
    };

    public static VendaLiniaViewModel DesDeServei(Servei servei) => new()
    {
        ServeiId = servei.Id,
        Descripcio = servei.Nom,
        PreuText = Diners.FormatExport(servei.PreuCents),
        IvaBp = servei.IvaBp
    };

    public static VendaLiniaViewModel DesDeProducte(Producte producte) => new()
    {
        ProducteId = producte.Id,
        Descripcio = producte.Nom,
        PreuText = Diners.FormatExport(producte.PreuCents),
        IvaBp = producte.IvaBp
    };

    public static VendaLiniaViewModel Lliure() => new() { Descripcio = string.Empty };
}
