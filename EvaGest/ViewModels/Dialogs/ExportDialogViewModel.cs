using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EvaGest.Services;
using EvaGest.Resources;

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

        await export.ExportSales(From, To, folder);
        await dialogs.Inform(Texts.ExportDoneTitle,
            string.Format(Texts.ExportDoneMessage, folder));

        ErrorValidation = null;
        RequestClose(true);
    }

    [RelayCommand]
    private void Cancel() => RequestClose(false);
}
