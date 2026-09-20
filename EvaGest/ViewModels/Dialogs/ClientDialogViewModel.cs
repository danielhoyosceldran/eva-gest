using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EvaGest.Models;
using EvaGest.Services;
using EvaGest.Resources;

namespace EvaGest.ViewModels.Dialogs;

public partial class ClientDialogViewModel : DialogViewModelBase
{
    private readonly IClientService _clients;
    private readonly int? _id;

    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string _mobile = string.Empty;
    [ObservableProperty] private string? _email;
    [ObservableProperty] private DateOnly? _birthDate;
    [ObservableProperty] private string? _notes;

    public override string Title => _id is null ? Texts.NewClientTitle : Texts.EditClientTitle;

    public ClientDialogViewModel(IClientService clients)
    {
        _clients = clients;
    }

    public ClientDialogViewModel(IClientService clients, Client client) : this(clients)
    {
        _id = client.Id;
        Name = client.Name;
        Mobile = client.Mobile;
        Email = client.Email;
        BirthDate = client.BirthDate;
        Notes = client.Notes;
    }

    [RelayCommand]
    private async Task Save()
    {
        if (string.IsNullOrWhiteSpace(Name) || string.IsNullOrWhiteSpace(Mobile))
        {
            ErrorValidation = Texts.NameAndMobileRequired;
            return;
        }
        if (!ContactValidator.IsValidPhone(Mobile))
        {
            ErrorValidation = Texts.PhoneInvalid;
            return;
        }
        if (!string.IsNullOrWhiteSpace(Email) && !ContactValidator.IsValidEmail(Email))
        {
            ErrorValidation = Texts.EmailInvalid;
            return;
        }

        // A name identifies a client, so this refuses the save rather than warning about
        // it (decision 6.1). It used to be a non-blocking notice with a "save it anyway"
        // button, but the unique index on ClientKey would then reject the insert with an
        // unhandled DbUpdateException: the override could never actually override
        // anything. Checked here, where the message can say what to do about it.
        //
        // The Id guard is what lets a client be edited without colliding with itself.
        var existing = await _clients.FindByName(Name);
        if (existing is not null && existing.Id != _id)
        {
            ErrorValidation = string.Format(Texts.ClientNameAlreadyExists,
                                            existing.Name, existing.Mobile);
            return;
        }

        ErrorValidation = null;
        RequestClose(true);
    }

    [RelayCommand]
    private void Cancel() => RequestClose(false);

    public Client AModel() => new()
    {
        Id = _id ?? 0,
        Name = Name.Trim(),
        Mobile = Mobile.Trim(),
        Email = string.IsNullOrWhiteSpace(Email) ? null : Email.Trim(),
        BirthDate = BirthDate,
        Notes = string.IsNullOrWhiteSpace(Notes) ? null : Notes.Trim()
    };
}
