using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EvaGest.Models;
using EvaGest.Services;
using EvaGest.Resources;

namespace EvaGest.ViewModels.Dialogs;

/// <summary>
/// Till movement (cash in / cash out), pantalles 3.5. Base and quota are computed
/// once here and frozen: if the VAT breakdown were derived later instead of stored,
/// changing the global VAT setting would retroactively alter past movements.
/// </summary>
public partial class MovementDialogViewModel : DialogViewModelBase
{
    private readonly int? _id;

    [ObservableProperty] private MovementType _type;
    [ObservableProperty] private string _priceText = "0,00";
    [ObservableProperty] private PaymentMethod? _method;
    [ObservableProperty] private string _concept = string.Empty;
    [ObservableProperty] private bool _splitVat;
    [ObservableProperty] private string _vatText = "21";
    [ObservableProperty] private string? _notes;

    public ObservableCollection<PaymentMethod> ActiveMethods { get; } = [];

    public override string Title => Type == MovementType.In ? Texts.NewCashInTitle : Texts.NewCashOutTitle;

    public MovementDialogViewModel(MovementType type) => Type = type;

    public MovementDialogViewModel(CashMovement movement)
    {
        _id = movement.Id;
        Type = movement.Type;
        PriceText = Money.FormatExport(movement.AmountCents);
        Concept = movement.Concept;
        Notes = movement.Notes;
        SplitVat = movement.BaseCents is not null;
        if (movement.VatBp is int bp) VatText = Percentages.Format(bp).Replace(" %", "");
    }

    public async Task LoadMethods(ICatalogService catalog)
    {
        foreach (var m in await catalog.GetMethods(onlyActive: true)) ActiveMethods.Add(m);
        Method ??= ActiveMethods.FirstOrDefault();
    }

    [RelayCommand]
    private void Save()
    {
        if (!Money.TryParse(PriceText, out int amountCents) || amountCents <= 0)
        {
            ErrorValidation = Texts.AmountMustBePositive;
            return;
        }
        if (Method is null)
        {
            ErrorValidation = Texts.PaymentMethodRequired;
            return;
        }
        if (string.IsNullOrWhiteSpace(Concept))
        {
            ErrorValidation = Texts.ConceptRequired;
            return;
        }
        // Only when the rate is actually going to be used. Unchecked, AModel used to
        // parse this box unguarded and threw on anything that was not a number.
        if (SplitVat && !Percentages.TryParse(VatText, out _))
        {
            ErrorValidation = Texts.VatOutOfRange;
            return;
        }

        ErrorValidation = null;
        RequestClose(true);
    }

    [RelayCommand]
    private void Cancel() => RequestClose(false);

    public CashMovement AModel()
    {
        Money.TryParse(PriceText, out int amountCents);
        int? baseCents = null, vatCents = null, vatBp = null;

        if (SplitVat && Percentages.TryParse(VatText, out int parsedBp))
        {
            vatBp = parsedBp;
            var line = new SaleLine { AmountCents = amountCents, VatBp = vatBp.Value };
            var d = VatCalculator.Compute([line], VatMode.Included);
            baseCents = d.BaseCents;
            vatCents = d.VatCents;
        }

        return new CashMovement
        {
            Id = _id ?? 0,
            Date = DateOnly.FromDateTime(DateTime.Today),
            Type = Type,
            AmountCents = amountCents,
            BaseCents = baseCents,
            VatCents = vatCents,
            VatBp = vatBp,
            PaymentMethodId = Method!.Id,
            Concept = Concept.Trim(),
            Notes = string.IsNullOrWhiteSpace(Notes) ? null : Notes.Trim()
        };
    }
}
