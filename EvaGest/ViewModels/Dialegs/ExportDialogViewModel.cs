using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EvaGest.Services;

namespace EvaGest.ViewModels.Dialegs;

/// <summary>Exportar període, pantalles 3.8.</summary>
public partial class ExportDialogViewModel(IExportService export, IDialogService dialegs) : DialegViewModelBase
{
    [ObservableProperty] private DateOnly _des = new(DateOnly.FromDateTime(DateTime.Today).Year,
        DateOnly.FromDateTime(DateTime.Today).Month, 1);
    [ObservableProperty] private DateOnly _fins = DateOnly.FromDateTime(DateTime.Today);

    public override string Titol => "Exportar període";

    [RelayCommand]
    private async Task Exportar()
    {
        if (Fins < Des)
        {
            ErrorValidacio = "La data «fins a» ha de ser posterior a «des de».";
            return;
        }

        var carpeta = await dialegs.DemanarCarpeta("Tria la carpeta de destí");
        if (carpeta is null) return;

        await export.ExportarVendes(Des, Fins, carpeta);
        await dialegs.Informar("Exportació completada",
            $"S'han generat els fitxers a:\n{carpeta}");

        ErrorValidacio = null;
        SolicitarTancar(true);
    }

    [RelayCommand]
    private void Cancellar() => SolicitarTancar(false);
}
