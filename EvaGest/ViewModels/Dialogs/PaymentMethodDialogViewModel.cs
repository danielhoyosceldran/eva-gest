using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EvaGest.Models;
using EvaGest.Resources;
using EvaGest.Services;

namespace EvaGest.ViewModels.Dialogs;

public partial class PaymentMethodDialogViewModel : DialogViewModelBase
{
    private readonly ICatalogService _catalog;
    private readonly int? _id;

    [ObservableProperty] private string _name = string.Empty;

    public override string Title => _id is null ? Texts.NewMethodTitle : Texts.EditMethodTitle;

    public PaymentMethodDialogViewModel(ICatalogService catalog) => _catalog = catalog;

    public PaymentMethodDialogViewModel(ICatalogService catalog, PaymentMethod method)
    {
        _catalog = catalog;
        _id = method.Id;
        Name = method.Name;
    }

    [RelayCommand]
    private async Task Save()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            ErrorValidation = Texts.MethodNameRequired;
            return;
        }

        // The name is unique in the database, deactivated methods included, and the
        // page saves only once this dialog has closed. A taken name used to get that far
        // and fail there with the generic error, the typed name already gone (E-06), so
        // it is refused here, where the message can say what to do. Compared ignoring
        // case: "bizum" next to "Bizum" would only be two entries for one thing.
        string name = Name.Trim();
        var taken = (await _catalog.GetMethods()).FirstOrDefault(m =>
            m.Id != _id && string.Equals(m.Name.Trim(), name, StringComparison.CurrentCultureIgnoreCase));
        if (taken is not null)
        {
            ErrorValidation = string.Format(Texts.MethodNameAlreadyExists, taken.Name);
            return;
        }

        ErrorValidation = null;
        RequestClose(true);
    }

    [RelayCommand]
    private void Cancel() => RequestClose(false);

    public PaymentMethod AModel() => new() { Id = _id ?? 0, Name = Name.Trim(), Active = true };
}
