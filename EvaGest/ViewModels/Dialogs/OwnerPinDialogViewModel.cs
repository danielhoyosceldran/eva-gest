using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EvaGest.Resources;
using EvaGest.Services;

namespace EvaGest.ViewModels.Dialogs;

public enum OwnerPinMode
{
    /// <summary>First PIN, or a new one after the recovery code was accepted.</summary>
    Create,

    /// <summary>From Settings: asks for the current PIN first.</summary>
    Change
}

/// <summary>
/// Creates or changes the owner's PIN. The new PIN is typed twice, since a typo here
/// would lock the owner out of her own pages. After a <see cref="OwnerPinMode.Create"/>,
/// <see cref="NewRecoveryCode"/> holds the code the host must show once.
/// </summary>
public partial class OwnerPinDialogViewModel(IOwnerAccessService owner, OwnerPinMode mode) : DialogViewModelBase
{
    // All three are pushed in from PasswordBoxes in the view, which cannot be bound.
    [ObservableProperty] private string _currentPin = string.Empty;
    [ObservableProperty] private string _newPin = string.Empty;
    [ObservableProperty] private string _repeatPin = string.Empty;

    public OwnerPinMode Mode => mode;

    public bool AsksCurrentPin => mode == OwnerPinMode.Change;

    public override string Title => mode == OwnerPinMode.Create ? Texts.CreateOwnerPinTitle : Texts.ChangePinTitle;

    /// <summary>Only the create dialog explains what the PIN is for; whoever changes it
    /// already knows.</summary>
    public string? Intro => mode == OwnerPinMode.Create ? Texts.CreateOwnerPinIntro : null;

    public string SaveText => mode == OwnerPinMode.Create ? Texts.CreatePinButton : Texts.ChangePinButton;

    /// <summary>Set once a PIN has been created; null after a change.</summary>
    public string? NewRecoveryCode { get; private set; }

    [RelayCommand]
    private async Task Save()
    {
        if (!OwnerPin.IsValid(NewPin))
        {
            ErrorValidation = Texts.PinFormatInvalid;
            return;
        }
        if (NewPin != RepeatPin)
        {
            ErrorValidation = Texts.PinsDoNotMatch;
            return;
        }

        if (mode == OwnerPinMode.Create)
        {
            NewRecoveryCode = await owner.CreatePin(NewPin);
        }
        else
        {
            var result = await owner.ChangePin(CurrentPin, NewPin);
            if (result != AccessResult.Accepted)
            {
                ErrorValidation = result == AccessResult.LockedOut
                    ? string.Format(Texts.TooManyAttempts, (int)Math.Ceiling(owner.LockoutRemaining.TotalSeconds))
                    : Texts.WrongCurrentPin;
                return;
            }
        }

        ErrorValidation = null;
        RequestClose(true);
    }

    [RelayCommand]
    private void Cancel() => RequestClose(false);
}
