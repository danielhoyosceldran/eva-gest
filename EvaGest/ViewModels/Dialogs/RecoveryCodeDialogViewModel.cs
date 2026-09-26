using CommunityToolkit.Mvvm.Input;
using EvaGest.Resources;

namespace EvaGest.ViewModels.Dialogs;

/// <summary>Shows the paper recovery code the one and only time it exists in clear:
/// only its hash is stored, so it cannot be shown again later.</summary>
public partial class RecoveryCodeDialogViewModel(string code) : DialogViewModelBase
{
    public string Code => code;

    public override string Title => Texts.RecoveryCodeTitle;

    [RelayCommand]
    private void Done() => RequestClose(true);
}
