using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EvaGest.Services;
using EvaGest.Resources;

namespace EvaGest.ViewModels.Dialogs;

/// <summary>Restore a backup, pantalles 3.9.</summary>
public partial class RestoreDialogViewModel : DialogViewModelBase
{
    private readonly IBackupService _backup;
    private readonly IDialogService _dialogs;

    public ObservableCollection<BackupInfo> Backups { get; } = [];

    [ObservableProperty] private BackupInfo? _selected;

    public override string Title => Texts.RestoreBackupTitle;

    public RestoreDialogViewModel(IBackupService backup, IDialogService dialogs)
    {
        _backup = backup;
        _dialogs = dialogs;
    }

    public async Task Load()
    {
        Backups.Clear();
        foreach (var c in await _backup.ListAll()) Backups.Add(c);
    }

    [RelayCommand]
    private async Task Restore()
    {
        if (Selected is null)
        {
            ErrorValidation = Texts.ChooseBackupFromList;
            return;
        }

        bool confirmed = await _dialogs.Confirm(
            Texts.ConfirmRestoreTitle,
            Texts.ConfirmRestoreMessage,
            Texts.Restore);

        if (!confirmed) return;

        await _backup.Restore(Selected.Path);
        await _dialogs.Inform(Texts.BackupRestoredTitle, Texts.BackupRestoredMessage);

        ErrorValidation = null;
        RequestClose(true);
    }

    [RelayCommand]
    private void Cancel() => RequestClose(false);
}
