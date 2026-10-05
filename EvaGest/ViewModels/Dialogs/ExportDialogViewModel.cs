using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EvaGest.Services;
using EvaGest.Resources;
using Serilog;

namespace EvaGest.ViewModels.Dialogs;

/// <summary>Export període, pantalles 3.8.</summary>
public partial class ExportDialogViewModel(IExportService export, IDialogService dialogs) : DialogViewModelBase
{
    [ObservableProperty] private DateOnly _from = new(DateOnly.FromDateTime(DateTime.Today).Year,
        DateOnly.FromDateTime(DateTime.Today).Month, 1);
    [ObservableProperty] private DateOnly _to = DateOnly.FromDateTime(DateTime.Today);

    public override string Title => Texts.ExportPeriod;

    [RelayCommand]
    private async Task Export()
    {
        if (To < From)
        {
            ErrorValidation = Texts.ExportToBeforeFrom;
            return;
        }

        var folder = await dialogs.AskFolder(Texts.ChooseDestinationFolder);
        if (folder is null) return;

        try
        {
            await export.ExportSales(From, To, folder);
        }
        catch (ExportFileInUseException ex)
        {
            // Re-exporting a period whose CSV is still open in Excel: nothing was written,
            // so naming the file and saying to close it is all the user needs.
            Log.Error(ex, "Export to {Folder} refused: {File} is open in another program", folder, ex.FileName);
            ErrorValidation = string.Format(Texts.ExportFileInUse, ex.FileName);
            return;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A read-only, full or vanished folder: another folder is the way out.
            Log.Error(ex, "Export to {Folder} failed", folder);
            ErrorValidation = Texts.ExportFolderNotWritable;
            return;
        }

        await dialogs.Inform(Texts.ExportDoneTitle,
            string.Format(Texts.ExportDoneMessage, folder));

        ErrorValidation = null;
        RequestClose(true);
    }

    [RelayCommand]
    private void Cancel() => RequestClose(false);
}
