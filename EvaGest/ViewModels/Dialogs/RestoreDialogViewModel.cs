using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EvaGest.Services;
using EvaGest.Resources;
using Serilog;

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

        try
        {
            await _backup.Restore(Selected.Path);
        }
        catch (BackupFromNewerVersionException ex)
        {
            // Sound, but from a later version: picking another copy is not the only way
            // out, updating the app is — so it gets its own message.
            Log.Error(ex, "Backup {BackupPath} refused for restore: made by a newer version", Selected.Path);
            ErrorValidation = Texts.BackupFromNewerVersion;
            return;
        }
        catch (InvalidDataException ex)
        {
            // Refused before the live database was touched, so the user only needs to
            // pick another copy - not the generic unexpected-error dialog.
            Log.Error(ex, "Backup {BackupPath} refused for restore", Selected.Path);
            ErrorValidation = Texts.BackupNotRestorable;
            return;
        }

        await _dialogs.Inform(Texts.BackupRestoredTitle, Texts.BackupRestoredMessage);

        ErrorValidation = null;
        RequestClose(true);
    }

    [RelayCommand]
    private void Cancel() => RequestClose(false);
}
