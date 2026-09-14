using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EvaGest.Models;
using EvaGest.Services;
using EvaGest.Resources;

namespace EvaGest.ViewModels.Dialogs;

public partial class ProductDialogViewModel : DialogViewModelBase
{
    private readonly int? _id;

    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string _priceText = "0,00";
    [ObservableProperty] private string _vatText = "21";
    [ObservableProperty] private string? _category;

    public override string Title => _id is null ? Texts.NewProductTitle : Texts.EditProductTitle;

    /// <summary>New entry: the VAT rate starts at whatever Configuració proposes,
    /// but stays editable per item (RF-07/RF-08).</summary>
    public ProductDialogViewModel(int defaultVatBp = 2100)
    {
        VatText = Percentages.FormatWithoutUnit(defaultVatBp);
    }

    public ProductDialogViewModel(Product product)
    {
        _id = product.Id;
        Name = product.Name;
        PriceText = Money.FormatExport(product.PriceCents);
        VatText = Percentages.FormatWithoutUnit(product.VatBp);
        Category = product.Category;
    }

    [RelayCommand]
    private void Save()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            ErrorValidation = Texts.ProductNameRequired;
            return;
        }
        if (!Money.TryParse(PriceText, out _))
        {
            ErrorValidation = Texts.PriceInvalid;
            return;
        }
        if (!Percentages.TryParse(VatText, out _))
        {
            ErrorValidation = Texts.VatOutOfRange;
            return;
        }

        ErrorValidation = null;
        RequestClose(true);
    }

    [RelayCommand]
    private void Cancel() => RequestClose(false);

    public Product AModel()
    {
        Money.TryParse(PriceText, out int priceCents);
        Percentages.TryParse(VatText, out int vatBp);

        return new Product
        {
            Id = _id ?? 0,
            Name = Name.Trim(),
            PriceCents = priceCents,
            Category = string.IsNullOrWhiteSpace(Category) ? null : Category.Trim(),
            VatBp = vatBp,
            Active = true
        };
    }
}
