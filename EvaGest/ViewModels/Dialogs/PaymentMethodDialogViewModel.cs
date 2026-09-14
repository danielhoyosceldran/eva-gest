using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EvaGest.Models;
using EvaGest.Resources;

namespace EvaGest.ViewModels.Dialogs;

public partial class PaymentMethodDialogViewModel : DialogViewModelBase
{
    private readonly int? _id;

    [ObservableProperty] private string _name = string.Empty;

    public override string Title => _id is null ? Texts.NewMethodTitle : Texts.EditMethodTitle;

    public PaymentMethodDialogViewModel() { }

    public PaymentMethodDialogViewModel(PaymentMethod method)
    {
        _id = method.Id;
        Name = method.Name;
    }

    [RelayCommand]
    private void Save()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            ErrorValidation = Texts.MethodNameRequired;
            return;
        }

        ErrorValidation = null;
        RequestClose(true);
    }

    [RelayCommand]
    private void Cancel() => RequestClose(false);

    public PaymentMethod AModel() => new() { Id = _id ?? 0, Name = Name.Trim(), Active = true };
}
