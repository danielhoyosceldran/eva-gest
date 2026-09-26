using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EvaGest.Resources;
using EvaGest.Services;

namespace EvaGest.ViewModels.Dialogs;

/// <summary>
/// Opens owner mode with the PIN. "He oblidat el PIN" switches the same dialog to the
/// recovery code; a correct code closes it with <see cref="Recovered"/> set, and the
/// shell then asks for a new PIN.
/// </summary>
public partial class OwnerUnlockDialogViewModel(IOwnerAccessService owner) : DialogViewModelBase
{
    /// <summary>Pushed in from the view's PasswordBox, which cannot be bound.</summary>
    [ObservableProperty] private string _pin = string.Empty;

    [ObservableProperty] private string _recoveryCode = string.Empty;

    /// <summary>True while the dialog asks for the recovery code instead of the PIN.</summary>
    [ObservableProperty] private bool _isRecovering;

    /// <summary>True when the dialog closed because the recovery code was accepted:
    /// owner mode is still closed and a new PIN has to be created.</summary>
    public bool Recovered { get; private set; }

    public override string Title => Texts.OwnerUnlockTitle;

    [RelayCommand]
    private async Task Unlock()
    {
        // An empty or malformed entry (Enter pressed too early) cannot be the PIN; it is
        // answered here so it does not use up one of the attempts before the lockout.
        if (!OwnerPin.IsValid(Pin))
        {
            ErrorValidation = Texts.WrongPin;
            return;
        }

        var result = await owner.Unlock(Pin);
        if (result == AccessResult.Accepted)
        {
            ErrorValidation = null;
            RequestClose(true);
            return;
        }
        ErrorValidation = Message(result, Texts.WrongPin);
    }

    [RelayCommand]
    private void ForgotPin()
    {
        ErrorValidation = null;
        IsRecovering = true;
    }

    [RelayCommand]
    private void BackToPin()
    {
        ErrorValidation = null;
        IsRecovering = false;
    }

    [RelayCommand]
    private async Task CheckRecoveryCode()
    {
        var result = await owner.CheckRecoveryCode(RecoveryCode);
        if (result == AccessResult.Accepted)
        {
            ErrorValidation = null;
            Recovered = true;
            RequestClose(true);
            return;
        }
        ErrorValidation = Message(result, Texts.WrongRecoveryCode);
    }

    [RelayCommand]
    private void Cancel() => RequestClose(false);

    /// <summary>The lockout message carries the seconds left, rounded up so it never
    /// says "0 segons" while the lockout is still running.</summary>
    private string Message(AccessResult result, string wrong) => result == AccessResult.LockedOut
        ? string.Format(Texts.TooManyAttempts, (int)Math.Ceiling(owner.LockoutRemaining.TotalSeconds))
        : wrong;
}
