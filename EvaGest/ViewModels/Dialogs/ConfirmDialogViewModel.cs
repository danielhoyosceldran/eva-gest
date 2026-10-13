using CommunityToolkit.Mvvm.Input;
using EvaGest.Resources;

namespace EvaGest.ViewModels.Dialogs;

/// <summary>
/// A plain question with two concrete answers: "Afegir-ne un altre" / "No afegir",
/// "Descartar" / "Seguir editant"… Every confirmation used to be a Windows MessageBox,
/// which ignored those words and showed "Yes" / "No" in the language of Windows, not of
/// the app (disseny-ui 9: concrete verbs, never a bare yes or no).
/// </summary>
public partial class ConfirmDialogViewModel(
    string title, string message, string confirmText, string? cancelText = null,
    bool destructive = false) : DialogViewModelBase
{
    public override string Title => title;

    public string Message => message;

    /// <summary>The verb that goes ahead.</summary>
    public string ConfirmText => confirmText;

    /// <summary>The way back; plain "Cancel·lar" when the caller has no better word.</summary>
    public string CancelText => cancelText ?? Texts.Cancel;

    /// <summary>Going ahead loses something (a delete, a discard, a restore): the view
    /// shows that answer in red and starts with the focus on the way back (B-34).</summary>
    public bool IsDestructive => destructive;

    [RelayCommand]
    private void Confirm() => RequestClose(true);

    [RelayCommand]
    private void Cancel() => RequestClose(false);
}
