using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EvaGest.Models;
using EvaGest.Services;
using EvaGest.Resources;

namespace EvaGest.ViewModels.Dialogs;

public partial class ClientDialogViewModel : DialogViewModelBase
{
    private readonly IClientService _clients;
    private readonly IDialogService _dialogs;
    private readonly int? _id;

    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string _mobile = string.Empty;
    [ObservableProperty] private string? _email;
    [ObservableProperty] private DateOnly? _birthDate;
    /// <summary>The birthday box reads something that is not a date (31/02); <see cref="BirthDate"/>
    /// keeps the last real one, so Save refuses instead of storing it.</summary>
    [ObservableProperty] private bool _isDateInvalid;
    [ObservableProperty] private string? _notes;

    public override string Title => _id is null ? Texts.NewClientTitle : Texts.EditClientTitle;

    /// <summary>A new client.</summary>
    /// <param name="name">What was already typed in the box the dialog was opened from
    /// (the sale's or the appointment's client box). It counts as the starting point, so
    /// closing the dialog without typing anything else does not ask.</param>
    public ClientDialogViewModel(IClientService clients, IDialogService dialogs, string? name = null)
    {
        _clients = clients;
        _dialogs = dialogs;
        Name = name ?? string.Empty;
        MarkOpened();
    }

    public ClientDialogViewModel(IClientService clients, IDialogService dialogs, Client client)
        : this(clients, dialogs)
    {
        _id = client.Id;
        Name = client.Name;
        Mobile = client.Mobile;
        Email = client.Email;
        BirthDate = client.BirthDate;
        Notes = client.Notes;
        MarkOpened();
    }

    [RelayCommand]
    private async Task Save()
    {
        if (string.IsNullOrWhiteSpace(Name) || string.IsNullOrWhiteSpace(Mobile))
        {
            ErrorValidation = Texts.NameAndMobileRequired;
            return;
        }
        if (IsDateInvalid)
        {
            ErrorValidation = Texts.DateInvalid;
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
        await CloseAfterSaving();
    }

    /// <summary>
    /// Asks before throwing away what was typed, like the sale and appointment dialogs:
    /// Esc used to close a half-filled client with no question (B-37). Esc and the X
    /// button reach the same question through the window.
    /// </summary>
    [RelayCommand]
    private async Task Cancel()
    {
        if (await CanDiscard()) RequestClose(false);
    }

    protected override IDialogService DiscardDialogs => _dialogs;

    /// <summary>Everything Save would write, as the boxes hold it.</summary>
    protected override string EditableState()
        => string.Join("|", Name, Mobile, Email, BirthDate, Notes);

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
