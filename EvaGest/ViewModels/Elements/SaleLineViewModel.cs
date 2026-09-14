using CommunityToolkit.Mvvm.ComponentModel;
using EvaGest.Models;
using EvaGest.Services;

namespace EvaGest.ViewModels.Elements;

/// <summary>Editable row inside the sale dialog. Its job is to allow modifying any
/// line, whether it came from the catalogue or not (pantalles 3.2).</summary>
public partial class SaleLineViewModel : ObservableObject
{
    // Catalogue origin, kept only for reporting. Never the source of price or VAT.
    public int? ServiceId { get; init; }
    public int? ProductId { get; init; }

    [ObservableProperty] private string _description = string.Empty;
    [ObservableProperty] private int _quantity = 1;
    [ObservableProperty] private string _priceText = "0,00";
    [ObservableProperty] private int _vatBp;

    /// <summary>Raised so the parent dialog can recompute the sale totals.</summary>
    public event Action? Changed;

    public int AmountCents => (Money.TryParse(PriceText, out int priceCents) ? priceCents : 0) * Quantity;

    partial void OnDescriptionChanged(string value) => Notify();
    partial void OnQuantityChanged(int value) => Notify();
    partial void OnPriceTextChanged(string value) => Notify();
    partial void OnVatBpChanged(int value) => Notify();

    private void Notify()
    {
        OnPropertyChanged(nameof(AmountCents));
        Changed?.Invoke();
    }

    public SaleLine AModel() => new()
    {
        ServiceId = ServiceId,
        ProductId = ProductId,
        Description = Description,
        Quantity = Quantity,
        UnitPriceCents = Money.TryParse(PriceText, out int p) ? p : 0,
        VatBp = VatBp,
        AmountCents = AmountCents
    };

    public static SaleLineViewModel FromService(Service service) => new()
    {
        ServiceId = service.Id,
        Description = service.Name,
        PriceText = Money.FormatExport(service.PriceCents),
        VatBp = service.VatBp
    };

    public static SaleLineViewModel FromProduct(Product product) => new()
    {
        ProductId = product.Id,
        Description = product.Name,
        PriceText = Money.FormatExport(product.PriceCents),
        VatBp = product.VatBp
    };

    public static SaleLineViewModel Free() => new() { Description = string.Empty };
}
