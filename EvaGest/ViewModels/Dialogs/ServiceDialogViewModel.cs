using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EvaGest.Models;
using EvaGest.Services;
using EvaGest.Resources;

namespace EvaGest.ViewModels.Dialogs;

public partial class ServiceDialogViewModel : DialogViewModelBase
{
    private readonly int? _id;

    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string _priceText = "0,00";
    [ObservableProperty] private string _vatText = "21";
    [ObservableProperty] private string? _durationMinText;

    public override string Title => _id is null ? Texts.NewServiceTitle : Texts.EditServiceTitle;

    /// <summary>New entry: the VAT rate starts at whatever Configuració proposes,
    /// but stays editable per item (RF-07/RF-08).</summary>
    public ServiceDialogViewModel(int defaultVatBp = 2100)
    {
        VatText = Percentages.FormatWithoutUnit(defaultVatBp);
    }

    public ServiceDialogViewModel(Service service)
    {
        _id = service.Id;
        Name = service.Name;
        PriceText = Money.FormatExport(service.PriceCents);
        VatText = Percentages.FormatWithoutUnit(service.VatBp);
        DurationMinText = service.DurationMin?.ToString();
    }

    [RelayCommand]
    private void Save()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            ErrorValidation = Texts.ServiceNameRequired;
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
        // Optional, but once typed it has to be a real number of minutes: int.TryParse
        // in AModel used to turn "trenta" into "no duration at all" without a word.
        if (!string.IsNullOrWhiteSpace(DurationMinText)
            && !NumberValidator.TryParseAtLeast(DurationMinText, 1, out _))
        {
            ErrorValidation = Texts.DurationInvalid;
            return;
        }

        ErrorValidation = null;
        RequestClose(true);
    }

    [RelayCommand]
    private void Cancel() => RequestClose(false);

    /// <summary>Reads the edited fields back into a model, ready to save.</summary>
    public Service AModel()
    {
        Money.TryParse(PriceText, out int priceCents);
        Percentages.TryParse(VatText, out int vatBp);
        int? durationMin = NumberValidator.TryParseAtLeast(DurationMinText, 1, out int d) ? d : null;

        return new Service
        {
            Id = _id ?? 0,
            Name = Name.Trim(),
            PriceCents = priceCents,
            VatBp = vatBp,
            DurationMin = durationMin,
            Active = true
        };
    }
}
