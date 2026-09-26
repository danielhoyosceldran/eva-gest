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

    /// <summary>
    /// The day the money actually moved. It used to be stamped with DateTime.Today inside
    /// <see cref="AModel"/>, so a movement entered while the Till showed a past period was
    /// written outside that period and vanished from the table the user was looking at —
    /// the same mistake the sale dialog used to make (decision 6.4: a recorded movement
    /// belongs to the day it happened, not to the moment the dialog was saved).
    /// </summary>
    [ObservableProperty] private DateOnly _date;
    [ObservableProperty] private string _priceText = "0,00";
    [ObservableProperty] private PaymentMethod? _method;
    [ObservableProperty] private string _concept = string.Empty;
    [ObservableProperty] private bool _splitVat;

    /// <summary>Filled from ConfigKeys.DefaultVatBp by <see cref="LoadDefaults"/>. The
    /// literal is only what the box holds before the settings have been read: hardcoding
    /// 21 here meant a shop configured for any other rate froze the wrong quota onto
    /// every cash movement it split.</summary>
    [ObservableProperty] private string _vatText = "21";

    /// <summary>
    /// Whether the VAT-split checkbox is offered at all. pantalles 3.5: "Desglossar IVA
    /// | Casella. Només visible si `aplicar_iva_caixa` està activat". The setting was
    /// written by Configuració and read by nobody, so the box showed unconditionally and
    /// the toggle the user flipped did nothing.
    /// </summary>
    [ObservableProperty] private bool _showVatSplit;
    [ObservableProperty] private string? _notes;

    /// <summary>Only meaningful for a cash-out; left null for a cash-in. Both are
    /// optional, so nothing here needs validation in <see cref="Save"/>.</summary>
    [ObservableProperty] private ExpenseCategory? _category;
    [ObservableProperty] private Worker? _worker;

    public ObservableCollection<PaymentMethod> ActiveMethods { get; } = [];
    public ObservableCollection<ExpenseCategory> ActiveCategories { get; } = [];
    public ObservableCollection<Worker> ActiveWorkers { get; } = [];

    public override string Title => Type == MovementType.In ? Texts.NewCashInTitle : Texts.NewCashOutTitle;

    /// <summary>Category and worker only make sense for money going out (RF-16
    /// extension): a cash-in has no expense to classify or worker to pay.</summary>
    public bool IsCashOut => Type == MovementType.Out;

    public MovementDialogViewModel(MovementType type, DateOnly date)
    {
        Type = type;
        Date = date;
    }

    /// <summary>
    /// The dialog ready to use: the one place the two pages that open it agree on how it
    /// is set up. The Start page used to build it by hand and forgot
    /// <see cref="LoadDefaults"/>, so the same action behaved differently depending on
    /// which page it was started from.
    /// </summary>
    public static async Task<MovementDialogViewModel> New(
        MovementType type, DateOnly date,
        ICatalogService catalog, IWorkerService workers, ISettingsService settings)
    {
        var vm = new MovementDialogViewModel(type, date);
        await vm.LoadMethods(catalog);
        await vm.LoadCategories(catalog);
        await vm.LoadWorkers(workers);
        await vm.LoadDefaults(settings);
        return vm;
    }

    public MovementDialogViewModel(CashMovement movement)
    {
        _id = movement.Id;
        Type = movement.Type;
        Date = movement.Date;
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

    public async Task LoadCategories(ICatalogService catalog)
    {
        foreach (var c in await catalog.GetCategories(onlyActive: true)) ActiveCategories.Add(c);
    }

    public async Task LoadWorkers(IWorkerService workers)
    {
        foreach (var w in await workers.GetAll(onlyActive: true)) ActiveWorkers.Add(w);
    }

    /// <summary>
    /// Brings across the two settings this dialog is supposed to follow: whether the VAT
    /// split is offered at all, and which rate it starts on. An existing movement keeps
    /// the rate it was saved with — a default must never move a figure already recorded.
    /// </summary>
    public async Task LoadDefaults(ISettingsService settings)
    {
        ShowVatSplit = await settings.GetBool(ConfigKeys.ApplyVatToTill, false);

        if (_id is null)
            VatText = Percentages.FormatWithoutUnit(
                await settings.GetInt(ConfigKeys.DefaultVatBp, 2100));

        // SplitVat stays the single source of truth for whether the VAT is split; this
        // is the one place the setting can clear it. Guarding AModel and Save on
        // ShowVatSplit instead would make the split silently depend on this loader
        // having been called, which is a trap for every caller that builds the dialog
        // directly.
        if (!ShowVatSplit) SplitVat = false;
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
            Date = Date,
            Type = Type,
            AmountCents = amountCents,
            BaseCents = baseCents,
            VatCents = vatCents,
            VatBp = vatBp,
            PaymentMethodId = Method!.Id,
            CategoryId = Category?.Id,
            WorkerId = Worker?.Id,
            Concept = Concept.Trim(),
            Notes = string.IsNullOrWhiteSpace(Notes) ? null : Notes.Trim()
        };
    }
}
