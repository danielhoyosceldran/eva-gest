using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EvaGest.Resources;
using EvaGest.Services;

namespace EvaGest.ViewModels.Dialogs;

/// <summary>
/// The question before a delete or a void, answered with the owner's PIN (F-05). The
/// owner asked for it on every one of them, owner mode open or not: a click is too easy
/// to make by mistake, and with owner mode open anyone at the counter could make it.
/// Checking the PIN here leaves owner mode exactly as it was.
/// </summary>
public partial class OwnerPinConfirmDialogViewModel(
    IOwnerAccessService owner, string title, string message, string confirmText) : DialogViewModelBase
{
    /// <summary>Pushed in from the view's PasswordBox, which cannot be bound.</summary>
    [ObservableProperty] private string _pin = string.Empty;

    public override string Title => title;

    /// <summary>What is about to be deleted or voided, as the plain question used to say it.</summary>
    public string Message => message;

    /// <summary>The concrete verb on the button: Eliminar, Anul·lar…</summary>
    public string ConfirmText => confirmText;

    [RelayCommand]
    private async Task Confirm()
    {
        // An empty or malformed entry (Enter pressed too early) cannot be the PIN; it is
        // answered here so it does not use up one of the attempts before the lockout.
        if (!OwnerPin.IsValid(Pin))
        {
            ErrorValidation = Texts.WrongPin;
            return;
        }

        var result = await owner.VerifyPin(Pin);
        if (result == AccessResult.Accepted)
        {
            ErrorValidation = null;
            RequestClose(true);
            return;
        }

        // Same wording as the unlock dialog: the lockout says how long is left, rounded
        // up so it never reads "0 segons" while it is still running.
        ErrorValidation = result == AccessResult.LockedOut
            ? string.Format(Texts.TooManyAttempts, (int)Math.Ceiling(owner.LockoutRemaining.TotalSeconds))
            : Texts.WrongPin;
    }

    [RelayCommand]
    private void Cancel() => RequestClose(false);
}
