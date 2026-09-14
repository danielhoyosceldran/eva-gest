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

    /// <summary>Set when a duplicate is found on save, so the view can show the
    /// non-blocking warning with its two extra actions (pantalles 3.3).</summary>
    [ObservableProperty] private Client? _duplicateFound;

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

        // The warning is informational only: it never blocks saving (RF-03).
        if (DuplicateFound is null)
        {
            var possible = await _clients.FindPossibleDuplicate(Name, Mobile);
            if (possible is not null && possible.Id != _id)
            {
                DuplicateFound = possible;
                return;
            }
        }

        ErrorValidation = null;
        RequestClose(true);
    }

    [RelayCommand]
    private void SaveAnyway()
    {
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
